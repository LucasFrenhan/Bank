using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using static Bank.IntegrationTests.ApiCartoes;
using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>10.000 compras em 1.000 cartões em até 5 minutos (specs/014-cartao-credito-cobrancas, SC-005, SC-007). Dados por SQL de teste.</summary>
public class VolumeProcessamentoTests(CartoesApiFactory factory) : IClassFixture<CartoesApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DezMilCompras_EmAteCincoMinutos()
    {
        using var client = factory.CreateClient();
        var pj = await CriarPjAsync(client);
        await using var contexto = factory.CriarContexto();
        await contexto.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO usuarios (id, nome_completo, email, documento, tipo_conta, senha_hash, criado_em)
            SELECT uuidv7(), 'PF ' || i, 'volume' || i || '@exemplo.com.br', lpad(i::text, 11, '0'), 'PessoaFisica', 'x', now() FROM generate_series(1, 1000) i;

            INSERT INTO cartoes (id, usuario_id, codigo_bandeira, nome_impresso, ultimos_digitos, validade_ano, validade_mes, variacao, impressao_digital, limite_total, limite_utilizado, criado_em)
            SELECT uuidv7(), id, '001', upper(nome_completo), '0000', 2040, 1, 0, documento, 5000, 10, now() FROM usuarios WHERE email LIKE 'volume%';

            INSERT INTO faturas (id, cartao_id, competencia, inicio, fechamento, vencimento, total)
            SELECT uuidv7(), id, '2027-03-01', '2027-02-08', '2027-03-07', '2027-03-10', 0 FROM cartoes;

            INSERT INTO cobrancas (id, lojista_usuario_id, carteira_destino_id, valor, descricao, status, pagador_usuario_id, codigo_bandeira, ultimos_digitos_cartao, codigo_autorizacao, paga_em, criado_em)
            SELECT uuidv7(), {0}, {1}, 1, 'Volume', 'Paga', c.usuario_id, '001', '0000', 'ABC123', '2027-03-01T12:00:00Z', '2027-03-01T12:00:00Z' FROM cartoes c CROSS JOIN generate_series(1, 10);

            INSERT INTO autorizacoes_cartao (id, cobranca_id, pagador_usuario_id, cartao_id, codigo_bandeira, ultimos_digitos, valor, resultado, codigo_autorizacao, chave_idempotencia, criado_em)
            SELECT uuidv7(), b.id, b.pagador_usuario_id, c.id, '001', '0000', 1, 'Aprovada', 'ABC123', gen_random_uuid(), b.paga_em FROM cobrancas b JOIN cartoes c ON c.usuario_id = b.pagador_usuario_id;

            INSERT INTO compras_cartao (id, cartao_id, cobranca_id, autorizacao_id, fatura_id, estabelecimento, descricao, valor, data_compra, autorizada_em)
            SELECT uuidv7(), a.cartao_id, a.cobranca_id, a.id, f.id, 'Loja', 'Volume', 1, '2027-03-01', a.criado_em FROM autorizacoes_cartao a JOIN faturas f ON f.cartao_id = a.cartao_id;
            """, [pj.UsuarioId, pj.CarteiraId], Ct);

        factory.Relogio.Agora = Brasilia(2027, 3, 8, 0, 0, 0);
        var inicio = Stopwatch.GetTimestamp();
        var resumo = await factory.ProcessarAsync();
        var duracao = Stopwatch.GetElapsedTime(inicio);

        Assert.Equal((10_000, 1_000, 0), (resumo.ComprasLancadas, resumo.FaturasLiberadas, resumo.Falhas));
        Assert.True(duracao < TimeSpan.FromMinutes(5), $"Levou {duracao}.");
        Assert.Equal(10_000, await contexto.LancamentosFatura.CountAsync(Ct));
        Assert.True(await contexto.Faturas.AllAsync(f => f.Total == 10m && f.LiberadaEm != null, Ct));
        Assert.Equal(0, (await factory.ProcessarAsync()).ComprasLancadas);
    }
}
