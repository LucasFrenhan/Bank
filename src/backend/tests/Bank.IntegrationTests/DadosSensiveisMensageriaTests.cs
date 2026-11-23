using System.Net;
using Bank.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Nomes, descrição, documentos, e-mails, payload e credenciais do broker nunca aparecem em logs,
/// spans ou métricas da mensageria, e as tags de métrica não têm identificadores
/// (specs/010-mensageria-rabbitmq, FR-023; constituição, Princípio VI). O texto das notificações
/// persistidas, que leva os nomes, também fica fora da telemetria (specs/011-notificacoes-auditoria, FR-010).
/// </summary>
public class DadosSensiveisMensageriaTests(AmbienteMensageria ambiente) : IClassFixture<AmbienteMensageria>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly HashSet<string> TagsPermitidas = new(StringComparer.Ordinal) { "tipo_evento", "status", "motivo", "papel" };

    [Fact]
    public async Task PublicacaoConsumoEDlq_NaoExpoemDadosSensiveis()
    {
        var worker = ambiente.Worker;
        await worker.IniciarProntoAsync();
        using var client = ambiente.Api.CreateClient();

        const string nomePagador = "Pagadora Confidencial";
        const string nomeRecebedor = "Recebedora Sigilosa Ltda";
        const string descricao = "Presente secreto";
        var emailPagador = Dados.Email();
        var cpf = Dados.Cpf();
        var emailRecebedor = Dados.Email();
        var cnpj = Dados.CnpjAlfanumerico();
        var cadastro = await ApiBank.RegistrarAsync(client, new { nomeCompleto = nomePagador, email = emailPagador, documento = cpf, tipoConta = "PessoaFisica", senha = Dados.SenhaValida });
        await ApiBank.RegistrarAsync(client, new { nomeCompleto = nomeRecebedor, email = emailRecebedor, documento = cnpj, tipoConta = "PessoaJuridica", senha = Dados.SenhaValida });
        var pagador = new ContaTeste(emailPagador, cpf, cadastro.GetProperty("usuarioId").GetGuid(), cadastro.GetProperty("carteiraId").GetGuid(),
            await ApiBank.ObterTokenAsync(client, emailPagador));

        using (var resposta = await TransferirAsync(client, pagador, cnpj, 1.00m, descricao: descricao))
        {
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(() =>
            worker.TextosDosLogs().Count(t => t.Contains("Notificação de transferência", StringComparison.Ordinal)
                && t.Contains($"UsuarioId={pagador.UsuarioId}", StringComparison.Ordinal)) == 1));

        // Mensagem inválida carregando os mesmos dados: vai para a DLQ sem vazar nada no diagnóstico.
        var invalidaId = Guid.CreateVersion7();
        var (conexao, canal) = await worker.AbrirCanalAsync(Ct);
        await using (conexao)
        await using (canal)
        {
            await BankWorkerFactory.PublicarAsync(canal, $$"""{"eventoId":"{{invalidaId}}","pagadorNome":"{{nomePagador}}","descricao":"{{descricao}}"}""",
                invalidaId.ToString(), cancellationToken: Ct);
        }

        Assert.True(await BankWorkerFactory.AguardarAsync(async () => await NovaTentativaEDlqTests.LerDaDlqAsync(worker, invalidaId) is { } naDlq
            && !Contem(NovaTentativaEDlqTests.Texto(naDlq, "x-ultimo-erro") ?? string.Empty, nomePagador, descricao)));

        string[] sensiveis = [nomePagador, nomeRecebedor, descricao, cpf, Dados.CpfComMascara(cpf), cnpj, emailPagador, emailRecebedor, RabbitMqCompartilhado.Senha];
        var textos = worker.TextosDosLogs()
            .Concat(worker.Spans.Copiar().SelectMany(s => s.TagObjects.Select(t => $"{s.DisplayName} {t.Key}={t.Value}")))
            .Concat(worker.Medicoes.SelectMany(m => m.Tags.Select(t => $"{m.Nome} {t.Key}={t.Value}")))
            .ToList();
        Assert.NotEmpty(textos);

        // Spec 011 (FR-010): os nomes entram só no texto persistido, nunca na telemetria.
        // O log da notificação sai antes do commit do consumidor: aguarda a linha ficar visível.
        List<string> mensagens = [];
        Assert.True(await BankWorkerFactory.AguardarAsync(async () =>
        {
            await using var contexto = ambiente.Api.CriarContexto();
            mensagens = await contexto.NotificacoesAuditoria
                .Where(n => n.UsuarioDestinoId == UsuarioId.De(pagador.UsuarioId))
                .Select(n => n.Mensagem)
                .ToListAsync(Ct);
            return mensagens.Count == 1;
        }));

        Assert.Equal($"Você enviou R$ 1,00 para {nomeRecebedor}.", mensagens[0]);
        Assert.DoesNotContain(textos, t => t.Contains("Você enviou", StringComparison.Ordinal) || t.Contains("Você recebeu", StringComparison.Ordinal));
        foreach (var sensivel in sensiveis)
        {
            Assert.DoesNotContain(textos, t => t.Contains(sensivel, StringComparison.OrdinalIgnoreCase));
        }

        Assert.All(worker.Medicoes.Where(m => m.Nome.StartsWith("banco.", StringComparison.Ordinal) || m.Nome.StartsWith("bank.notificacoes", StringComparison.Ordinal)),
            m => Assert.Subset(TagsPermitidas, m.Tags.Keys.ToHashSet(StringComparer.Ordinal)));
    }

    private static bool Contem(string texto, params string[] valores) => valores.Any(v => texto.Contains(v, StringComparison.OrdinalIgnoreCase));
}
