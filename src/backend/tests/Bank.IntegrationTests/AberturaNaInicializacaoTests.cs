using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Bank.IntegrationTests;

/// <summary>
/// Abertura das carteiras anteriores ao razão ao iniciar a aplicação (spec 006, FR-016; research
/// R5): idempotente e só com a flag ligada.
/// </summary>
public class AberturaNaInicializacaoTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CarteiraSemAbertura_RecebeAoReiniciar_SemDuplicar_ESoComAFlag()
    {
        // Carteira gravada sem o lançamento, como as criadas pela spec 003.
        var legado = Usuario.Criar(
            "Conta Antiga",
            Email.Criar(Dados.Email()).Valor,
            Documento.Criar(Dados.Cpf(), TipoConta.PessoaFisica).Valor,
            "hash",
            10m,
            DateTimeOffset.UtcNow);
        await using (var contexto = factory.CriarContexto())
        {
            contexto.Usuarios.Add(legado);
            await contexto.SaveChangesAsync(Ct);
        }

        await ReiniciarAsync(flag: false);
        Assert.Equal(0, await AberturasAsync(legado.Carteira.Id));

        await ReiniciarAsync(flag: true);
        await ReiniciarAsync(flag: true);

        await using var verificacao = factory.CriarContexto();
        var abertura = await verificacao.Lancamentos.SingleAsync(l => l.CarteiraId == legado.Carteira.Id, Ct);
        Assert.Equal((TipoLancamento.Abertura, DirecaoLancamento.Entrada, 10m, Lancamento.DescricaoAbertura),
            (abertura.Tipo, abertura.Direcao, abertura.Valor, abertura.Descricao));
    }

    private async Task ReiniciarAsync(bool flag)
    {
        await using var reiniciada = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Persistencia:CriarLancamentosDeAberturaNaInicializacao"] = flag ? "true" : "false" })));
        _ = reiniciada.Server;
    }

    private async Task<int> AberturasAsync(Guid carteiraId)
    {
        await using var contexto = factory.CriarContexto();
        return await contexto.Lancamentos.CountAsync(l => l.CarteiraId == carteiraId && l.Tipo == TipoLancamento.Abertura, Ct);
    }
}
