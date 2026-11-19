using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarIndiceTransferenciasOrigemCriadoEm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_transferencias_origem_criado_em",
                table: "transferencias",
                columns: new[] { "carteira_origem_id", "criado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transferencias_origem_criado_em",
                table: "transferencias");
        }
    }
}
