using Bank.Domain.Bancos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.IntegrationTests;

/// <summary>
/// Restrições do banco para os dados bancários e a function do dígito
/// (specs/012-dados-bancarios-carteira, FR-001, FR-003, FR-004, FR-014; research R1, R4, R5).
/// SQL só de teste (isento da regra de arquivos .sql — constituição v1.1.0).
/// </summary>
public class DadosBancariosRestricoesTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("0001", "00000001")]
    [InlineData("0001", "00000002")]
    [InlineData("0001", "00000042")]
    [InlineData("0001", "12345678")]
    [InlineData("0001", "99999999")]
    [InlineData("9876", "54321098")]
    public async Task FunctionSql_IgualAoCalculoEmCSharp(string agencia, string numero)
    {
        await using var contexto = factory.CriarContexto();

        var digito = await contexto.Database
            .SqlQueryRaw<string>("SELECT calcular_digito_conta({0}, {1})::text AS \"Value\"", agencia, numero)
            .SingleAsync(Ct);

        Assert.Equal(ContaBancaria.CalcularDigito(agencia, numero), digito);
    }

    [Fact]
    public async Task Bancos_ExatamenteOsCinco()
    {
        await using var contexto = factory.CriarContexto();

        var bancos = await contexto.Bancos.AsNoTracking().OrderBy(b => b.Codigo).Select(b => new { b.Codigo, b.Nome }).ToListAsync(Ct);

        Assert.Equal(
            [("001", "One Bank"), ("002", "Banco Aurora"), ("003", "Banco Horizonte"), ("004", "Banco Atlântico"), ("005", "Banco Cerrado")],
            bancos.Select(b => (b.Codigo, b.Nome)));
    }

    [Theory]
    [InlineData("digito_conta = CASE WHEN digito_conta = '0' THEN '1' ELSE '0' END", "ck_carteiras_digito_conta")]
    [InlineData("codigo_banco = '002'", "ck_carteiras_codigo_banco")]
    [InlineData("tipo_conta_bancaria = 'outro'", "ck_carteiras_tipo_conta_bancaria")]
    public async Task Carteira_DadoInvalido_Recusado(string alteracao, string restricao)
    {
        var carteiraId = await NovaCarteiraAsync();

        var erro = await AlterarAsync(carteiraId, alteracao);

        Assert.Equal(PostgresErrorCodes.CheckViolation, erro.SqlState);
        Assert.Equal(restricao, erro.ConstraintName);
    }

    [Fact]
    public async Task Carteira_NumeroRepetido_Recusado()
    {
        var primeira = await NovaCarteiraAsync();
        var segunda = await NovaCarteiraAsync();
        await using var contexto = factory.CriarContexto();
        var numero = await contexto.Database
            .SqlQueryRaw<string>("SELECT numero_conta::text AS \"Value\" FROM carteiras WHERE id = {0}", primeira)
            .SingleAsync(Ct);

        var erro = await AlterarAsync(segunda, $"numero_conta = '{numero}', digito_conta = calcular_digito_conta(agencia, '{numero}')");

        Assert.Equal(PostgresErrorCodes.UniqueViolation, erro.SqlState);
        Assert.Equal("ux_carteiras_conta_bancaria", erro.ConstraintName);
    }

    private async Task<Guid> NovaCarteiraAsync()
    {
        using var client = factory.CreateClient();
        var cadastro = await ApiBank.RegistrarAsync(client, Dados.CadastroPf());
        return Guid.Parse(cadastro.GetProperty("carteiraId").GetString()!);
    }

    private async Task<PostgresException> AlterarAsync(Guid carteiraId, string alteracao)
    {
        await using var contexto = factory.CriarContexto();
#pragma warning disable EF1002 // SQL de teste com alterações fixas das InlineData
        return await Assert.ThrowsAsync<PostgresException>(() =>
            contexto.Database.ExecuteSqlRawAsync($"UPDATE carteiras SET {alteracao} WHERE id = {{0}}", [carteiraId], Ct));
#pragma warning restore EF1002
    }
}
