using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <summary>
    /// Cartões, cobranças, faturas e recebíveis (specs/014-cartao-credito-cobrancas, data-model). Registros
    /// financeiros novos são imutáveis (research R5); o razão ganha as origens fatura e recebível (R14). O
    /// <c>Down</c> só funciona enquanto não houver lançamentos desses tipos.
    /// </summary>
    public partial class AdicionarCartoesCobrancasERecebiveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacoes_auditoria_tipo",
                table: "notificacoes_auditoria");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lancamentos_tipo",
                table: "lancamentos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lancamentos_transferencia_coerente",
                table: "lancamentos");

            migrationBuilder.AddColumn<Guid>(
                name: "fatura_id",
                table: "lancamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recebivel_id",
                table: "lancamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bandeiras",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "char(3)", nullable: false),
                    nome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bandeiras", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "cartoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_bandeira = table.Column<string>(type: "char(3)", nullable: false),
                    nome_impresso = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ultimos_digitos = table.Column<string>(type: "char(4)", nullable: false),
                    validade_ano = table.Column<int>(type: "integer", nullable: false),
                    validade_mes = table.Column<int>(type: "integer", nullable: false),
                    variacao = table.Column<int>(type: "integer", nullable: false),
                    impressao_digital = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    limite_total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    limite_utilizado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cartoes", x => x.id);
                    table.CheckConstraint("ck_cartoes_limite", "limite_total > 0 AND limite_utilizado BETWEEN 0 AND limite_total");
                    table.CheckConstraint("ck_cartoes_ultimos_digitos", "ultimos_digitos ~ '^[0-9]{4}$'");
                    table.CheckConstraint("ck_cartoes_validade", "validade_mes BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_cartoes_variacao", "variacao BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "fk_cartoes_bandeiras",
                        column: x => x.codigo_bandeira,
                        principalTable: "bandeiras",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cartoes_usuarios",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cobrancas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lojista_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carteira_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pagador_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo_bandeira = table.Column<string>(type: "char(3)", nullable: true),
                    ultimos_digitos_cartao = table.Column<string>(type: "char(4)", nullable: true),
                    codigo_autorizacao = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    paga_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultima_recusa_motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ultima_recusa_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cobrancas", x => x.id);
                    table.CheckConstraint("ck_cobrancas_cancelada_coerente", "(status = 'Cancelada') = (cancelada_em IS NOT NULL)");
                    table.CheckConstraint("ck_cobrancas_paga_coerente", "num_nonnulls(pagador_usuario_id, codigo_bandeira, ultimos_digitos_cartao, codigo_autorizacao, paga_em) = CASE status WHEN 'Paga' THEN 5 ELSE 0 END");
                    table.CheckConstraint("ck_cobrancas_recusa_coerente", "(ultima_recusa_motivo IS NULL) = (ultima_recusa_em IS NULL)");
                    table.CheckConstraint("ck_cobrancas_recusa_motivo", "ultima_recusa_motivo IN ('CartaoNaoAutorizado', 'FaturaVencida', 'LimiteInsuficiente')");
                    table.CheckConstraint("ck_cobrancas_status", "status IN ('Pendente', 'Paga', 'Cancelada')");
                    table.CheckConstraint("ck_cobrancas_ultimos_digitos", "ultimos_digitos_cartao ~ '^[0-9]{4}$'");
                    table.CheckConstraint("ck_cobrancas_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "fk_cobrancas_bandeiras",
                        column: x => x.codigo_bandeira,
                        principalTable: "bandeiras",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cobrancas_carteiras",
                        column: x => x.carteira_destino_id,
                        principalTable: "carteiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cobrancas_usuarios_lojista",
                        column: x => x.lojista_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cobrancas_usuarios_pagador",
                        column: x => x.pagador_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "faturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cartao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competencia = table.Column<DateOnly>(type: "date", nullable: false),
                    inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fechamento = table.Column<DateOnly>(type: "date", nullable: false),
                    vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    liberada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paga_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    taxa_juros_mensal = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    dias_atraso = table.Column<int>(type: "integer", nullable: true),
                    juros = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    valor_pago = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    chave_idempotencia = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faturas", x => x.id);
                    table.CheckConstraint("ck_faturas_datas", "inicio < fechamento AND fechamento < vencimento AND EXTRACT(DAY FROM competencia) = 1");
                    table.CheckConstraint("ck_faturas_pagamento_coerente", "num_nonnulls(paga_em, taxa_juros_mensal, dias_atraso, juros, valor_pago, chave_idempotencia) IN (0, 6) AND (paga_em IS NULL OR (liberada_em IS NOT NULL AND dias_atraso >= 0 AND juros >= 0 AND valor_pago = total + juros))");
                    table.CheckConstraint("ck_faturas_total", "total >= 0");
                    table.ForeignKey(
                        name: "fk_faturas_cartoes",
                        column: x => x.cartao_id,
                        principalTable: "cartoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "autorizacoes_cartao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cobranca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pagador_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cartao_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo_bandeira = table.Column<string>(type: "char(3)", nullable: true),
                    ultimos_digitos = table.Column<string>(type: "char(4)", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    resultado = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    motivo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_autorizacao = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    chave_idempotencia = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autorizacoes_cartao", x => x.id);
                    table.CheckConstraint("ck_autorizacoes_cartao_motivo", "motivo IN ('CartaoNaoAutorizado', 'FaturaVencida', 'LimiteInsuficiente')");
                    table.CheckConstraint("ck_autorizacoes_cartao_resultado", "resultado IN ('Aprovada', 'Recusada')");
                    table.CheckConstraint("ck_autorizacoes_cartao_resultado_coerente", "(resultado = 'Aprovada') = (codigo_autorizacao IS NOT NULL) AND (resultado = 'Aprovada') = (motivo IS NULL) AND (resultado = 'Recusada' OR (cartao_id IS NOT NULL AND codigo_bandeira IS NOT NULL))");
                    table.CheckConstraint("ck_autorizacoes_cartao_ultimos_digitos", "ultimos_digitos ~ '^[0-9]{4}$'");
                    table.CheckConstraint("ck_autorizacoes_cartao_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "fk_autorizacoes_cartao_bandeiras",
                        column: x => x.codigo_bandeira,
                        principalTable: "bandeiras",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_autorizacoes_cartao_cartoes",
                        column: x => x.cartao_id,
                        principalTable: "cartoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_autorizacoes_cartao_cobrancas",
                        column: x => x.cobranca_id,
                        principalTable: "cobrancas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_autorizacoes_cartao_usuarios",
                        column: x => x.pagador_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recebiveis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cobranca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lojista_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carteira_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_bruto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    taxa = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    valor_liquido = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    data_prevista = table.Column<DateOnly>(type: "date", nullable: false),
                    situacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recebido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    chave_idempotencia = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recebiveis", x => x.id);
                    table.CheckConstraint("ck_recebiveis_situacao", "situacao IN ('Pendente', 'Liquidado', 'Antecipado')");
                    table.CheckConstraint("ck_recebiveis_situacao_coerente", "(situacao = 'Pendente') = (recebido_em IS NULL) AND (situacao = 'Antecipado') = (chave_idempotencia IS NOT NULL) AND (situacao = 'Antecipado' OR taxa = 0)");
                    table.CheckConstraint("ck_recebiveis_valores", "valor_bruto > 0 AND taxa >= 0 AND valor_liquido > 0 AND valor_liquido = valor_bruto - taxa");
                    table.ForeignKey(
                        name: "fk_recebiveis_carteiras",
                        column: x => x.carteira_id,
                        principalTable: "carteiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recebiveis_cobrancas",
                        column: x => x.cobranca_id,
                        principalTable: "cobrancas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recebiveis_usuarios",
                        column: x => x.lojista_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compras_cartao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cartao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cobranca_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autorizacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fatura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estabelecimento = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    data_compra = table.Column<DateOnly>(type: "date", nullable: false),
                    autorizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_compras_cartao", x => x.id);
                    table.CheckConstraint("ck_compras_cartao_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "fk_compras_cartao_autorizacoes",
                        column: x => x.autorizacao_id,
                        principalTable: "autorizacoes_cartao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_compras_cartao_cartoes",
                        column: x => x.cartao_id,
                        principalTable: "cartoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_compras_cartao_cobrancas",
                        column: x => x.cobranca_id,
                        principalTable: "cobrancas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_compras_cartao_faturas",
                        column: x => x.fatura_id,
                        principalTable: "faturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lancamentos_fatura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fatura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estabelecimento = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    data_compra = table.Column<DateOnly>(type: "date", nullable: false),
                    lancado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lancamentos_fatura", x => x.id);
                    table.CheckConstraint("ck_lancamentos_fatura_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "fk_lancamentos_fatura_compras",
                        column: x => x.compra_id,
                        principalTable: "compras_cartao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lancamentos_fatura_faturas",
                        column: x => x.fatura_id,
                        principalTable: "faturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "bandeiras",
                columns: new[] { "codigo", "nome" },
                values: new object[,]
                {
                    { "001", "Visa" },
                    { "002", "Mastercard" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacoes_auditoria_tipo",
                table: "notificacoes_auditoria",
                sql: "tipo IN ('comprovante_envio', 'aviso_credito', 'comprovante_cobranca', 'cobranca_paga')");

            migrationBuilder.CreateIndex(
                name: "ux_lancamentos_fatura_id",
                table: "lancamentos",
                column: "fatura_id",
                unique: true,
                filter: "fatura_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_lancamentos_recebivel_id",
                table: "lancamentos",
                column: "recebivel_id",
                unique: true,
                filter: "recebivel_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lancamentos_origem_coerente",
                table: "lancamentos",
                sql: "(tipo = 'Transferencia') = (transferencia_id IS NOT NULL) AND (tipo = 'PagamentoFatura') = (fatura_id IS NOT NULL) AND (tipo IN ('LiquidacaoRecebivel', 'AntecipacaoRecebivel')) = (recebivel_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lancamentos_tipo",
                table: "lancamentos",
                sql: "tipo IN ('Abertura', 'Transferencia', 'PagamentoFatura', 'LiquidacaoRecebivel', 'AntecipacaoRecebivel')");

            migrationBuilder.CreateIndex(
                name: "ix_autorizacoes_cartao_cartao_id",
                table: "autorizacoes_cartao",
                column: "cartao_id");

            migrationBuilder.CreateIndex(
                name: "ix_autorizacoes_cartao_codigo_bandeira",
                table: "autorizacoes_cartao",
                column: "codigo_bandeira");

            migrationBuilder.CreateIndex(
                name: "ux_autorizacoes_cartao_chave",
                table: "autorizacoes_cartao",
                columns: new[] { "pagador_usuario_id", "chave_idempotencia" },
                unique: true,
                filter: "resultado = 'Aprovada'");

            migrationBuilder.CreateIndex(
                name: "ux_autorizacoes_cartao_cobranca_aprovada",
                table: "autorizacoes_cartao",
                column: "cobranca_id",
                unique: true,
                filter: "resultado = 'Aprovada'");

            migrationBuilder.CreateIndex(
                name: "ix_cartoes_codigo_bandeira",
                table: "cartoes",
                column: "codigo_bandeira");

            migrationBuilder.CreateIndex(
                name: "ux_cartoes_impressao_digital",
                table: "cartoes",
                column: "impressao_digital",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cartoes_usuario_id",
                table: "cartoes",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cobrancas_carteira_destino_id",
                table: "cobrancas",
                column: "carteira_destino_id");

            migrationBuilder.CreateIndex(
                name: "ix_cobrancas_codigo_bandeira",
                table: "cobrancas",
                column: "codigo_bandeira");

            migrationBuilder.CreateIndex(
                name: "ix_cobrancas_lojista",
                table: "cobrancas",
                columns: new[] { "lojista_usuario_id", "criado_em", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_cobrancas_pagador_usuario_id",
                table: "cobrancas",
                column: "pagador_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_compras_cartao_cartao_id",
                table: "compras_cartao",
                column: "cartao_id");

            migrationBuilder.CreateIndex(
                name: "ix_compras_cartao_fatura_id",
                table: "compras_cartao",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "ux_compras_cartao_autorizacao_id",
                table: "compras_cartao",
                column: "autorizacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_compras_cartao_cobranca_id",
                table: "compras_cartao",
                column: "cobranca_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faturas_em_aberto",
                table: "faturas",
                columns: new[] { "cartao_id", "vencimento" },
                filter: "liberada_em IS NOT NULL AND paga_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_faturas_nao_liberadas",
                table: "faturas",
                column: "fechamento",
                filter: "liberada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_faturas_cartao_competencia",
                table: "faturas",
                columns: new[] { "cartao_id", "competencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_faturas_chave",
                table: "faturas",
                columns: new[] { "cartao_id", "chave_idempotencia" },
                unique: true,
                filter: "chave_idempotencia IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_fatura_fatura_id",
                table: "lancamentos_fatura",
                column: "fatura_id");

            migrationBuilder.CreateIndex(
                name: "ux_lancamentos_fatura_compra_id",
                table: "lancamentos_fatura",
                column: "compra_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recebiveis_carteira_id",
                table: "recebiveis",
                column: "carteira_id");

            migrationBuilder.CreateIndex(
                name: "ix_recebiveis_devidos",
                table: "recebiveis",
                column: "data_prevista",
                filter: "situacao = 'Pendente'");

            migrationBuilder.CreateIndex(
                name: "ix_recebiveis_lojista",
                table: "recebiveis",
                columns: new[] { "lojista_usuario_id", "criado_em", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_recebiveis_chave",
                table: "recebiveis",
                columns: new[] { "lojista_usuario_id", "chave_idempotencia" },
                unique: true,
                filter: "chave_idempotencia IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_recebiveis_cobranca_id",
                table: "recebiveis",
                column: "cobranca_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_lancamentos_faturas",
                table: "lancamentos",
                column: "fatura_id",
                principalTable: "faturas",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lancamentos_recebiveis",
                table: "lancamentos",
                column: "recebivel_id",
                principalTable: "recebiveis",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(SqlEmbutido.Ler("Procedures/ImpedirAlteracaoDeRegistrosDeCartao.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlEmbutido.Ler("Procedures/RemoverImpedimentoDeRegistrosDeCartao.sql"));

            migrationBuilder.DropForeignKey(
                name: "fk_lancamentos_faturas",
                table: "lancamentos");

            migrationBuilder.DropForeignKey(
                name: "fk_lancamentos_recebiveis",
                table: "lancamentos");

            migrationBuilder.DropTable(
                name: "lancamentos_fatura");

            migrationBuilder.DropTable(
                name: "recebiveis");

            migrationBuilder.DropTable(
                name: "compras_cartao");

            migrationBuilder.DropTable(
                name: "autorizacoes_cartao");

            migrationBuilder.DropTable(
                name: "faturas");

            migrationBuilder.DropTable(
                name: "cobrancas");

            migrationBuilder.DropTable(
                name: "cartoes");

            migrationBuilder.DropTable(
                name: "bandeiras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacoes_auditoria_tipo",
                table: "notificacoes_auditoria");

            migrationBuilder.DropIndex(
                name: "ux_lancamentos_fatura_id",
                table: "lancamentos");

            migrationBuilder.DropIndex(
                name: "ux_lancamentos_recebivel_id",
                table: "lancamentos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lancamentos_origem_coerente",
                table: "lancamentos");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lancamentos_tipo",
                table: "lancamentos");

            migrationBuilder.DropColumn(
                name: "fatura_id",
                table: "lancamentos");

            migrationBuilder.DropColumn(
                name: "recebivel_id",
                table: "lancamentos");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacoes_auditoria_tipo",
                table: "notificacoes_auditoria",
                sql: "tipo IN ('comprovante_envio', 'aviso_credito')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lancamentos_tipo",
                table: "lancamentos",
                sql: "tipo IN ('Abertura', 'Transferencia')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lancamentos_transferencia_coerente",
                table: "lancamentos",
                sql: "(tipo = 'Transferencia') = (transferencia_id IS NOT NULL)");
        }
    }
}
