using System.Net;
using System.Net.Http.Json;
using Bank.Domain.Bancos;
using Bank.Domain.Carteiras;
using Microsoft.EntityFrameworkCore;

namespace Bank.IntegrationTests;

/// <summary>
/// Cadastro gera conta corrente no banco 001 (specs/012-dados-bancarios-carteira, US1, FR-005,
/// FR-006, FR-008, SC-001, SC-003).
/// </summary>
public class DadosBancariosCadastroTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private const int Simultaneos = 50;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CadastroPfEPj_ContaCorrenteNoOneBankComDigitoValido()
    {
        using var client = factory.CreateClient();
        var pf = await ApiTransferencias.CriarPfAsync(client);
        var pj = await ApiTransferencias.CriarPjAsync(client);

        var contaPf = await ContaAsync(pf.CarteiraId);
        var contaPj = await ContaAsync(pj.CarteiraId);

        foreach (var conta in new[] { contaPf, contaPj })
        {
            Assert.Equal(Banco.CodigoOneBank, conta.CodigoBanco);
            Assert.Equal(ContaBancaria.AgenciaPadrao, conta.Agencia);
            Assert.Matches("^[0-9]{8}$", conta.Numero);
            Assert.Equal(ContaBancaria.CalcularDigito(conta.Agencia, conta.Numero), conta.Digito);
            Assert.Equal(TipoContaBancaria.ContaCorrente, conta.Tipo);
        }

        Assert.NotEqual(contaPf.Numero, contaPj.Numero);
    }

    [Fact]
    public async Task CadastrosSimultaneos_NumerosDistintos()
    {
        using var client = factory.CreateClient();
        using var largada = new SemaphoreSlim(0);

        var tarefas = Enumerable.Range(0, Simultaneos).Select(async _ =>
        {
            await largada.WaitAsync(Ct);
            var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf());
            return Guid.Parse(cadastro.GetProperty("carteiraId").GetString()!);
        }).ToArray();

        largada.Release(Simultaneos);
        var carteiras = (await Task.WhenAll(tarefas)).Select(CarteiraId.De).ToList();

        await using var contexto = factory.CriarContexto();
        var numeros = await contexto.Carteiras
            .Where(c => carteiras.Contains(c.Id))
            .Select(c => c.NumeroConta)
            .ToListAsync(Ct);

        Assert.Equal(Simultaneos, numeros.Count);
        Assert.Equal(Simultaneos, numeros.Distinct().Count());
    }

    [Fact]
    public async Task CadastroRecusado_NaoCriaCarteira()
    {
        using var client = factory.CreateClient();
        var email = Dados.Email();
        await ApiBank.RegistrarAsync(client, Dados.CadastroPf(email));
        await using var antes = factory.CriarContexto();
        var quantidade = await antes.Carteiras.CountAsync(Ct);

        using var resposta = await client.PostAsJsonAsync(ApiBank.Registrar, Dados.CadastroPf(email), Ct);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        await using var depois = factory.CriarContexto();
        Assert.Equal(quantidade, await depois.Carteiras.CountAsync(Ct));
    }

    [Fact]
    public async Task CadastroPedindoPoupanca_IgnoraEEhContaCorrente()
    {
        using var client = factory.CreateClient();
        var cadastro = new
        {
            nomeCompleto = "Lucas Silva",
            email = Dados.Email(),
            documento = Dados.Cpf(),
            tipoConta = "PessoaFisica",
            senha = Dados.SenhaValida,
            tipoContaBancaria = "ContaPoupanca",
        };

        var registrado = await ApiBank.RegistrarAsync(client, cadastro);

        var conta = await ContaAsync(Guid.Parse(registrado.GetProperty("carteiraId").GetString()!));
        Assert.Equal(TipoContaBancaria.ContaCorrente, conta.Tipo);
    }

    private async Task<ContaBancaria> ContaAsync(Guid carteiraId)
    {
        await using var contexto = factory.CriarContexto();
        var carteira = await contexto.Carteiras.AsNoTracking().SingleAsync(c => c.Id == CarteiraId.De(carteiraId), Ct);
        return carteira.ContaBancaria;
    }
}
