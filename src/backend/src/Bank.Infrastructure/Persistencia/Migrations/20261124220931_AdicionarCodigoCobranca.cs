using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <summary>
    /// Código curto da cobrança (specs/015-frontend-cartao-credito, research R2). Ordem do <c>Up</c>: coluna anulável;
    /// índice único; código das cobranças existentes; coluna obrigatória e restrição.
    /// </summary>
    public partial class AdicionarCodigoCobranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo",
                table: "cobrancas",
                type: "char(8)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_cobrancas_codigo",
                table: "cobrancas",
                column: "codigo",
                unique: true);

            migrationBuilder.Sql(SqlEmbutido.Ler("Migracoes/PreencherCodigosDasCobrancas.sql"));

            migrationBuilder.AlterColumn<string>(
                name: "codigo",
                table: "cobrancas",
                type: "char(8)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(8)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_cobrancas_codigo",
                table: "cobrancas",
                sql: "codigo ~ '^[2-9A-HJKMNP-TV-Z]{8}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cobrancas_codigo",
                table: "cobrancas");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cobrancas_codigo",
                table: "cobrancas");

            migrationBuilder.DropColumn(
                name: "codigo",
                table: "cobrancas");
        }
    }
}
