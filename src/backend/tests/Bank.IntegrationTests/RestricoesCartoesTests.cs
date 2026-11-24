using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;
using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Restrições do schema (specs/014-cartao-credito-cobrancas, data-model; FR-003, FR-017, FR-020, FR-023, FR-026). SQL só de
/// teste.
/// </summary>
public class RestricoesCartoesTests(BankApiFactory factory) : IClassFixture<BankApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> Invalidos() => new()
    {
        { "UPDATE cartoes SET limite_utilizado = limite_total + 0.01", "ck_cartoes_limite" },
        { "UPDATE cartoes SET limite_utilizado = -0.01", "ck_cartoes_limite" },
        { "UPDATE cartoes SET ultimos_digitos = '12a4'", "ck_cartoes_ultimos_digitos" },
        { "UPDATE cartoes SET validade_mes = 13", "ck_cartoes_validade" },
        { "UPDATE cartoes SET variacao = 5", "ck_cartoes_variacao" },
        { "UPDATE cartoes SET codigo_bandeira = '003'", "fk_cartoes_bandeiras" },
        { "UPDATE cobrancas SET valor = 0", "ck_cobrancas_valor_positivo" },
        { "UPDATE cobrancas SET status = 'Outro', pagador_usuario_id = NULL, codigo_bandeira = NULL, ultimos_digitos_cartao = NULL, codigo_autorizacao = NULL, paga_em = NULL", "ck_cobrancas_status" },
        { "UPDATE cobrancas SET status = 'Pendente'", "ck_cobrancas_paga_coerente" },
        { "UPDATE cobrancas SET codigo_autorizacao = NULL", "ck_cobrancas_paga_coerente" },
        { "UPDATE cobrancas SET cancelada_em = now()", "ck_cobrancas_cancelada_coerente" },
        { "UPDATE cobrancas SET ultima_recusa_motivo = 'FaturaVencida'", "ck_cobrancas_recusa_coerente" },
        { "UPDATE cobrancas SET ultima_recusa_motivo = 'Outro', ultima_recusa_em = now()", "ck_cobrancas_recusa_motivo" },
        { "UPDATE faturas SET vencimento = fechamento", "ck_faturas_datas" },
        { "UPDATE faturas SET total = -1", "ck_faturas_total" },
        { "UPDATE faturas SET paga_em = now()", "ck_faturas_pagamento_coerente" },
        { "UPDATE faturas SET paga_em = now(), liberada_em = now(), taxa_juros_mensal = 15, dias_atraso = 0, juros = 0, valor_pago = total + 1, chave_idempotencia = gen_random_uuid()", "ck_faturas_pagamento_coerente" },
        { "UPDATE recebiveis SET valor_liquido = valor_bruto - 1", "ck_recebiveis_valores" },
        { "UPDATE recebiveis SET situacao = 'Liquidado'", "ck_recebiveis_situacao_coerente" },
        { "UPDATE recebiveis SET situacao = 'Antecipado', recebido_em = now()", "ck_recebiveis_situacao_coerente" },
        { "UPDATE recebiveis SET taxa = 1, valor_liquido = valor_bruto - 1", "ck_recebiveis_situacao_coerente" },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task DadoInvalido_Recusado(string comando, string restricao)
    {
        await PrepararAsync();
        await using var contexto = factory.CriarContexto();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.ExecuteSqlRawAsync(comando, Ct));

        Assert.Equal(restricao, erro.ConstraintName);
    }

    public static TheoryData<string> Imutaveis() => new()
    {
        "UPDATE autorizacoes_cartao SET valor = valor + 1",
        "DELETE FROM autorizacoes_cartao",
        "UPDATE compras_cartao SET valor = valor + 1",
        "DELETE FROM compras_cartao",
        "UPDATE lancamentos_fatura SET valor = valor + 1",
        "DELETE FROM lancamentos_fatura",
    };

    [Theory]
    [MemberData(nameof(Imutaveis))]
    public async Task RegistroFinanceiro_EhImutavel(string comando)
    {
        await PrepararAsync();
        await using var contexto = factory.CriarContexto();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.ExecuteSqlRawAsync(comando, Ct));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, erro.SqlState);
    }

    public static TheoryData<string, string> Duplicados() => new()
    {
        { "INSERT INTO cartoes SELECT gen_random_uuid(), usuario_id, codigo_bandeira, nome_impresso, ultimos_digitos, validade_ano, validade_mes, variacao, 'outra', limite_total, 0, criado_em FROM cartoes LIMIT 1", "ux_cartoes_usuario_id" },
        { "INSERT INTO lancamentos_fatura SELECT gen_random_uuid(), fatura_id, compra_id, estabelecimento, descricao, valor, data_compra, lancado_em FROM lancamentos_fatura LIMIT 1", "ux_lancamentos_fatura_compra_id" },
        { "INSERT INTO faturas (id, cartao_id, competencia, inicio, fechamento, vencimento, total) SELECT gen_random_uuid(), cartao_id, competencia, inicio, fechamento, vencimento, 0 FROM faturas LIMIT 1", "ux_faturas_cartao_competencia" },
        { "INSERT INTO recebiveis SELECT gen_random_uuid(), cobranca_id, lojista_usuario_id, carteira_id, valor_bruto, taxa, valor_liquido, data_prevista, situacao, criado_em, recebido_em, chave_idempotencia FROM recebiveis LIMIT 1", "ux_recebiveis_cobranca_id" },
        { "INSERT INTO autorizacoes_cartao SELECT gen_random_uuid(), cobranca_id, pagador_usuario_id, cartao_id, codigo_bandeira, ultimos_digitos, valor, resultado, motivo, codigo_autorizacao, gen_random_uuid(), criado_em FROM autorizacoes_cartao LIMIT 1", "ux_autorizacoes_cartao_cobranca_aprovada" },
        { "INSERT INTO lancamentos (id, carteira_id, tipo, direcao, valor, recebivel_id, criado_em) SELECT gen_random_uuid(), carteira_id, 'LiquidacaoRecebivel', 'Entrada', 1, id, now() FROM recebiveis CROSS JOIN generate_series(1, 2)", "ux_lancamentos_recebivel_id" },
        { "INSERT INTO lancamentos (id, carteira_id, tipo, direcao, valor, criado_em) SELECT gen_random_uuid(), carteira_id, 'LiquidacaoRecebivel', 'Entrada', 1, now() FROM recebiveis LIMIT 1", "ck_lancamentos_origem_coerente" },
        { "INSERT INTO lancamentos (id, carteira_id, tipo, direcao, valor, recebivel_id, criado_em) SELECT gen_random_uuid(), carteira_id, 'PagamentoFatura', 'Saida', 1, id, now() FROM recebiveis LIMIT 1", "ck_lancamentos_origem_coerente" },
    };

    [Theory]
    [MemberData(nameof(Duplicados))]
    public async Task DuplicidadeOuOrigemIncoerente_Recusada(string comando, string restricao)
    {
        await PrepararAsync();
        await using var contexto = factory.CriarContexto();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.ExecuteSqlRawAsync(comando, Ct));

        Assert.Equal(restricao, erro.ConstraintName);
    }

    [Fact]
    public async Task DownEUp_PreservamOsDadosAnteriores()
    {
        var connectionString = await PostgresCompartilhado.CriarConnectionStringAsync();
        await using var contexto = new BankDbContext(new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options);
        var migrator = contexto.GetService<IMigrator>();
        await migrator.MigrateAsync("AdicionarDadosBancariosCarteira", Ct);
        await contexto.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO usuarios (id, nome_completo, email, documento, tipo_conta, senha_hash, criado_em)
            VALUES ('019a8f77-0000-7000-8000-000000000001', 'Antigo', 'antigo@exemplo.com.br', '52998224725', 'PessoaFisica', 'x', now());
            INSERT INTO carteiras (id, usuario_id, saldo, moeda, atualizado_em, codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria)
            VALUES ('019a8f77-0000-7000-8000-000000000002', '019a8f77-0000-7000-8000-000000000001', 10, 'BRL', now(), '001', '0001', '00000001', '6', 'conta_corrente');
            INSERT INTO lancamentos (id, carteira_id, tipo, direcao, valor, descricao, criado_em)
            VALUES ('019a8f77-0000-7000-8000-000000000003', '019a8f77-0000-7000-8000-000000000002', 'Abertura', 'Entrada', 10, 'Saldo inicial de demonstração', now());
            """, Ct);

        await migrator.MigrateAsync("AdicionarCartoesCobrancasERecebiveis", Ct);
        Assert.Equal(["001", "002"], await contexto.Bandeiras.OrderBy(b => b.Codigo).Select(b => b.Codigo).ToListAsync(Ct));
        await migrator.MigrateAsync("AdicionarDadosBancariosCarteira", Ct);
        await migrator.MigrateAsync("AdicionarCartoesCobrancasERecebiveis", Ct);

        Assert.Equal(10m, await contexto.Carteiras.Select(c => c.Saldo).SingleAsync(Ct));
        Assert.Equal(TipoLancamento.Abertura, await contexto.Lancamentos.Select(l => l.Tipo).SingleAsync(Ct));
    }

    /// <summary>Um pagamento completo, com a compra lançada.</summary>
    private async Task PrepararAsync()
    {
        using var client = factory.CreateClient();
        await using var contexto = factory.CriarContexto();
        if (await contexto.LancamentosFatura.AnyAsync(Ct))
        {
            return;
        }

        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        var agora = DateTimeOffset.UtcNow;
        var hoje = DateOnly.FromDateTime(agora.UtcDateTime);

        var cartao = Cartao.Emitir(CartaoId.Novo(agora), Bank.Domain.Usuarios.UsuarioId.De(pf.UsuarioId), Bandeira.Visa, "Lucas Silva", ValidadeCartao.DaEmissao(hoje), 0, "7899", "impressao", 5000m, agora);
        var cobranca = Cobranca.Emitir(Bank.Domain.Usuarios.UsuarioId.De(pj.UsuarioId), Bank.Domain.Carteiras.CarteiraId.De(pj.CarteiraId), ValorTransferencia.Criar(100m).Valor, "Café", agora);
        var fatura = Fatura.Abrir(cartao.Id, CompetenciaFatura.De(hoje), agora);
        var execucao = PagamentoCobranca.Executar(cobranca, cartao, fatura, "Loja", hoje, Guid.NewGuid(), agora).Valor;
        contexto.AddRange(cartao, cobranca, fatura, execucao.Autorizacao, execucao.Compra, execucao.Recebivel, fatura.Lancar(execucao.Compra, agora));
        await contexto.SaveChangesAsync(Ct);
        Assert.Equal(SituacaoRecebivel.Pendente, execucao.Recebivel.Situacao);
    }
}
