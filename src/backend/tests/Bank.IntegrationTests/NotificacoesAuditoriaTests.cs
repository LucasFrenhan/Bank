using Bank.Domain.Comum;
using Bank.Domain.Notificacoes;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

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
