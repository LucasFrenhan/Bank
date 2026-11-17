using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriarUsuariosECarteiras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_completo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    tipo_conta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    senha_hash = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                    table.CheckConstraint("ck_usuarios_tipo_conta", "tipo_conta IN ('PessoaFisica', 'PessoaJuridica')");
                });

            migrationBuilder.CreateTable(
                name: "carteiras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    moeda = table.Column<string>(type: "char(3)", nullable: false, defaultValueSql: "'BRL'"),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carteiras", x => x.id);
                    table.CheckConstraint("ck_carteiras_moeda", "moeda = 'BRL'");
                    table.CheckConstraint("ck_carteiras_saldo_nao_negativo", "saldo >= 0");
                    table.ForeignKey(
                        name: "fk_carteiras_usuarios",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_carteiras_usuario_id",
                table: "carteiras",
                column: "usuario_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_documento",
                table: "usuarios",
                column: "documento",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "carteiras");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
