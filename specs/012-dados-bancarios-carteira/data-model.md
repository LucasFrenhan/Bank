# Data Model: Dados bancários da carteira

**Feature**: [spec.md](spec.md) | **Decisões**: [research.md](research.md)

## Domain (`Bank.Domain/Bancos/`)

### `Banco` (catálogo fixo, R5)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `Codigo` | `string` | 3 dígitos; chave natural |
| `Nome` | `string` | até 60 caracteres |

Instâncias fixas (`Banco.Todos`): `001` "One Bank" (`Banco.OneBank`), `002` "Banco Aurora",
`003` "Banco Horizonte", `004` "Banco Atlântico", `005` "Banco Cerrado" (FR-001).

### `TipoContaBancaria` (enum)

`ContaCorrente = 1`, `ContaPoupanca = 2`. Persistido como `conta_corrente` / `conta_poupanca`;
exposto na API como `ContaCorrente` / `ContaPoupanca`.

### `ContaBancaria` (value object, R1/R3)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `CodigoBanco` | `string` | um dos cinco códigos |
| `Agencia` | `string` | exatamente 4 dígitos (`0001` nesta entrega) |
| `Numero` | `string` | exatamente 8 dígitos, `00000001`–`99999999` |
| `Digito` | `string` | 1 dígito = Luhn(`Agencia` + `Numero`) |
| `Tipo` | `TipoContaBancaria` | corrente ou poupança |

- `Nova(Banco, agencia, long numero, tipo)`: formata com zeros, valida e calcula o dígito;
  lança `ArgumentException`/`ArgumentOutOfRangeException` para formato inválido.
- `CalcularDigito(agencia, numero)`: Luhn sobre os 12 dígitos; pública para testes e para
  conferência com a function SQL.
- Sem identidade própria (value object, sem identificador tipado).

### `Carteira` (alterada)

Novas propriedades, todas obrigatórias, com `private set`: `CodigoBanco`, `Agencia`,
`NumeroConta`, `DigitoConta`, `TipoContaBancaria`; `ContaBancaria` (somente leitura, montada a
partir delas, ignorada pelo EF).

- `Criar(UsuarioId, ContaBancaria conta, decimal saldoInicial, DateTimeOffset agora)`: recusa
  conta cujo banco não seja o 001 (FR-014).
- Sem método para alterar dados bancários (não mudam depois da criação).

### `Usuario` (alterada)

`Criar(nome, email, documento, senhaHash, ContaBancaria conta, saldoInicial, agora)`: repassa a
conta para `Carteira.Criar`.

### `ContaExterna` (nova entidade, R6) e `ContaExternaId`

| Campo | Tipo | Regra |
| --- | --- | --- |
| `Id` | `ContaExternaId` | UUIDv7 |
| `CodigoBanco`, `Agencia`, `NumeroConta`, `DigitoConta`, `TipoContaBancaria` | como em `Carteira` | banco ≠ 001 (FR-014) |
| `TitularNome` | `string` | 1–150 caracteres |
| `TitularDocumento` | `string` | 11 (CPF) ou 14 (CNPJ) dígitos |
| `CriadoEm` | `DateTimeOffset` | |

Sem relação com `Usuario`/`Carteira`; sem senha nem saldo (FR-011). Gravada só pelo script.

## Application

- `IGeradorNumeroConta.ProximoAsync(CancellationToken) : Task<long>` (R2).
- `RegistrarUsuario`: obtém o número e monta `ContaBancaria.Nova(Banco.OneBank, "0001",
  numero, TipoContaBancaria.ContaCorrente)` antes de `Usuario.Criar` (FR-005/FR-006).
- `DadosCarteira` ganha `DadosContaBancaria ContaBancaria` (`CodigoBanco`, `NomeBanco`,
  `Agencia`, `Numero`, `Digito`, `Tipo`).

## PostgreSQL (migration `AdicionarDadosBancariosCarteira`, R7)

### `bancos` (nova)

| Coluna | Tipo | Restrição |
| --- | --- | --- |
| `codigo` | `char(3)` | `pk_bancos` |
| `nome` | `varchar(60)` | not null |

Dados: os cinco bancos (`HasData`).

### `carteiras` (colunas novas)

| Coluna | Tipo | Restrição |
| --- | --- | --- |
| `codigo_banco` | `char(3)` | not null; `fk_carteiras_bancos_codigo_banco` (RESTRICT); `ck_carteiras_codigo_banco` (`= '001'`) |
| `agencia` | `char(4)` | not null |
| `numero_conta` | `char(8)` | not null |
| `digito_conta` | `char(1)` | not null; `ck_carteiras_digito_conta` (`= calcular_digito_conta(agencia, numero_conta)`) |
| `tipo_conta_bancaria` | `varchar(20)` | not null; `ck_carteiras_tipo_conta_bancaria` (`IN ('conta_corrente','conta_poupanca')`) |

`ck_carteiras_formato_conta`: `agencia ~ '^[0-9]{4}$' AND numero_conta ~ '^[0-9]{8}$' AND
digito_conta ~ '^[0-9]$'`. Índice único `ux_carteiras_conta_bancaria (codigo_banco, agencia,
numero_conta)`.

### `contas_externas` (nova)

`id uuid` (`pk_contas_externas`), as cinco colunas bancárias com os mesmos `CHECK` (prefixo
`ck_contas_externas_`, com `codigo_banco <> '001'`), `titular_nome varchar(150)`,
`titular_documento varchar(14)` (`ck_contas_externas_titular_documento`: 11 ou 14 dígitos),
`criado_em timestamptz`; `fk_contas_externas_bancos_codigo_banco` (RESTRICT); índice único
`ux_contas_externas_conta_bancaria`.

### Sequence e function

- `seq_numero_conta_one_bank`: `START 1`, `MAXVALUE 99999999`, `NO CYCLE`.
- `calcular_digito_conta(agencia text, numero text) RETURNS char(1) IMMUTABLE`
  (`Sql/Procedures/CalcularDigitoConta.sql`).

### Backfill (`Sql/Migracoes/PreencherDadosBancariosCarteiras.sql`)

Para cada carteira sem dados, numerada por `row_number() OVER (ORDER BY id)`:
`codigo_banco = '001'`, `agencia = '0001'`, `numero_conta = lpad(n::text, 8, '0')`,
`digito_conta = calcular_digito_conta(...)`, `tipo_conta_bancaria = 'conta_corrente'`; depois,
`setval('seq_numero_conta_one_bank', max)` (ou `setval(..., 1, false)` com a tabela vazia). Saldo, `atualizado_em`
e demais tabelas não são tocados (FR-007). A coluna `xmin` muda, mas o update é feito antes de
qualquer uso da aplicação.

## Transições

Não há: os dados bancários são definidos na criação (cadastro, backfill ou script) e não mudam.
