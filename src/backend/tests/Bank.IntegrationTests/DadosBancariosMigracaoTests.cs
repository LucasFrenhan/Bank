using Bank.Domain.Bancos;
using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bank.IntegrationTests;

/// <summary>
/// Backfill das carteiras existentes pela migration AdicionarDadosBancariosCarteira
/// (specs/012-dados-bancarios-carteira, US2, FR-007, FR-008, SC-002; research R7). Cada teste usa
/// um banco próprio no contêiner compartilhado, migrado até a versão anterior, com dados gravados
/// por SQL de teste (isento da regra de arquivos .sql — constituição v1.1.0).
/// </summary>
public class DadosBancariosMigracaoTests
{
    private const string MigracaoAnterior = "20261123141050_AdicionarNotificacoesAuditoria";
    private const string MigracaoDadosBancarios = "20261123185853_AdicionarDadosBancariosCarteira";

    private const string ConsultaCarteiras = "SELECT id::text || '|' || usuario_id || '|' || saldo || '|' || moeda || '|' || atualizado_em FROM carteiras ORDER BY id";
    private const string ConsultaTransferencias = "SELECT t::text FROM transferencias t ORDER BY id";
    // Só as colunas daquela versão: a spec 014 acrescentou fatura_id e recebivel_id (nulas).
    private const string ConsultaLancamentos = "SELECT (id, carteira_id, tipo, direcao, valor, transferencia_id, descricao, criado_em)::text FROM lancamentos ORDER BY id";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CarteirasExistentes_RecebemDadosEmOrdemDeCriacao_SemMudarSaldoNemHistorico()
    {
        var connectionString = await PostgresCompartilhado.CriarConnectionStringAsync();
        await MigrarAsync(connectionString, MigracaoAnterior);

        var carteiras = await PopularVersaoAnteriorAsync(connectionString);
        var antes = await FotografarAsync(connectionString);

        await MigrarAsync(connectionString, null);

        Assert.Equal(antes, await FotografarAsync(connectionString));

        await using var conexao = await AbrirAsync(connectionString);
        for (var i = 0; i < carteiras.Count; i++)
        {
            await using var comando = new NpgsqlCommand(
                "SELECT codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria FROM carteiras WHERE id = @id", conexao);
            comando.Parameters.AddWithValue("id", carteiras[i]);
            await using var leitor = await comando.ExecuteReaderAsync(Ct);
            Assert.True(await leitor.ReadAsync(Ct));

            var numero = $"{i + 1:D8}";
            Assert.Equal(Banco.CodigoOneBank, leitor.GetString(0));
            Assert.Equal(ContaBancaria.AgenciaPadrao, leitor.GetString(1));
            Assert.Equal(numero, leitor.GetString(2));
            Assert.Equal(ContaBancaria.CalcularDigito(ContaBancaria.AgenciaPadrao, numero), leitor.GetString(3));
            Assert.Equal("conta_corrente", leitor.GetString(4));
        }

        Assert.Equal(carteiras.Count + 1, await ProximoNumeroAsync(conexao));
    }

    [Fact]
    public async Task BaseVazia_MigraSemErro_ESequenceComecaEm1()
    {
        var connectionString = await PostgresCompartilhado.CriarConnectionStringAsync();

        await MigrarAsync(connectionString, null);

        await using var conexao = await AbrirAsync(connectionString);
        Assert.Equal(1, await ProximoNumeroAsync(conexao));
    }

    [Fact]
    public async Task DownEUp_Funcionam()
    {
        var connectionString = await PostgresCompartilhado.CriarConnectionStringAsync();
        await MigrarAsync(connectionString, MigracaoAnterior);
        var carteiras = await PopularVersaoAnteriorAsync(connectionString);

        await MigrarAsync(connectionString, MigracaoDadosBancarios);
        await MigrarAsync(connectionString, MigracaoAnterior);
        await MigrarAsync(connectionString, null);

        await using var conexao = await AbrirAsync(connectionString);
        await using var comando = new NpgsqlCommand("SELECT count(*) FROM carteiras WHERE numero_conta IS NOT NULL", conexao);
        Assert.Equal((long)carteiras.Count, (long)(await comando.ExecuteScalarAsync(Ct))!);
        Assert.Equal(carteiras.Count + 1, await ProximoNumeroAsync(conexao));
    }

    private static async Task MigrarAsync(string connectionString, string? alvo)
    {
        await using var contexto = new BankDbContext(new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options);
        await contexto.GetService<IMigrator>().MigrateAsync(alvo, Ct);
    }

    private static async Task<NpgsqlConnection> AbrirAsync(string connectionString)
    {
        var conexao = new NpgsqlConnection(connectionString);
        await conexao.OpenAsync(Ct);
        return conexao;
    }

    /// <summary>Três titulares (com saldos diferentes) e uma transferência com seus lançamentos, no esquema anterior.</summary>
    private static async Task<List<Guid>> PopularVersaoAnteriorAsync(string connectionString)
    {
        var agora = DateTimeOffset.UtcNow;
        var carteiras = Enumerable.Range(0, 3).Select(i => Guid.CreateVersion7(agora.AddSeconds(i))).ToList();
        var saldos = new[] { 10.00m, 0.00m, 123.45m };

        await using var conexao = await AbrirAsync(connectionString);
        for (var i = 0; i < carteiras.Count; i++)
        {
            var usuarioId = Guid.CreateVersion7(agora.AddSeconds(i));
            await ExecutarAsync(
                conexao,
                "INSERT INTO usuarios (id, nome_completo, email, documento, tipo_conta, senha_hash, criado_em) VALUES (@id, 'Conta Antiga', @email, @documento, 'PessoaFisica', 'hash', @agora)",
                ("id", usuarioId), ("email", Dados.Email()), ("documento", Dados.Cpf()), ("agora", agora));
            await ExecutarAsync(
                conexao,
                "INSERT INTO carteiras (id, usuario_id, saldo, moeda, atualizado_em) VALUES (@id, @usuario, @saldo, 'BRL', @agora)",
                ("id", carteiras[i]), ("usuario", usuarioId), ("saldo", saldos[i]), ("agora", agora));
        }

        var transferenciaId = Guid.CreateVersion7(agora);
        await ExecutarAsync(
            conexao,
            "INSERT INTO transferencias (id, chave_idempotencia, carteira_origem_id, carteira_destino_id, valor, descricao, status, criado_em) VALUES (@id, @chave, @origem, @destino, 5.00, 'Antiga', 'Concluida', @agora)",
            ("id", transferenciaId), ("chave", Guid.NewGuid()), ("origem", carteiras[0]), ("destino", carteiras[2]), ("agora", agora));
        await ExecutarAsync(
            conexao,
            "INSERT INTO lancamentos (id, carteira_id, tipo, direcao, valor, transferencia_id, descricao, criado_em) VALUES (@saida, @origem, 'Transferencia', 'Saida', 5.00, @transferencia, 'Antiga', @agora), (@entrada, @destino, 'Transferencia', 'Entrada', 5.00, @transferencia, 'Antiga', @agora)",
            ("saida", Guid.CreateVersion7(agora)), ("entrada", Guid.CreateVersion7(agora)), ("origem", carteiras[0]), ("destino", carteiras[2]), ("transferencia", transferenciaId), ("agora", agora));

        return carteiras;
    }

    private static async Task<string> FotografarAsync(string connectionString)
    {
        await using var conexao = await AbrirAsync(connectionString);
        var partes = new List<string>();
        foreach (var consulta in new[] { ConsultaCarteiras, ConsultaTransferencias, ConsultaLancamentos })
        {
            await using var comando = new NpgsqlCommand(consulta, conexao);
            await using var leitor = await comando.ExecuteReaderAsync(Ct);
            while (await leitor.ReadAsync(Ct))
            {
                partes.Add(leitor.GetString(0));
            }
        }

        return string.Join('\n', partes);
    }

    private static async Task<long> ProximoNumeroAsync(NpgsqlConnection conexao)
    {
        await using var comando = new NpgsqlCommand($"SELECT nextval('{BankDbContext.SequenceNumeroConta}')", conexao);
        return (long)(await comando.ExecuteScalarAsync(Ct))!;
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql, params (string Nome, object Valor)[] parametros)
    {
        await using var comando = new NpgsqlCommand(sql, conexao);
        foreach (var (nome, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nome, valor);
        }

        await comando.ExecuteNonQueryAsync(Ct);
    }
}
