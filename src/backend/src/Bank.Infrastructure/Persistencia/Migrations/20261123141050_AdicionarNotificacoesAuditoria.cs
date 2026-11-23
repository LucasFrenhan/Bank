using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarNotificacoesAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificacoes_auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacoes_auditoria", x => x.id);
                    table.CheckConstraint("ck_notificacoes_auditoria_canal", "canal IN ('simulado')");
                    table.CheckConstraint("ck_notificacoes_auditoria_tipo", "tipo IN ('comprovante_envio', 'aviso_credito')");
                    table.ForeignKey(
                        name: "fk_notificacoes_auditoria_usuarios_usuario_destino_id",
                        column: x => x.usuario_destino_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificacoes_auditoria_usuario_destino_id",
                table: "notificacoes_auditoria",
                column: "usuario_destino_id");

            migrationBuilder.CreateIndex(
                name: "ux_notificacoes_auditoria_evento_tipo",
                table: "notificacoes_auditoria",
                columns: new[] { "evento_id", "tipo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacoes_auditoria");
        }
    }
}
