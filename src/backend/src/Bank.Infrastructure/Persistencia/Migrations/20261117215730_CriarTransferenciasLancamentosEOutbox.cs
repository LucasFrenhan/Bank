using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarTransferenciasLancamentosEOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_eventos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_eventos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transferencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave_idempotencia = table.Column<Guid>(type: "uuid", nullable: false),
                    carteira_origem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carteira_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transferencias", x => x.id);
                    table.CheckConstraint("ck_transferencias_origem_diferente_destino", "carteira_origem_id <> carteira_destino_id");
                    table.CheckConstraint("ck_transferencias_status", "status = 'Concluida'");
                    table.CheckConstraint("ck_transferencias_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "fk_transferencias_carteira_destino",
                        column: x => x.carteira_destino_id,
                        principalTable: "carteiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transferencias_carteira_origem",
                        column: x => x.carteira_origem_id,
                        principalTable: "carteiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lancamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    carteira_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direcao = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lancamentos", x => x.id);
                    table.CheckConstraint("ck_lancamentos_direcao", "direcao IN ('Entrada', 'Saida')");
                    table.CheckConstraint("ck_lancamentos_tipo", "tipo IN ('Abertura', 'Transferencia')");
                    table.CheckConstraint("ck_lancamentos_transferencia_coerente", "(tipo = 'Transferencia') = (transferencia_id IS NOT NULL)");
                    table.CheckConstraint("ck_lancamentos_valor_positivo", "valor > 0");
                    table.ForeignKey(
                        name: "fk_lancamentos_carteiras",
                        column: x => x.carteira_id,
                        principalTable: "carteiras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lancamentos_transferencias",
                        column: x => x.transferencia_id,
                        principalTable: "transferencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_extrato",
                table: "lancamentos",
                columns: new[] { "carteira_id", "criado_em", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_lancamentos_transferencia_id",
                table: "lancamentos",
                column: "transferencia_id");

            migrationBuilder.CreateIndex(
                name: "ux_lancamentos_abertura_por_carteira",
                table: "lancamentos",
                column: "carteira_id",
                unique: true,
                filter: "tipo = 'Abertura'");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_eventos_pendentes",
                table: "outbox_eventos",
                column: "ocorrido_em",
                filter: "publicado_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_carteira_destino_id",
                table: "transferencias",
                column: "carteira_destino_id");

            migrationBuilder.CreateIndex(
                name: "ux_transferencias_origem_chave",
                table: "transferencias",
                columns: new[] { "carteira_origem_id", "chave_idempotencia" },
                unique: true);

            // Imutabilidade de transferências e lançamentos (FR-008). SQL em arquivo .sql
            // embutido — constituição v1.1.0.
            migrationBuilder.Sql(SqlEmbutido.Ler("Procedures/ImpedirAlteracaoDeRegistrosImutaveis.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlEmbutido.Ler("Procedures/RemoverImpedimentoDeAlteracao.sql"));

            migrationBuilder.DropTable(
                name: "lancamentos");

            migrationBuilder.DropTable(
                name: "outbox_eventos");

            migrationBuilder.DropTable(
                name: "transferencias");
        }
    }
}
