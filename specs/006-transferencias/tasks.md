---

description: "Lista de tarefas da feature 006-transferencias"
---

# Tasks: Motor de transferências e extrato

**Input**: Design documents from `specs/006-transferencias/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/transferencias-api.md](contracts/transferencias-api.md),
[contracts/evento-transferencia-concluida.md](contracts/evento-transferencia-concluida.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII; plano de testes da #13). Testes escritos
primeiro e falhando antes da implementação.

**Organization**: US1 (transferir), US1a (consultar destinatário), US2 (idempotência), US3
(concorrência), US4 (extrato).

## Format: `[ID] [P?] [Story] Description`

## Path Conventions

Backend em `src/backend/` (abreviado `backend/`); SQL em
`backend/src/Bank.Infrastructure/Persistencia/Sql/` (abreviado `Sql/`).

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: domínio, razão, outbox, SQL, migration, abertura e ajustes das specs anteriores

### Testes (escrever primeiro, devem falhar)

- [X] T001 [P] Criar `backend/tests/Bank.UnitTests/Domain/ValorTransferenciaTests.cs`: aceita
  0,01, 150,50 e 9.999.999.999.999.999,99; rejeita 0, negativo, 10,005 e acima do máximo, cada um
  com erro no campo `valor`
- [X] T002 [P] Criar `backend/tests/Bank.UnitTests/Domain/CarteiraMovimentacaoTests.cs`:
  `Debitar` com saldo suficiente (inclusive deixando zero) e insuficiente (`saldo-insuficiente`,
  saldo inalterado); `Creditar`; ambos atualizam `AtualizadoEm`
- [X] T003 [P] Criar `backend/tests/Bank.UnitTests/Domain/TransferenciaTests.cs`: criação gera
  transferência `Concluida`, lançamento `Saida` na origem e `Entrada` no destino com o mesmo valor,
  descrição e data; descrição com espaços → nula, > 200 → erro; origem = destino → `autotransferencia`;
  evento `TransferenciaConcluida` com os ids e o valor
- [X] T004 [P] Criar `backend/tests/Bank.UnitTests/Domain/LancamentoAberturaTests.cs` e estender
  `UsuarioTests.cs`: `Usuario.Criar` com saldo > 0 gera abertura `Entrada` "Saldo inicial de
  demonstração" com o valor do saldo; saldo 0 não gera; `Documento.Ocultar` mascara CPF
  (`•••.982.247-••`) e mantém CNPJ completo
- [X] T005 [P] Criar `backend/tests/Bank.UnitTests/Infrastructure/EventoOutboxTests.cs`: o payload
  serializado de `TransferenciaConcluida` tem exatamente os campos camelCase do contrato v1
  (`eventoId`, `transferenciaId`, `pagadorUsuarioId`, `recebedorUsuarioId`, `valor` com 2 casas,
  `ocorridoEm` UTC), `tipo` `transferencia.concluida` e `versao` 1
- [X] T006 [P] Criar `backend/tests/Bank.ArchitectureTests/SqlEmbutidoTests.cs`: os três arquivos
  `.sql` do data-model existem como recursos embutidos do assembly da Infrastructure e
  `SqlEmbutido.Ler` devolve o conteúdo de cada um (quatro arquivos, incluindo
  `Procedures/RemoverImpedimentoDeAlteracao.sql`)

### Implementação

- [X] T007 [P] Adicionar `TipoErro.RegraDeNegocio` em `backend/src/Bank.Domain/Comum/Erro.cs` e o
  mapeamento 422 + títulos ("Destinatário não encontrado", "Transferência para a própria conta",
  "Saldo insuficiente", "Chave de idempotência já usada com outros dados") em
  `backend/src/Bank.Api/ErrorHandling/ResultadoHttpExtensions.cs`
- [X] T008 [P] Criar `backend/src/Bank.Domain/Transferencias/ValorTransferencia.cs` e
  `ErrosTransferencia.cs` (`destinatario-nao-encontrado`, `autotransferencia`,
  `saldo-insuficiente`, `idempotencia-divergente` — data-model) (faz T001 passar)
- [X] T009 Adicionar `Debitar`/`Creditar` em `backend/src/Bank.Domain/Carteiras/Carteira.cs`
  (faz T002 passar)
- [X] T010 Criar `backend/src/Bank.Domain/Transferencias/Lancamento.cs` (`TipoLancamento`,
  `DirecaoLancamento`, fábricas `Abertura`, `SaidaDeTransferencia`, `EntradaDeTransferencia`),
  `Transferencia.cs` (`StatusTransferencia`; `Executar(origem, destino, valor, descricao, chave,
  agora)` → débito, crédito, transferência, 2 lançamentos, evento) e `TransferenciaConcluida.cs`;
  em `Usuario.Criar`, gerar `LancamentoDeAbertura` quando o saldo inicial > 0; em
  `Documento`, `Ocultar(string)` (faz T003 e T004 passar)
- [X] T011 Criar os arquivos SQL (cabeçalho com a spec de origem — constituição v1.1.0):
  `Sql/Procedures/ImpedirAlteracaoDeRegistrosImutaveis.sql` (função
  `impedir_alteracao_de_registro_imutavel()` + triggers `BEFORE UPDATE OR DELETE` em
  `transferencias` e `lancamentos`), `Sql/Consultas/BloquearCarteirasParaTransferencia.sql`
  (`SELECT id, usuario_id, saldo, moeda, atualizado_em, xmin FROM carteiras WHERE id = ANY(@ids)
  ORDER BY id FOR UPDATE`) e `Sql/Consultas/CriarLancamentosDeAbertura.sql` (`INSERT ... SELECT`
  idempotente das aberturas faltantes, valor = saldo − soma dos lançamentos quando > 0) e
  `Sql/Procedures/RemoverImpedimentoDeAlteracao.sql` (remove triggers e função); incluir
  `<EmbeddedResource Include="Persistencia/Sql/**/*.sql" />` em
  `backend/src/Bank.Infrastructure/Bank.Infrastructure.csproj` e criar
  `backend/src/Bank.Infrastructure/Persistencia/SqlEmbutido.cs` (faz T006 passar)
- [X] T012 Criar `backend/src/Bank.Infrastructure/Mensageria/EventoOutbox.cs` (entidade +
  `DeTransferenciaConcluida`, serialização `System.Text.Json` camelCase) (faz T005 passar)
- [X] T013 Criar `Configuracoes/TransferenciaConfiguracao.cs`, `LancamentoConfiguracao.cs` e
  `EventoOutboxConfiguracao.cs` em `backend/src/Bank.Infrastructure/Persistencia/` com o
  mapeamento exato do data-model (nomes de tabelas, colunas, `numeric(18,2)`, checks
  `ck_transferencias_valor_positivo`, `ck_transferencias_origem_diferente_destino`,
  status/tipo/direção, `ck_lancamentos_transferencia_coerente`
  (`(tipo = 'Transferencia') = (transferencia_id IS NOT NULL)`), `ux_transferencias_origem_chave`, `ix_lancamentos_extrato`
  `(carteira_id, criado_em DESC, id DESC)`, único parcial `ux_lancamentos_abertura_por_carteira`,
  `ix_outbox_eventos_pendentes`, FKs `RESTRICT`, `payload jsonb`); `DbSet`s em `BankDbContext`
- [X] T014 Gerar a migration `CriarTransferenciasLancamentosEOutbox` e acrescentar ao `Up` a
  execução de `SqlEmbutido.Ler("Procedures/ImpedirAlteracaoDeRegistrosImutaveis.sql")` (e ao
  `Down` a execução de `SqlEmbutido.Ler("Procedures/RemoverImpedimentoDeAlteracao.sql")` —
  nenhum `migrationBuilder.Sql("...")` com SQL escrito no C#, regra da constituição v1.1.0);
  revisar o SQL gerado contra o data-model
- [X] T015 Gravar a abertura no cadastro: `backend/src/Bank.Infrastructure/Persistencia/UsuarioRepositorio.cs`
  adiciona `usuario.LancamentoDeAbertura` no mesmo `SaveChanges` (RN-04 da spec 003 continua
  atômica)
- [X] T016 Criar `backend/src/Bank.Infrastructure/Persistencia/LancamentosDeAberturaNaInicializacao.cs`
  (serviço hospedado registrado depois das migrations; executa `CriarLancamentosDeAbertura.sql`
  quando `Persistencia:CriarLancamentosDeAberturaNaInicializacao` = true) e a opção em
  `PersistenciaOptions`; ligar a flag em `appsettings.Development.json` e
  `appsettings.Testing.json` (padrão `false`)
- [X] T017 Reescrever os testes de "conta removida" de
  `backend/tests/Bank.IntegrationTests/MinhaCarteiraTests.cs` e `UsuariosMeTests.cs` para usar
  um token válido (`TokensDeTeste` com a chave da fábrica) com `sub` de usuário inexistente, sem
  apagar dados (Complexity Tracking)
- [X] T018 Executar `dotnet test` e confirmar T001–T006 e todos os testes das specs 002–005
  verdes

**Checkpoint**: razão, outbox, SQL e abertura prontos

---

## Phase 2: User Story 1 - Transferir para outra conta (Priority: P1) 🎯 MVP

**Goal**: `POST /api/v1/transferencias` P2P e P2B com todas as recusas

**Independent Test**: quickstart cenário 2, passos 1, 3 e 6–8

### Testes (escrever primeiro, devem falhar)

- [X] T019 [P] [US1] Criar `backend/tests/Bank.UnitTests/Application/RealizarTransferenciaTests.cs`
  (dublês de `IUsuarioConsultas`, `ITransferenciaRepositorio`, `IUnidadeDeTransferencia`):
  sucesso; validação (chave ausente, valor inválido, descrição longa, documento inválido) com
  todos os erros juntos; destino inexistente → 404; autotransferência → 422; saldo insuficiente
  → 422 sem gravar; bloqueio que não devolve as duas carteiras (removida entre a busca e o
  débito) → desfaz e responde 404 `destinatario-nao-encontrado`; métricas
  `concluidas`/`recusadas{motivo}`/`valor`
- [X] T020 [P] [US1] Criar `backend/tests/Bank.IntegrationTests/TransferenciasTests.cs`: P2B (PF
  R$ 10,00 → PJ R$ 4,50: 201 com comprovante do contrato, saldos 5,50/4,50 em
  `/carteiras/minha`); P2P para CPF mascarado; saldo exato até zero; PJ → 403; sem token → 401;
  destino inexistente → 404; próprio CPF → 422 `autotransferencia`; valor acima do saldo → 422
  `saldo-insuficiente` sem mudança; 400 por campo (sem `Idempotency-Key`, chave não-UUID, valor 0,
  -1, 10,005, descrição com 201 caracteres); todas as respostas de erro com `type` do contrato e
  `traceId`; outbox com 1 evento `transferencia.concluida` v1 por transferência e nenhum por recusa

### Implementação

- [X] T021 [P] [US1] Criar as abstrações em `backend/src/Bank.Application/Abstracoes/`:
  `IUsuarioConsultas.ObterDestinatarioAsync(documentoNormalizado)` (usuário, carteira, nome,
  documento, tipo), `ITransferenciaRepositorio` (`ObterPorChaveAsync(carteiraOrigemId, chave)` →
  comprovante) e `IUnidadeDeTransferencia` (`IniciarAsync`, `BloquearCarteirasAsync(idA, idB)` →
  carteiras na ordem por id, `ObterPorChaveAsync`, `Adicionar(transferencia, lançamentos,
  evento)`, `ConfirmarAsync` → `Ok`/`ChaveDuplicada`)
- [X] T022 [P] [US1] Criar `backend/src/Bank.Application/Observabilidade/MetricasTransferencias.cs`
  (meter `Bank.Transferencias`, instrumentos do research R10) e registrá-lo no `AddTelemetria`
- [X] T023 [US1] Criar `backend/src/Bank.Application/Transferencias/RealizarTransferencia.cs`
  (comando com `Idempotency-Key`, documento, valor, descrição; validação acumulada; busca do
  destino; transação com bloqueio; `Transferencia.Executar`; comprovante; métricas; logs só com
  ids) e registrá-lo em `DependencyInjection.cs` (faz T019 passar, exceto idempotência)
- [X] T024 [US1] Implementar na Infrastructure `UnidadeDeTransferencia.cs` (transação
  `ReadCommitted`; `FromSqlRaw(SqlEmbutido.Ler("Consultas/BloquearCarteirasParaTransferencia.sql"),
  @ids)` sem composição; menos de duas carteiras bloqueadas → rollback e resultado
  `CarteiraIndisponivel`; grava transferência, lançamentos e `EventoOutbox` no mesmo
  `SaveChanges`; `23505` em `ux_transferencias_origem_chave` → `ChaveDuplicada` com rollback),
  `TransferenciaRepositorio.cs` e `UsuarioConsultas.ObterDestinatarioAsync`; registrar em
  `DependencyInjection.cs`
- [X] T025 [US1] Criar `backend/src/Bank.Api/Endpoints/TransferenciasEndpoints.cs` com
  `POST /api/v1/transferencias` (`[FromHeader(Name = "Idempotency-Key")] string?`; políticas
  `UsuarioAutenticado` + `Permissao:RealizarTransferencia`; 201 novo / 200 replay; OpenAPI com o
  cabeçalho obrigatório e as respostas do contrato) e mapear em `Program.cs` (faz T020 passar)

**Checkpoint**: transferências P2P/P2B funcionais

---

## Phase 3: User Story 1a - Confirmar o destinatário (Priority: P1)

**Goal**: consulta prévia do nome e tipo de conta pelo documento

**Independent Test**: quickstart cenário 2, passo 2

- [X] T026 [P] [US1a] Criar `backend/tests/Bank.UnitTests/Application/ConsultarDestinatarioTests.cs`
  (encontrado; inexistente; próprio documento; inválido; métrica por `resultado`)
- [X] T027 [P] [US1a] Criar `backend/tests/Bank.IntegrationTests/DestinatarioTests.cs`: CNPJ
  alfanumérico minúsculo com máscara e CPF → 200 só com `nome` e `tipoConta`; inexistente → 404;
  próprio → 422; inválido → 400; PJ → 403; sem token → 401
- [X] T028 [US1a] Criar `backend/src/Bank.Application/Transferencias/ConsultarDestinatario.cs` e o
  endpoint `POST /api/v1/transferencias/destinatarios/consultar` em `TransferenciasEndpoints.cs`
  (documento no corpo; mesmas políticas do POST) (faz T026–T027 passar)

---

## Phase 4: User Story 2 - Repetir sem pagar duas vezes (Priority: P1)

**Goal**: idempotência por pagador com `Idempotency-Key`

**Independent Test**: quickstart cenário 2, passos 4–5

- [X] T029 [P] [US2] Estender `RealizarTransferenciaTests.cs`: replay antes do bloqueio (200, sem
  débito, métrica `replays`); divergência de valor ou destino → 409; replay detectado depois do
  bloqueio; `ChaveDuplicada` na confirmação → relê e devolve replay/409
- [X] T030 [P] [US2] Criar `backend/tests/Bank.IntegrationTests/IdempotenciaTransferenciaTests.cs`:
  reenvio sequencial → 200 com o mesmo `transferenciaId` e 1 débito; mesma chave com outro valor
  e com outro destino → 409; **20 requisições paralelas** com a mesma chave → 1 × 201 + 19 × 200 e
  um único débito (SC-004); **10 paralelas com a mesma chave e valores diferentes** → 1 × 201 e
  9 × 409 `idempotencia-divergente`; mesma chave por outro pagador → nova transferência; replay com saldo
  já insuficiente → 200
- [X] T031 [US2] Completar o fluxo de idempotência em `RealizarTransferencia.cs` (busca antes e
  depois do bloqueio; tratamento de `ChaveDuplicada` — research R4) (faz T029–T030 passar)

---

## Phase 5: User Story 3 - Nunca gastar duas vezes (Priority: P1)

**Goal**: bloqueio ordenado sem saldo negativo e sem impasse

**Independent Test**: os dois testes de concorrência abaixo contra o PostgreSQL real

- [X] T032 [US3] Criar `backend/tests/Bank.IntegrationTests/ConcorrenciaTransferenciaTests.cs`
  com uma fábrica derivada (`Carteiras:SaldoInicial:PessoaFisica=1000`):
  - **5 × R$ 300** simultâneas (chaves distintas) de uma carteira com R$ 1.000 → exatamente 3 ×
    201 e 2 × 422 `saldo-insuficiente`; saldo final R$ 100; nunca negativo (SC-002) — repetido 5
    vezes no mesmo teste;
  - **50 transferências cruzadas** A→B e B→A simultâneas → todas concluídas em < 10 s, sem erro de
    impasse, soma dos saldos preservada (SC-003);
  - após cada cenário, saldo = soma dos lançamentos para as carteiras envolvidas
- [X] T033 [US3] Ajustar a implementação se T032 apontar falhas (ordem do bloqueio, transação) e
  registrar no research qualquer descoberta

---

## Phase 6: User Story 4 - Extrato (Priority: P2)

**Goal**: `GET /api/v1/transferencias/extrato` paginado, com filtro de direção e abertura

**Independent Test**: quickstart cenário 2, passo 7

- [X] T034 [P] [US4] Criar `backend/tests/Bank.UnitTests/Application/ObterExtratoTests.cs`:
  validação de `pagina`, `tamanhoPagina`, `direcao` (erros por parâmetro); CPF da contraparte
  mascarado e CNPJ completo; abertura sem contraparte
- [X] T035 [P] [US4] Criar `backend/tests/Bank.IntegrationTests/ExtratoTests.cs`: pagador vê
  `Saida` e recebedor `Entrada` com a contraparte; PF nova vê a abertura de R$ 10,00 e a soma do
  extrato fecha com o saldo; 25 entradas + 5 saídas → página 1 com 20, página 2 com o resto,
  `direcao=Saida` total 5, `direcao=Entrada` total 25 (US4 4a); além da última página → vazia com
  o total; parâmetros inválidos → 400; ordem mais recente primeiro; PJ sem movimentações → vazio
- [X] T036 [US4] Criar `backend/src/Bank.Application/Abstracoes/IExtratoConsultas.cs`,
  `backend/src/Bank.Application/Transferencias/ObterExtrato.cs`,
  `backend/src/Bank.Infrastructure/Persistencia/ExtratoConsultas.cs` (LINQ com `Count`,
  `ORDER BY criado_em DESC, id DESC`, junção com a contraparte) e o endpoint
  `GET /api/v1/transferencias/extrato` (política `UsuarioAutenticado`) (faz T034–T035 passar)

**Checkpoint**: todas as histórias funcionais

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T037 [P] Criar `backend/tests/Bank.IntegrationTests/RazaoEImutabilidadeTests.cs`: UPDATE e
  DELETE em `lancamentos` e `transferencias` falham com o erro da trigger; após transferências
  variadas, toda carteira tem saldo = soma dos lançamentos (SC-005)
- [X] T038 [P] Criar `backend/tests/Bank.IntegrationTests/AberturaNaInicializacaoTests.cs`:
  carteira com saldo sem abertura (inserida direto na base, simulando a spec 003) recebe a
  abertura ao reiniciar a aplicação; reiniciar de novo não duplica; com a flag desligada, nada é
  criado
- [X] T039 [P] Estender `OpenApiTests.cs` (três endpoints, `Bearer`, cabeçalho `Idempotency-Key`
  obrigatório, respostas do contrato) e `DadosSensiveisTelemetriaTests.cs` (transferência, consulta
  de destinatário e extrato sem documento, nome ou e-mail em logs e spans; métricas
  `Bank.Transferencias` com as tags corretas)
- [X] T040 [P] Atualizar `docs/desenvolvimento.md` (transferências, `Idempotency-Key`, extrato,
  abertura, contratos) e a nota do saldo de demonstração em `specs/003-usuarios-carteiras/spec.md`
  (Assumptions: lançamento de abertura entregue pela spec 006)
- [X] T041 Executar `dotnet build`/`dotnet test` em Release e os cenários do
  [quickstart.md](quickstart.md) no Compose (inclui a abertura das carteiras já existentes na base
  local e o p95 de 100 transferências e 100 extratos — SC-006); registrar resultados e
  divergências neste arquivo

---

## Dependencies & Execution Order

- Phase 1 → todas as histórias
- US1 (Phase 2) → US1a, US2, US3 (reaproveitam endpoint e unidade de transferência)
- US4 depende só da Phase 1 + US1 (precisa de transferências para listar)
- Polish por último

### Parallel Opportunities

- Phase 1: T001–T006 em paralelo; T007, T008 em paralelo
- US1: T019, T020, T021, T022 em paralelo
- US1a, US2 e US4 em paralelo após a US1 (arquivos de teste distintos; implementação toca
  `TransferenciasEndpoints.cs` — fazer em sequência)
- Polish: T037–T040 em paralelo

## Implementation Strategy

1. Phase 1 → US1 (MVP: transferência funcional)
2. US2 e US3 (garantias financeiras) → US1a → US4
3. Polish e validação
4. Commits por fase (`feat(transferencias): ...`, `test(transferencias): ...`); PR com
   `Closes #8` e `Closes #13`

---

## Resultados da validação (T041, 2026-11-17)

- **Release**: build sem avisos nem erros; `dotnet test --solution Bank.slnx -c Release` →
  330 testes, 0 falhas, em três execuções seguidas.
- **Divergência encontrada e corrigida**: com a suíte completa em paralelo, 18 das 50
  transferências cruzadas (US3) voltaram 500 por `53300: sorry, too many clients already` — o
  contêiner PostgreSQL de teste usava o padrão de 100 conexões, insuficiente para as fábricas em
  paralelo mais as conexões retidas no `FOR UPDATE`. `PostgresCompartilhado` passou a subir com
  `max_connections=500`. Não houve falha na lógica de transferência.
- **Compose — cenário 2**: passos 1–8 conforme o roteiro (201, replay 200 com o mesmo
  `transferenciaId`, saldo 5,50, 409, 422 ×2, extrato com `Saida` e abertura, PJ → 403).
- **Compose — cenário 3**: 0 carteiras com saldo ≠ razão; `UPDATE` em `lancamentos` recusado
  pela trigger; 101 transferências ↔ 101 eventos `transferencia.concluida` v1 no outbox.
- **Compose — cenário 4**: a base local da spec 003 tinha 343 carteiras (288 com saldo); ao subir
  a API, 288 aberturas criadas e nenhuma divergência; reiniciar não duplicou.
- **SC-006 (p95, local)**: 100 transferências → 12,2 ms; 100 extratos (`tamanhoPagina=100`) →
  12,7 ms.
