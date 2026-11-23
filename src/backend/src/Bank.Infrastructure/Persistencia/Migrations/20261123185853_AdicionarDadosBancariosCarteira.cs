using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bank.Infrastructure.Persistencia.Migrations
{
    /// <summary>
    /// Dados bancários da carteira (specs/012-dados-bancarios-carteira, research R7). Ordem do
    /// <c>Up</c>: bancos; sequence; function do dígito; colunas anuláveis; backfill das carteiras
    /// existentes; colunas obrigatórias e restrições; contas de outros bancos.
    /// </summary>
    public partial class AdicionarDadosBancariosCarteira : Migration
    {
        private static readonly string[] ColunasConta = ["codigo_banco", "agencia", "numero_conta"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Os cinco bancos (FR-001).
            migrationBuilder.CreateTable(
                name: "bancos",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "char(3)", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bancos", x => x.codigo);
                });

            migrationBuilder.InsertData(
                table: "bancos",
                columns: new[] { "codigo", "nome" },
                values: new object[,]
                {
                    { "001", "One Bank" },
                    { "002", "Banco Aurora" },
                    { "003", "Banco Horizonte" },
                    { "004", "Banco Atlântico" },
                    { "005", "Banco Cerrado" }
                });

            // 2. Contador único dos números de conta do banco 001 (FR-008, R2).
            migrationBuilder.CreateSequence(
                name: "seq_numero_conta_one_bank",
                maxValue: 99999999L);

            // 3. Dígito verificador (Luhn, R1). SQL em arquivo .sql embutido — constituição v1.1.0.
            migrationBuilder.Sql(SqlEmbutido.Ler("Procedures/CalcularDigitoConta.sql"));

            // 4. Colunas anuláveis, para preencher as carteiras existentes antes de exigi-las (FR-007).
            AdicionarColunasDaCarteira(migrationBuilder);

            // 5. Backfill: numera as carteiras existentes em ordem de criação e ajusta a sequence.
            migrationBuilder.Sql(SqlEmbutido.Ler("Migracoes/PreencherDadosBancariosCarteiras.sql"));

            // 6. Colunas obrigatórias e restrições (FR-002, FR-003, FR-004, FR-014).
            TornarColunasDaCarteiraObrigatorias(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "ux_carteiras_conta_bancaria",
                table: "carteiras",
                columns: ColunasConta,
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_carteiras_codigo_banco",
                table: "carteiras",
                sql: "codigo_banco = '001'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_carteiras_digito_conta",
                table: "carteiras",
                sql: "digito_conta = calcular_digito_conta(agencia, numero_conta)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_carteiras_formato_conta",
                table: "carteiras",
                sql: "agencia ~ '^[0-9]{4}$' AND numero_conta ~ '^[0-9]{8}$' AND digito_conta ~ '^[0-9]$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_carteiras_tipo_conta_bancaria",
                table: "carteiras",
                sql: "tipo_conta_bancaria IN ('conta_corrente', 'conta_poupanca')");

            migrationBuilder.AddForeignKey(
                name: "fk_carteiras_bancos_codigo_banco",
                table: "carteiras",
                column: "codigo_banco",
                principalTable: "bancos",
                principalColumn: "codigo",
                onDelete: ReferentialAction.Restrict);

            // 7. Contas de outros bancos, só para registro (FR-011, R6).
            migrationBuilder.CreateTable(
                name: "contas_externas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_banco = table.Column<string>(type: "char(3)", nullable: false),
                    agencia = table.Column<string>(type: "char(4)", nullable: false),
                    numero_conta = table.Column<string>(type: "char(8)", nullable: false),
                    digito_conta = table.Column<string>(type: "char(1)", nullable: false),
                    tipo_conta_bancaria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titular_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    titular_documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contas_externas", x => x.id);
                    table.CheckConstraint("ck_contas_externas_codigo_banco", "codigo_banco <> '001'");
                    table.CheckConstraint("ck_contas_externas_digito_conta", "digito_conta = calcular_digito_conta(agencia, numero_conta)");
                    table.CheckConstraint("ck_contas_externas_formato_conta", "agencia ~ '^[0-9]{4}$' AND numero_conta ~ '^[0-9]{8}$' AND digito_conta ~ '^[0-9]$'");
                    table.CheckConstraint("ck_contas_externas_tipo_conta_bancaria", "tipo_conta_bancaria IN ('conta_corrente', 'conta_poupanca')");
                    table.CheckConstraint("ck_contas_externas_titular_documento", "titular_documento ~ '^([0-9]{11}|[0-9]{14})$'");
                    table.ForeignKey(
                        name: "fk_contas_externas_bancos_codigo_banco",
                        column: x => x.codigo_banco,
                        principalTable: "bancos",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_contas_externas_conta_bancaria",
                table: "contas_externas",
                columns: ColunasConta,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contas_externas");

            migrationBuilder.DropForeignKey(
                name: "fk_carteiras_bancos_codigo_banco",
                table: "carteiras");

            migrationBuilder.DropIndex(
                name: "ux_carteiras_conta_bancaria",
                table: "carteiras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_carteiras_codigo_banco",
                table: "carteiras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_carteiras_digito_conta",
                table: "carteiras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_carteiras_formato_conta",
                table: "carteiras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_carteiras_tipo_conta_bancaria",
                table: "carteiras");

            // A numeração atribuída é descartada.
            foreach (var (nome, _, _) in Colunas)
            {
                migrationBuilder.DropColumn(
                    name: nome,
                    table: "carteiras");
            }

            migrationBuilder.Sql(SqlEmbutido.Ler("Procedures/RemoverCalcularDigitoConta.sql"));

            migrationBuilder.DropSequence(
                name: "seq_numero_conta_one_bank");

            migrationBuilder.DropTable(
                name: "bancos");
        }

        private static readonly (string Nome, string Tipo, int? Tamanho)[] Colunas =
        [
            ("codigo_banco", "char(3)", null),
            ("agencia", "char(4)", null),
            ("numero_conta", "char(8)", null),
            ("digito_conta", "char(1)", null),
            ("tipo_conta_bancaria", "character varying(20)", 20),
        ];

        private static void AdicionarColunasDaCarteira(MigrationBuilder migrationBuilder)
        {
            foreach (var (nome, tipo, tamanho) in Colunas)
            {
                migrationBuilder.AddColumn<string>(
                    name: nome,
                    table: "carteiras",
                    type: tipo,
                    maxLength: tamanho,
                    nullable: true);
            }
        }

        private static void TornarColunasDaCarteiraObrigatorias(MigrationBuilder migrationBuilder)
        {
            foreach (var (nome, tipo, tamanho) in Colunas)
            {
                migrationBuilder.AlterColumn<string>(
                    name: nome,
                    table: "carteiras",
                    type: tipo,
                    maxLength: tamanho,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: tipo,
                    oldMaxLength: tamanho,
                    oldNullable: true);
            }
        }
    }
}
