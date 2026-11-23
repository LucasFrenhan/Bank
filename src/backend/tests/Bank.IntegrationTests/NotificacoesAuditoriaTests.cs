using System.Collections.Concurrent;
using Bank.Application.Abstracoes;
using Bank.Domain.Comum;
using Bank.Domain.Notificacoes;
using Bank.Domain.Usuarios;
using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RabbitMQ.Client;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>Controle compartilhado do dublê: quantas gravações falham por evento (ou sempre).</summary>
public sealed class FalhasDeGravacao
{
    private readonly ConcurrentDictionary<EventoId, int> _restantes = new();

    public void Falhar(Guid eventoId, int vezes) => _restantes[EventoId.De(eventoId)] = vezes;

    public void FalharSempre(Guid eventoId) => _restantes[EventoId.De(eventoId)] = int.MaxValue;

    public void Normalizar(Guid eventoId) => _restantes.TryRemove(EventoId.De(eventoId), out _);

    internal bool DeveFalhar(EventoId eventoId)
    {
        if (!_restantes.TryGetValue(eventoId, out var restantes) || restantes <= 0)
        {
            return false;
        }

        _restantes[eventoId] = restantes == int.MaxValue ? restantes : restantes - 1;
        return true;
    }
}

/// <summary>
/// Repositório real que, quando configurado, grava a 1ª notificação (com <c>SaveChangesAsync</c>, na
/// transação do consumidor) e falha antes da 2ª: a falha acontece com uma linha já escrita.
/// </summary>
internal sealed class NotificacoesAuditoriaQueFalham(BankDbContext contexto, FalhasDeGravacao falhas) : INotificacoesAuditoria
{
    private readonly NotificacoesAuditoriaRepositorio _real = new(contexto);

    public Task<bool> ExistemParaEventoAsync(EventoId eventoId, CancellationToken cancellationToken) =>
        _real.ExistemParaEventoAsync(eventoId, cancellationToken);

    public async Task AdicionarAsync(IReadOnlyCollection<NotificacaoAuditoria> notificacoes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificacoes);
        var primeira = notificacoes.First();
        if (!falhas.DeveFalhar(primeira.EventoId))
        {
            await _real.AdicionarAsync(notificacoes, cancellationToken);
            return;
        }

        await _real.AdicionarAsync([primeira], cancellationToken);
        throw new InvalidOperationException("Falha ao gravar a segunda notificação (dublê).");
    }
}

/// <summary>Ambiente com a gravação das notificações trocada pelo dublê que falha.</summary>
public sealed class AmbienteNotificacoesControlaveis : AmbienteMensageria
{
    public FalhasDeGravacao Falhas { get; } = new();

    protected override void ConfigurarServicosDoWorker(IServiceCollection services) =>
        services.AddScoped<INotificacoesAuditoria>(sp => new NotificacoesAuditoriaQueFalham(sp.GetRequiredService<BankDbContext>(), Falhas));
}

/// <summary>
/// Falha no registro não deixa notificação pela metade (specs/011-notificacoes-auditoria, US3:
/// FR-005, FR-006; SC-004, SC-005): rollback de tudo, nova tentativa, DLQ e reprocessamento.
/// Intervalos de teste: 100, 200 e 400 ms.
/// </summary>
public class NotificacoesAuditoriaFalhasTests(AmbienteNotificacoesControlaveis ambiente) : IClassFixture<AmbienteNotificacoesControlaveis>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FalhaNaSegundaNotificacao_DesfazTudo_ENovaTentativaGravaUmPar()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();
        ambiente.Falhas.Falhar(eventoId, 2);

        await PublicarAsync(eventoId);

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarProcessadosAsync(eventoId) == 1),
            "O evento deveria ser processado na 3ª execução.");
        Assert.Equal([TipoNotificacao.ComprovanteEnvio, TipoNotificacao.AvisoCredito], await TiposGravadosAsync(eventoId));
        Assert.True(await BankWorkerFactory.AguardarAsync(() =>
            worker.TextosDosLogs().Count(t => t.Contains($"EventoId={eventoId}", StringComparison.Ordinal) && t.Contains("nova tentativa", StringComparison.Ordinal)) == 2));
    }

    [Fact]
    public async Task FalhaPermanente_DlqSemNotificacoes_ReprocessamentoGravaUmPar()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();
        ambiente.Falhas.FalharSempre(eventoId);

        await PublicarAsync(eventoId);

        var naDlq = await AguardarNaDlqAsync(eventoId);
        Assert.Equal("tentativas-esgotadas", NovaTentativaEDlqTests.Texto(naDlq, "x-motivo"));
        Assert.Empty(await TiposGravadosAsync(eventoId));
        Assert.Equal(0, await ContarProcessadosAsync(eventoId));

        // Reprocessamento manual com a causa corrigida: o par é gravado uma vez.
        ambiente.Falhas.Normalizar(eventoId);
        await MoverParaAFilaPrincipalAsync(naDlq);

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await ContarProcessadosAsync(eventoId) == 1));
        Assert.Equal([TipoNotificacao.ComprovanteEnvio, TipoNotificacao.AvisoCredito], await TiposGravadosAsync(eventoId));
        Assert.True(await BankWorkerFactory.AguardarAsync(() => worker.TextosDosLogs().Any(t =>
            t.Contains($"EventoId={eventoId}", StringComparison.Ordinal) && t.Contains("Reprocessamentos=1", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task DestinatarioInexistente_VaiParaDlq()
    {
        await ambiente.Worker.IniciarProntoAsync();
        var eventoId = Guid.CreateVersion7();
        var (pagador, _) = await UsuariosDeTeste.CriarParAsync(ambiente.Api);

        await PublicarAsync(eventoId, ConsumoIdempotenteTests.Payload(eventoId, pagador, recebedorUsuarioId: Guid.CreateVersion7()));

        var naDlq = await AguardarNaDlqAsync(eventoId);
        Assert.Equal("tentativas-esgotadas", NovaTentativaEDlqTests.Texto(naDlq, "x-motivo"));
        Assert.Empty(await TiposGravadosAsync(eventoId));
        Assert.Equal(0, await ContarProcessadosAsync(eventoId));
    }

    private async Task PublicarAsync(Guid eventoId, string? corpo = null)
    {
        if (corpo is null)
        {
            var (pagador, recebedor) = await UsuariosDeTeste.CriarParAsync(ambiente.Api);
            corpo = ConsumoIdempotenteTests.Payload(eventoId, pagador, recebedor);
        }

        var (conexao, canal) = await ambiente.Worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, corpo, eventoId.ToString(), cancellationToken: Ct);
        }
    }

    /// <summary>Como o "Move messages" da interface de gestão: corpo, propriedades e headers intactos.</summary>
    private async Task MoverParaAFilaPrincipalAsync(BasicGetResult mensagem)
    {
        var (conexao, canal) = await ambiente.Worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await canal.BasicPublishAsync(string.Empty, "notificacoes.transferencias.fila", mandatory: true, new BasicProperties(mensagem.BasicProperties), mensagem.Body, Ct);
        }
    }

    private async Task<BasicGetResult> AguardarNaDlqAsync(Guid eventoId)
    {
        BasicGetResult? encontrada = null;
        Assert.True(await BankWorkerFactory.AguardarAsync(async () => (encontrada = await NovaTentativaEDlqTests.LerDaDlqAsync(ambiente.Worker, eventoId)) is not null),
            $"A mensagem {eventoId} não chegou à DLQ.");
        return encontrada!;
    }

    private async Task<int> ContarProcessadosAsync(Guid eventoId)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        return await contexto.EventosProcessados.CountAsync(e => e.EventoId == EventoId.De(eventoId), Ct);
    }

    private async Task<List<TipoNotificacao>> TiposGravadosAsync(Guid eventoId)
    {
        await using var contexto = ambiente.Api.CriarContexto();
        var tipos = await contexto.NotificacoesAuditoria.Where(n => n.EventoId == EventoId.De(eventoId)).Select(n => n.Tipo).ToListAsync(Ct);
        return [.. tipos.Order()];
    }
}

/// <summary>
/// Restrições da tabela <c>notificacoes_auditoria</c> (specs/011-notificacoes-auditoria, FR-006,
/// FR-008; Clarifications Q1 e Q4): índice único por evento e tipo, e usuário com notificações
/// não pode ser excluído.
/// </summary>
public class NotificacoesAuditoriaIntegridadeTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task IndiceUnico_RejeitaSegundaNotificacaoDoMesmoTipo()
    {
        using var client = factory.CreateClient();
        var pf = await CriarPfAsync(client);
        var eventoId = EventoId.De(Guid.CreateVersion7());
        var agora = DateTimeOffset.UtcNow;

        await using (var contexto = factory.CriarContexto())
        {
            contexto.NotificacoesAuditoria.Add(NotificacaoAuditoria.ComprovanteEnvio(eventoId, UsuarioId.De(pf.UsuarioId), 1m, null, agora));
            await contexto.SaveChangesAsync(Ct);
        }

        await using (var contexto = factory.CriarContexto())
        {
            contexto.NotificacoesAuditoria.Add(NotificacaoAuditoria.ComprovanteEnvio(eventoId, UsuarioId.De(pf.UsuarioId), 1m, null, agora));
            var erro = await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync(Ct));
            var postgres = Assert.IsType<PostgresException>(erro.InnerException);
            Assert.Equal("ux_notificacoes_auditoria_evento_tipo", postgres.ConstraintName);
        }
    }

    [Fact]
    public async Task UsuarioComNotificacoes_NaoPodeSerExcluido()
    {
        // Usuário sem carteira, para que só a chave estrangeira nova bloqueie a exclusão.
        // SQL só de teste (isento da regra de arquivos .sql — constituição v1.1.0).
        var usuarioId = Guid.CreateVersion7();
        await using (var contexto = factory.CriarContexto())
        {
            await contexto.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO usuarios (id, nome_completo, email, documento, tipo_conta, senha_hash, criado_em)
                VALUES ({0}, 'Sem Carteira', {1}, {2}, 'PessoaFisica', 'hash', now())
                """,
                [usuarioId, Dados.Email(), Dados.Cpf()],
                Ct);
            contexto.NotificacoesAuditoria.Add(
                NotificacaoAuditoria.AvisoCredito(EventoId.De(Guid.CreateVersion7()), UsuarioId.De(usuarioId), 1m, null, DateTimeOffset.UtcNow));
            await contexto.SaveChangesAsync(Ct);
        }

        await using (var contexto = factory.CriarContexto())
        {
            var erro = await Assert.ThrowsAsync<PostgresException>(() =>
                contexto.Database.ExecuteSqlRawAsync("DELETE FROM usuarios WHERE id = {0}", [usuarioId], Ct));
            Assert.Equal(PostgresErrorCodes.RestrictViolation, erro.SqlState); // ON DELETE RESTRICT → 23001
            Assert.Equal("fk_notificacoes_auditoria_usuarios_usuario_destino_id", erro.ConstraintName);
        }
    }
}
