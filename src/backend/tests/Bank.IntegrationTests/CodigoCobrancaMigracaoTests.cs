using Bank.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bank.IntegrationTests;

/// <summary>
/// Código das cobranças existentes pela migration AdicionarCodigoCobranca (specs/015-frontend-cartao-credito, FR-009a,
/// SC-003a; research R2). SQL só de teste.
/// </summary>
public class CodigoCobrancaMigracaoTests
{
    private const string Anterior = "AdicionarCartoesCobrancasERecebiveis";

    private const string Fotografia =
        "SELECT string_agg((to_jsonb(c) - 'codigo')::text, ';' ORDER BY id) AS \"Value\" FROM cobrancas c";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CobrancasExistentes_RecebemCodigoUnico_SemMudarOutrosDados()
    {
        await using var contexto = await CriarAsync();
        var migrator = contexto.GetService<IMigrator>();
        await migrator.MigrateAsync(Anterior, Ct);
        await PopularAsync(contexto);
        var antes = await contexto.Database.SqlQueryRaw<string>(Fotografia).SingleAsync(Ct);

        await migrator.MigrateAsync(null, Ct);

        Assert.Equal(antes, await contexto.Database.SqlQueryRaw<string>(Fotografia).SingleAsync(Ct));
        var codigos = await contexto.Cobrancas.Select(c => c.Codigo).ToListAsync(Ct);
        Assert.Equal(1_200, codigos.Count);
        Assert.Equal(codigos.Count, codigos.Select(c => c.Valor).Distinct(StringComparer.Ordinal).Count());
        Assert.All(codigos, c => Assert.Matches("^[23456789ABCDEFGHJKMNPQRSTVWXYZ]{8}$", c.Valor));
        Assert.Equal(30, codigos.SelectMany(c => c.Valor).Distinct().Count());
    }

    [Fact]
    public async Task DownEUp_EBaseVazia()
    {
        await using var contexto = await CriarAsync();
        var migrator = contexto.GetService<IMigrator>();
        await migrator.MigrateAsync(null, Ct);
        await migrator.MigrateAsync(Anterior, Ct);
        await PopularAsync(contexto);

        await migrator.MigrateAsync(null, Ct);

        Assert.Equal(1_200, await contexto.Cobrancas.CountAsync(Ct));
    }

    private static async Task<BankDbContext> CriarAsync() =>
        new(new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(await PostgresCompartilhado.CriarConnectionStringAsync()).Options);

    private static Task<int> PopularAsync(BankDbContext contexto) => contexto.Database.ExecuteSqlRawAsync(
        """
        INSERT INTO usuarios (id, nome_completo, email, documento, tipo_conta, senha_hash, criado_em)
        VALUES ('019a8f77-0000-7000-8000-000000000001', 'Loja', 'loja@exemplo.com.br', '12ABC34501DE35', 'PessoaJuridica', 'x', now());
        INSERT INTO carteiras (id, usuario_id, saldo, moeda, atualizado_em, codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria)
        VALUES ('019a8f77-0000-7000-8000-000000000002', '019a8f77-0000-7000-8000-000000000001', 0, 'BRL', now(), '001', '0001', '00000001', '6', 'conta_corrente');
        INSERT INTO cobrancas (id, lojista_usuario_id, carteira_destino_id, valor, descricao, status, pagador_usuario_id, codigo_bandeira, ultimos_digitos_cartao, codigo_autorizacao, paga_em, criado_em, cancelada_em)
        SELECT uuidv7(), '019a8f77-0000-7000-8000-000000000001', '019a8f77-0000-7000-8000-000000000002', n, 'Antiga ' || n, s.status,
               CASE WHEN s.status = 'Paga' THEN '019a8f77-0000-7000-8000-000000000001'::uuid END,
               CASE WHEN s.status = 'Paga' THEN '001' END, CASE WHEN s.status = 'Paga' THEN '7899' END,
               CASE WHEN s.status = 'Paga' THEN 'ABC123' END, CASE WHEN s.status = 'Paga' THEN now() END,
               now(), CASE WHEN s.status = 'Cancelada' THEN now() END
        FROM generate_series(1, 400) AS n CROSS JOIN (VALUES ('Pendente'), ('Paga'), ('Cancelada')) AS s (status);
        """, Ct);
}
