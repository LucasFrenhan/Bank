using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSessoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sessoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultima_renovacao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revogada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_revogacao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessoes", x => x.id);
                    table.CheckConstraint("ck_sessoes_revogacao", "(revogada_em IS NULL) = (motivo_revogacao IS NULL)");
                    table.ForeignKey(
                        name: "fk_sessoes_usuarios",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credenciais_renovacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    emitida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rotacionada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    substituta_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credenciais_renovacao", x => x.id);
                    table.CheckConstraint("ck_credenciais_renovacao_hash", "octet_length(hash) = 32");
                    table.ForeignKey(
                        name: "fk_credenciais_renovacao_sessoes",
                        column: x => x.sessao_id,
                        principalTable: "sessoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_credenciais_renovacao_ativa",
                table: "credenciais_renovacao",
                column: "sessao_id",
                unique: true,
                filter: "rotacionada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_credenciais_renovacao_hash",
                table: "credenciais_renovacao",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessoes_expira_em",
                table: "sessoes",
                column: "expira_em");

            migrationBuilder.CreateIndex(
                name: "ix_sessoes_revogada_em",
                table: "sessoes",
                column: "revogada_em",
                filter: "revogada_em IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sessoes_usuario_id",
                table: "sessoes",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credenciais_renovacao");

            migrationBuilder.DropTable(
                name: "sessoes");
        }
    }
}
