using System.Net;
using Bank.Domain.Bancos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bank.IntegrationTests;

/// <summary>
/// Scripts de <c>scripts/sql/dados-bancarios</c>: contas de outros bancos e titular com conta
/// poupança, reexecutáveis (specs/012-dados-bancarios-carteira, US4, FR-010, FR-011, FR-014,
/// SC-005; research R8). Cada script roda duas vezes no banco desta fábrica.
/// </summary>
public class ScriptsDadosBancariosTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private const string EmailPoupanca = "poupanca.demo@exemplo.com.br";
    private const string SenhaPoupanca = "Poupanca@123";
    private const string DocumentoTitularExterno = "21098765494";

    private static readonly SemaphoreSlim Execucao = new(1, 1);
    private static bool _executado;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Executa os dois scripts duas vezes, uma única vez por execução da classe.</summary>
    private async Task GarantirScriptsAsync()
    {
        await Execucao.WaitAsync(Ct);
        try
        {
            if (_executado)
            {
                return;
            }

            _ = factory.Server; // migrations aplicadas
            await using var conexao = new NpgsqlConnection(factory.ConnectionString);
            await conexao.OpenAsync(Ct);
            foreach (var script in new[] { "registrar-contas-outros-bancos.sql", "criar-conta-poupanca.sql" })
            {
                var sql = await File.ReadAllTextAsync(CaminhoDoScript(script), Ct);
                for (var vez = 0; vez < 2; vez++)
                {
                    await using var comando = new NpgsqlCommand(sql, conexao);
                    await comando.ExecuteNonQueryAsync(Ct);
                }
            }

            _executado = true;
        }
        finally
        {
            Execucao.Release();
        }
    }

    [Fact]
    public async Task ContasDeOutrosBancos_RegistradasUmaVezComDigitoValido()
    {
        await GarantirScriptsAsync();
        await using var contexto = factory.CriarContexto();
        var contas = await contexto.ContasExternas.AsNoTracking().ToListAsync(Ct);

        Assert.Equal(8, contas.Count);
        Assert.Equal(["002", "003", "004", "005"], contas.Select(c => c.CodigoBanco).Distinct().Order(StringComparer.Ordinal));
        Assert.All(contas.GroupBy(c => c.CodigoBanco), banco =>
            Assert.Equal([TipoContaBancaria.ContaCorrente, TipoContaBancaria.ContaPoupanca], banco.Select(c => c.TipoContaBancaria).Order()));
        Assert.All(contas, c => Assert.Equal(ContaBancaria.CalcularDigito(c.Agencia, c.NumeroConta), c.DigitoConta));
        Assert.DoesNotContain(contas, c => c.CodigoBanco == Banco.CodigoOneBank);
    }

    [Fact]
    public async Task ContaPoupanca_TitularUnicoEntraEVePoupancaNoOneBank()
    {
        await GarantirScriptsAsync();
        await using (var contexto = factory.CriarContexto())
        {
            Assert.Equal(1, await contexto.Usuarios.CountAsync(u => u.Email == EmailPoupanca, Ct));
        }

        using var client = factory.CreateClient();
        var token = await ApiBank.ObterTokenAsync(client, EmailPoupanca, SenhaPoupanca);
        using var resposta = await ApiTransferencias.GetComTokenAsync(client, token, "/api/v1/carteiras/minha");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var conta = (await ApiBank.LerJsonAsync(resposta)).GetProperty("contaBancaria");
        Assert.Equal("001", conta.GetProperty("banco").GetProperty("codigo").GetString());
        Assert.Equal("0001", conta.GetProperty("agencia").GetString());
        Assert.Equal("ContaPoupanca", conta.GetProperty("tipo").GetString());
        Assert.Equal(
            ContaBancaria.CalcularDigito("0001", conta.GetProperty("numero").GetString()!),
            conta.GetProperty("digito").GetString());
    }

    [Fact]
    public async Task ContaExterna_NaoEhDestinoDeTransferencia()
    {
        await GarantirScriptsAsync();
        using var client = factory.CreateClient();
        var pagador = await ApiTransferencias.CriarPfAsync(client);

        using var resposta = await ApiTransferencias.TransferirAsync(client, pagador, DocumentoTitularExterno, 1m);

        await ApiBank.AssertProblemaAsync(resposta, HttpStatusCode.NotFound, "urn:bank:problema:destinatario-nao-encontrado");
    }

    [Theory]
    [InlineData("999", PostgresErrorCodes.ForeignKeyViolation, "fk_contas_externas_bancos_codigo_banco")]
    [InlineData("001", PostgresErrorCodes.CheckViolation, "ck_contas_externas_codigo_banco")]
    public async Task ContaExterna_BancoInvalido_Recusada(string codigoBanco, string erroEsperado, string restricao)
    {
        await using var contexto = factory.CriarContexto();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO contas_externas (id, codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria, titular_nome, titular_documento, criado_em)
            VALUES (uuidv7(), {0}, '0999', '99000001', calcular_digito_conta('0999', '99000001'), 'conta_corrente', 'Teste', '21098765494', now())
            """,
            [codigoBanco],
            Ct));

        Assert.Equal(erroEsperado, erro.SqlState);
        Assert.Equal(restricao, erro.ConstraintName);
    }

    private static string CaminhoDoScript(string nome)
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "CONSTITUTION.md")))
        {
            diretorio = diretorio.Parent;
        }

        Assert.NotNull(diretorio);
        return Path.Combine(diretorio.FullName, "scripts", "sql", "dados-bancarios", nome);
    }
}
