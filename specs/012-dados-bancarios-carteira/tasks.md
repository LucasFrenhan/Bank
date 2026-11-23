---

description: "Lista de tarefas da feature 012-dados-bancarios-carteira"
---

# Tasks: Dados bancários da carteira

**Input**: Design documents from `specs/012-dados-bancarios-carteira/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/carteira-minha.md](contracts/carteira-minha.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII): unitários (Domain, Application) e
integração contra PostgreSQL em contêiner. Em cada história os testes são escritos primeiro e
devem falhar antes da implementação. Exceção: os testes da US2 (T021–T022) verificam o backfill,
que nasce junto com a migration da US1 (uma migration única, R7), e por isso já nascem verdes.

**Organization**: fundação (catálogo de bancos, tipo de conta, `ContaBancaria` com Luhn,
function SQL, entidade `ContaExterna`); US1 (cadastro gera conta corrente, com a migration);
US2 (contas existentes preenchidas); US3 (consulta expõe `contaBancaria`); US4 (scripts de outros
bancos e poupança); por fim, telemetria, documentação e validação.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (abreviado `back/`): `back/src/Bank.*` e `back/tests/Bank.*`; comandos
`dotnet` rodam em `src/backend/`. Testes de integração em `back/tests/Bank.IntegrationTests/`
(abreviado `it/`). SQL da aplicação em `back/src/Bank.Infrastructure/Persistencia/Sql/`
(abreviado `sql/`), embutido pelo `.csproj` (`Persistencia/Sql/**/*.sql`) e lido por
`SqlEmbutido.Ler`.

---

## Phase 1: Setup

**Purpose**: nada a preparar: sem projeto, pacote, serviço de Compose ou configuração novos
(plan.md, "nenhuma dependência nova"). A branch `012-dados-bancarios-carteira` já existe.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: tipos do Domain, regra do dígito, catálogo de bancos e mapeamentos que todas as
histórias usam

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

- [ ] T001 [P] Criar `back/src/Bank.Domain/Bancos/Banco.cs`: `public sealed record Banco(string
  Codigo, string Nome)` com construtor privado para o EF (ou classe com `private set`),
  constantes `CodigoOneBank = "001"`, `TamanhoCodigo = 3`, `TamanhoMaximoNome = 60`, instâncias
  estáticas `OneBank` ("001", "One Bank"), `Aurora` ("002", "Banco Aurora"), `Horizonte`
  ("003", "Banco Horizonte"), `Atlantico` ("004", "Banco Atlântico"), `Cerrado` ("005",
  "Banco Cerrado") e `IReadOnlyList<Banco> Todos` nessa ordem (FR-001, R5); comentário XML
  citando a spec 012
- [ ] T002 [P] Criar `back/src/Bank.Domain/Bancos/TipoContaBancaria.cs`:
  `enum TipoContaBancaria { ContaCorrente = 1, ContaPoupanca = 2 }`
- [ ] T003 [P] Escrever `back/tests/Bank.UnitTests/Domain/ContaBancariaTests.cs` (deve falhar):
  vetores do contrato (`0001`+`00000001`→`6`, `00000002`→`4`, `00000042`→`0`, `12345678`→`0`,
  `99999999`→`6`) em `CalcularDigito`; `Nova(Banco.OneBank, "0001", 42, ContaCorrente)` →
  `Numero` `"00000042"`, `Digito` `"0"`, `CodigoBanco` `"001"`; número 0, negativo ou
  > 99.999.999 → `ArgumentOutOfRangeException`; agência com tamanho ≠ 4 ou não numérica →
  `ArgumentException`; para o número `12345678`, trocar qualquer um dos 12 dígitos de
  agência + número por qualquer outro valor muda o dígito (FR-004)
- [ ] T004 Criar `back/src/Bank.Domain/Bancos/ContaBancaria.cs` (value object, `sealed record`):
  `CodigoBanco`, `Agencia`, `Numero`, `Digito`, `Tipo`; `Nova(Banco banco, string agencia, long
  numero, TipoContaBancaria tipo)` (formata o número com 8 dígitos, valida, calcula o dígito);
  `Reconstituir(codigoBanco, agencia, numero, digito, tipo)` para montar a partir das colunas;
  `public static string CalcularDigito(string agencia, string numero)` com Luhn sobre os 12
  dígitos (R1); constantes `TamanhoAgencia = 4`, `TamanhoNumero = 8`, `NumeroMaximo =
  99_999_999`, `AgenciaPadrao = "0001"` (faz T003 passar)
- [ ] T005 [P] Criar `back/src/Bank.Domain/Bancos/ContaExternaId.cs` no formato de
  `back/src/Bank.Domain/Usuarios/UsuarioId.cs` e `back/src/Bank.Domain/Bancos/ContaExterna.cs`
  (R6): propriedades `Id`, `CodigoBanco`, `Agencia`, `NumeroConta`, `DigitoConta`,
  `TipoContaBancaria`, `TitularNome` (1–150), `TitularDocumento` (11 ou 14 dígitos), `CriadoEm`,
  todas com `private set` e construtor privado para o EF; sem fábrica pública (só o script grava,
  FR-011); constantes `TamanhoMaximoTitularNome = 150`, `TamanhoMaximoTitularDocumento = 14`
- [ ] T006 [P] Criar `sql/Procedures/CalcularDigitoConta.sql` (cabeçalho citando a spec 012,
  R1): `CREATE OR REPLACE FUNCTION calcular_digito_conta(agencia text, numero text) RETURNS
  char(1) LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE` com Luhn sobre `agencia || numero`
  (por exemplo, `generate_series` sobre as posições a partir da direita); e
  `sql/Procedures/RemoverCalcularDigitoConta.sql` (`DROP FUNCTION IF EXISTS
  calcular_digito_conta(text, text)`) para o `Down`
- [ ] T007 Mapear na Infrastructure (R4–R6): `Configuracoes/BancoConfiguracao.cs` (tabela
  `bancos`, `codigo char(3)` PK `pk_bancos`, `nome varchar(60)` obrigatório, `HasData` com
  `Banco.Todos`); `Configuracoes/ContaExternaConfiguracao.cs` (tabela `contas_externas`, PK
  `pk_contas_externas`, colunas e `CHECK` de [data-model.md](data-model.md) com prefixo
  `ck_contas_externas_`, incluindo `codigo_banco <> '001'`, formato, tipo, documento e
  `digito_conta = calcular_digito_conta(agencia, numero_conta)`; FK
  `fk_contas_externas_bancos_codigo_banco` RESTRICT; índice único
  `ux_contas_externas_conta_bancaria`); conversor `TipoContaBancaria` ↔ `conta_corrente` /
  `conta_poupanca` reutilizável (por exemplo em `Configuracoes/ConversoresBancarios.cs`);
  `BankDbContext`: `DbSet<Banco> Bancos`, `DbSet<ContaExterna> ContasExternas`, conversor de
  `ContaExternaId` em `ConfigureConventions` e a sequence `seq_numero_conta_one_bank`
  (`HasSequence<long>`, início 1, máximo 99.999.999, sem ciclo) com o nome em constante

**Checkpoint**: `dotnet build` verde; `ContaBancariaTests` verde

---

## Phase 3: User Story 1 - Nova conta nasce com dados bancários (Priority: P1) 🎯 MVP

**Goal**: todo cadastro cria carteira no banco 001, agência 0001, número sequencial com dígito e
tipo conta corrente

**Independent Test**: cadastrar PF e PJ e conferir os dados gravados; 50 cadastros simultâneos
sem número repetido

### Testes da US1 (escrever primeiro)

- [ ] T008 [P] [US1] Em `back/tests/Bank.UnitTests/Domain/UsuarioTests.cs` (e criar
  `back/tests/Bank.UnitTests/Domain/CarteiraTests.cs` se não existir): `Usuario.Criar` com uma
  `ContaBancaria` grava `CodigoBanco`, `Agencia`, `NumeroConta`, `DigitoConta` e
  `TipoContaBancaria` na carteira e `Carteira.ContaBancaria` devolve o mesmo value object;
  conta de banco ≠ 001 → `ArgumentException` (FR-014)
- [ ] T009 [P] [US1] Em `back/tests/Bank.UnitTests/Application/RegistrarUsuarioTests.cs`: com um
  `IGeradorNumeroConta` falso que devolve 7, o usuário criado tem conta `001`/`0001`/`00000007`
  com o dígito de `CalcularDigito` e tipo `ContaCorrente` (FR-005/FR-006); com comando inválido o
  gerador não é chamado
- [ ] T010 [P] [US1] Criar `it/DadosBancariosCadastroTests.cs` (`IClassFixture<BankApiFactory>`):
  cadastro PF e PJ → linhas de `carteiras` com `001`, `0001`, número de 8 dígitos, dígito igual a
  `ContaBancaria.CalcularDigito` e `conta_corrente`, números diferentes; 50 cadastros simultâneos
  (`Task.WhenAll`) → 50 números distintos (SC-003); cadastro recusado por e-mail repetido não
  cria carteira (US1-3)
- [ ] T011 [P] [US1] Criar `it/DadosBancariosRestricoesTests.cs` (`IClassFixture<BankApiFactory>`):
  para os vetores do contrato, `SELECT calcular_digito_conta(...)` = `ContaBancaria.CalcularDigito`;
  em uma carteira cadastrada, `UPDATE` com dígito errado → `ck_carteiras_digito_conta`;
  `codigo_banco = '002'` → `ck_carteiras_codigo_banco`; `tipo_conta_bancaria = 'outro'` →
  `ck_carteiras_tipo_conta_bancaria`; repetir o número de outra carteira →
  `ux_carteiras_conta_bancaria`; `bancos` tem exatamente os cinco registros com os nomes de FR-001

### Implementação da US1

- [ ] T012 [US1] Em `back/src/Bank.Domain/Carteiras/Carteira.cs`: propriedades `CodigoBanco`,
  `Agencia`, `NumeroConta`, `DigitoConta`, `TipoContaBancaria` (`private set`) e `ContaBancaria`
  (montada por `ContaBancaria.Reconstituir`); `Criar(UsuarioId, ContaBancaria conta, decimal
  saldoInicial, DateTimeOffset agora)` recusa banco ≠ `Banco.CodigoOneBank`
- [ ] T013 [US1] Em `back/src/Bank.Domain/Usuarios/Usuario.cs`: `Criar(..., string senhaHash,
  ContaBancaria conta, decimal saldoInicial, DateTimeOffset agora)` repassa a conta
- [ ] T014 [US1] Criar `back/tests/Bank.UnitTests/ContasDeTeste.cs` (`ContaBancaria
  Corrente(long numero = 1)`) e ajustar os chamadores de `Usuario.Criar` em
  `back/tests/Bank.UnitTests/` (`AutenticarTests`, `DublesTransferencia`, `LancamentoAberturaTests`,
  `TransferenciaTests`, `UsuarioTests`, `EmissorTokenJwtTests`) e em
  `it/AberturaNaInicializacaoTests.cs` (números distintos quando gravados no banco)
- [ ] T015 [P] [US1] Criar `back/src/Bank.Application/Abstracoes/IGeradorNumeroConta.cs`
  (`Task<long> ProximoAsync(CancellationToken)`, comentário citando R2) e usá-lo em
  `back/src/Bank.Application/Usuarios/RegistrarUsuario.cs`: depois das validações e das
  verificações de duplicidade, obter o número e montar `ContaBancaria.Nova(Banco.OneBank,
  ContaBancaria.AgenciaPadrao, numero, TipoContaBancaria.ContaCorrente)` (faz T009 passar)
- [ ] T016 [US1] Criar `sql/Consultas/ObterProximoNumeroConta.sql` (`SELECT
  nextval('seq_numero_conta_one_bank')`, cabeçalho citando a spec 012) e
  `back/src/Bank.Infrastructure/Persistencia/GeradorNumeroConta.cs` (internal sealed,
  `BankDbContext`, executa o SQL embutido com `SqlQueryRaw<long>`/`Database.SqlQuery` e devolve o
  valor); registrar `IGeradorNumeroConta` como scoped em `AddPersistencia`
  (`back/src/Bank.Infrastructure/DependencyInjection.cs`)
- [ ] T017 [US1] Em `Configuracoes/CarteiraConfiguracao.cs` (R4): colunas `codigo_banco char(3)`,
  `agencia char(4)`, `numero_conta char(8)`, `digito_conta char(1)`, `tipo_conta_bancaria
  varchar(20)` obrigatórias; `Ignore(c => c.ContaBancaria)`; `CHECK` `ck_carteiras_codigo_banco`,
  `ck_carteiras_tipo_conta_bancaria`, `ck_carteiras_formato_conta`, `ck_carteiras_digito_conta`;
  FK `fk_carteiras_bancos_codigo_banco` (RESTRICT, sem navegação); índice único
  `ux_carteiras_conta_bancaria`; nomes em constantes públicas para os testes
- [ ] T018 [US1] Criar `sql/Migracoes/PreencherDadosBancariosCarteiras.sql` (cabeçalho citando a
  spec 012 e R7): `UPDATE carteiras` das linhas com `numero_conta IS NULL`, em ordem de `id`,
  com `001`, `0001`, `lpad(nextval('seq_numero_conta_one_bank')::text, 8, '0')`, dígito pela
  function e `conta_corrente` (numeração ordenada por subconsulta com `ORDER BY id`)
- [ ] T019 [US1] Gerar a migration `AdicionarDadosBancariosCarteira` (`dotnet ef migrations add
  AdicionarDadosBancariosCarteira -p src/Bank.Infrastructure -s src/Bank.Api`) e reordenar o `Up`
  conforme R7: `bancos` + dados; sequence; `migrationBuilder.Sql(SqlEmbutido.Ler(
  "Procedures/CalcularDigitoConta.sql"))`; colunas de `carteiras` **anuláveis**;
  `Sql(SqlEmbutido.Ler("Migracoes/PreencherDadosBancariosCarteiras.sql"))`; `AlterColumn` para
  obrigatórias; índice, FK e `CHECK` de `carteiras`; `contas_externas`. `Down` na ordem inversa,
  removendo a function com `RemoverCalcularDigitoConta.sql`. Conferir que o snapshot só ganha
  linhas e que `SqlEmbutido` resolve a subpasta `Migracoes/`
- [ ] T020 [US1] Rodar `dotnet test` dos projetos UnitTests, ArchitectureTests e
  IntegrationTests: T008–T011 verdes e a suíte das specs 001–011 sem regressão (SC-006);
  corrigir testes que inserem carteiras por SQL, se houver

**Checkpoint**: cadastro gera conta corrente válida; MVP entregue

---

## Phase 4: User Story 2 - Contas existentes ganham dados bancários (Priority: P1)

**Goal**: a atualização preenche todas as carteiras antigas sem tocar em saldo e histórico

**Independent Test**: base na versão anterior com dados → migrar → conferir

### Testes da US2 (nascem verdes; ver nota no topo)

- [ ] T021 [US2] Criar `it/DadosBancariosMigracaoTests.cs`: banco próprio no contêiner
  compartilhado (`PostgresCompartilhado.CriarConnectionStringAsync`); `BankDbContext` com
  `IMigrator.MigrateAsync("20261123141050_AdicionarNotificacoesAuditoria")`; inserir por SQL do
  teste 3 usuários com carteiras (saldos diferentes), uma transferência com seus lançamentos;
  `MigrateAsync()` até o fim; conferir: 3 carteiras com `001`/`0001`/`conta_corrente`, números
  `00000001`–`00000003` na ordem dos `id`, dígitos válidos; saldos, `atualizado_em`,
  transferências e lançamentos idênticos aos de antes (FR-007, SC-002); depois, um
  `nextval` devolve 4 (FR-008)
- [ ] T022 [US2] No mesmo arquivo: base vazia migra do zero até o fim sem erro (US2-4); e
  `Down` até `AdicionarNotificacoesAuditoria` seguido de `Up` funciona

**Checkpoint**: backfill comprovado

---

## Phase 5: User Story 3 - Consultar os dados bancários da própria conta (Priority: P2)

**Goal**: `GET /api/v1/carteiras/minha` expõe `contaBancaria` (contrato 1.1.0)

**Independent Test**: cadastrar, entrar, consultar e conferir `contaBancaria` e os campos antigos

### Testes da US3 (escrever primeiro)

- [ ] T023 [P] [US3] Em `it/MinhaCarteiraTests.cs`: a resposta traz `contaBancaria.banco.codigo`
  `"001"`, `banco.nome` `"One Bank"`, `agencia` `"0001"`, `numero` igual ao gravado em
  `carteiras`, `digito` válido e `tipo` `"ContaCorrente"`, todos como string JSON; os campos da
  v1.0.0 continuam iguais (FR-009)
- [ ] T024 [P] [US3] Em `it/OpenApiTests.cs`: o schema de `MinhaCarteiraResposta` tem
  `contaBancaria` obrigatório com `banco` (`codigo`, `nome`), `agencia`, `numero`, `digito` e
  `tipo` do tipo string; as respostas de cadastro, `usuarios/me`, transferência e extrato não
  ganham `contaBancaria` (FR-009)

### Implementação da US3

- [ ] T025 [US3] Em `back/src/Bank.Application/Abstracoes/ICarteiraConsultas.cs`: `DadosContaBancaria
  (string CodigoBanco, string NomeBanco, string Agencia, string Numero, string Digito,
  TipoContaBancaria Tipo)` e o campo `ContaBancaria` em `DadosCarteira`; em
  `back/src/Bank.Infrastructure/Persistencia/CarteiraConsultas.cs`, preencher com join em
  `contexto.Bancos` pelo código
- [ ] T026 [US3] Em `back/src/Bank.Api/Endpoints/CarteirasEndpoints.cs`: `MinhaCarteiraResposta`
  ganha `ContaBancariaResposta ContaBancaria` (`BancoResposta Banco`, `Agencia`, `Numero`,
  `Digito`, `Tipo` como `ToString()` do enum), mapeado de `DadosCarteira` (faz T023–T024 passarem)
- [ ] T027 [P] [US3] Em `specs/003-usuarios-carteiras/contracts/usuarios-carteiras-api.md`, seção
  `GET /api/v1/carteiras/minha`: nota apontando para a versão 1.1.0 em
  `specs/012-dados-bancarios-carteira/contracts/carteira-minha.md`

**Checkpoint**: consulta com dados bancários

---

## Phase 6: User Story 4 - Contas de outros bancos e conta poupança por script (Priority: P3)

**Goal**: scripts reexecutáveis para registrar contas nos bancos 002–005 e criar um titular com
conta poupança

**Independent Test**: executar os scripts duas vezes e conferir contagens, login e consulta

### Testes da US4 (escrever primeiro)

- [ ] T028 [US4] Criar `it/ScriptsDadosBancariosTests.cs` (`IClassFixture<BankApiFactory>`): lê
  os arquivos de `scripts/sql/dados-bancarios/` a partir da raiz do repositório (subindo
  diretórios até achar `CONSTITUTION.md`) e executa cada um duas vezes com `NpgsqlCommand`;
  conferir: `contas_externas` com contas em cada banco 002–005, ambos os tipos, dígitos válidos,
  contagem igual após a segunda execução (SC-005); nenhum registro com banco 001; um usuário com
  o e-mail do script e carteira `conta_poupanca` no 001, só uma após a segunda execução; login
  com a senha documentada no script e `GET /carteiras/minha` → `contaBancaria.tipo`
  `"ContaPoupanca"`; `INSERT` manual em `contas_externas` com banco `'999'` → FK e com `'001'`
  → `ck_contas_externas_codigo_banco` (FR-011, FR-014)

### Implementação da US4

- [ ] T029 [P] [US4] Criar `scripts/sql/dados-bancarios/registrar-contas-outros-bancos.sql`
  (cabeçalho: spec 012, como executar, que é reexecutável): dentro de `BEGIN/COMMIT`, insere
  duas contas por banco 002–005 (uma `conta_corrente`, uma `conta_poupanca`), agência e número
  fixos, dígito por `calcular_digito_conta`, titulares e documentos fictícios (CPF/CNPJ válidos),
  `id = uuidv7()`, `criado_em = now()`; `ON CONFLICT ON CONSTRAINT
  ux_contas_externas_conta_bancaria DO NOTHING`
- [ ] T030 [P] [US4] Criar `scripts/sql/dados-bancarios/criar-conta-poupanca.sql` (cabeçalho:
  spec 012, e-mail e senha de demonstração, reexecutável): dentro de `BEGIN/COMMIT`, se o e-mail
  não existir, insere o usuário PF (nome, e-mail, CPF válido fixo, `tipo_conta` como o mapeamento
  atual grava, `senha_hash` V3 do Identity pré-calculado para a senha documentada — gerar com
  `HasherSenhaIdentity` uma vez) e a carteira com `id = uuidv7()`, saldo 0, banco 001, agência
  0001, número `nextval`, dígito pela function e `conta_poupanca` (FR-010)
- [ ] T031 [US4] Rodar T028 até ficar verde

**Checkpoint**: contas de outros bancos e poupança disponíveis por script

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T032 [P] Em `it/DadosSensiveisTelemetriaTests.cs` (ou equivalente da spec 003/006):
  depois de um cadastro e uma consulta da carteira, nenhum log, span ou tag de métrica contém o
  número da conta (FR-013)
- [ ] T033 [P] Em `docs/desenvolvimento.md`: seção "Dados bancários" com os cinco bancos, a
  regra do dígito (Luhn), a sequence e os comandos dos scripts (quickstart, cenário 4)
- [ ] T034 Rodar `dotnet build` e `dotnet test` em Release (como no CI) com o Docker em execução;
  depois, no Compose, validar os cenários 2–5 do [quickstart.md](quickstart.md) (para o cenário
  2, subir a `main` anterior, cadastrar e transferir, e então subir esta branch) e registrar o
  resultado no PR
- [ ] T035 Marcar na issue #28 os itens entregues (enum; propriedades + configuração com índice
  único; migration com backfill; gerador de número com dígito — no caso de uso `RegistrarUsuario`,
  não em um `AutenticacaoServico`, que não existe; DTO e testes) e abrir o PR com `Closes #28` e
  `--assignee @me`, citando que o frontend fica para uma task posterior

---

## Dependencies & Execution Order

- **Foundational (2)** → US1 (3) → US2 (4) e US3 (5) → US4 (6) → Polish (7)
- T004 depende de T001–T003; T007 de T001, T002, T005 e T006; T012 de T004; T013 de T012; T014
  de T013; T015 de T013; T016 de T007 e T015; T017 de T012 e T007; T019 de T006, T007, T017 e
  T018; T020 de tudo da US1.
- US2 e US3 dependem só da US1 e podem andar em paralelo (arquivos diferentes); US4 depende da
  migration (T019) e, para o login, de nada além da US1; o teste de consulta da poupança usa a
  US3.
- Dentro de cada história: testes primeiro (falhando), depois implementação (exceto US2).

### Parallel Opportunities

- T001, T002, T003, T005 e T006
- T008, T009, T010 e T011
- T021 (US2) com T023–T027 (US3)
- T029 e T030
- T032 e T033

## Parallel Example: User Story 1

```text
T008 UsuarioTests/CarteiraTests.cs   T009 RegistrarUsuarioTests.cs
T010 DadosBancariosCadastroTests.cs  T011 DadosBancariosRestricoesTests.cs
```

---

## Implementation Strategy

### MVP First

1. Fundação (T001–T007)
2. US1 (T008–T020) → cadastro com conta corrente, migration aplicada
3. **PARAR e VALIDAR**: quickstart, cenário 3 (parte do cadastro)

### Incremental Delivery

1. US2 → backfill comprovado em base com dados
2. US3 → `contaBancaria` na consulta e no OpenAPI
3. US4 → scripts de outros bancos e poupança
4. Polish → telemetria, documentação, validação no Compose, issue e PR

### Commits

Conventional Commits por fase, escopo `dados-bancarios` (por exemplo `feat(dados-bancarios):
...`, `test(dados-bancarios): ...`, `docs(dados-bancarios): ...`); PR com `Closes #28`.
