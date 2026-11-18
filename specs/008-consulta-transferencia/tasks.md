---

description: "Lista de tarefas da feature 008-consulta-transferencia"
---

# Tasks: Consulta de transferência por identificador

**Input**: Design documents from `specs/008-consulta-transferencia/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/transferencias-api.md](contracts/transferencias-api.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII): unitários do caso de uso e de integração
com 200 (pagador e recebedor), 400, 401, 403 e 404 contra PostgreSQL em contêiner. Em cada
história, os testes são escritos primeiro e devem falhar antes da implementação.

**Organization**: fundação comum, depois US1 (pagador), US2 (recebedor) e US3 (terceiros e
erros); por fim, transversais.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (abreviado `back/`): `back/src/Bank.*` e `back/tests/Bank.*`. Comandos
`dotnet` rodam em `src/backend/`.

---

## Phase 1: Setup

Sem tarefas: nenhum projeto, dependência ou migration nova (plan.md, Technical Context).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: comprovante com documento do pagador e chave anulável, erro, métrica e leitura por
identificador

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Testes da fundação (escrever primeiro)

- [ ] T001 [P] Em `back/tests/Bank.UnitTests/Application/RealizarTransferenciaTests.cs`, acrescentar
  asserções de que o comprovante de uma transferência nova e o de um reenvio trazem
  `Pagador.Documento` = CPF do pagador mascarado `•••.XXX.XXX-••` (FR-002a) e
  `ChaveIdempotencia` preenchida
- [ ] T002 [P] Em `back/tests/Bank.IntegrationTests/TransferenciasTests.cs`, conferir no corpo do
  `POST /api/v1/transferencias` (201 e 200) o campo `pagador.documento` mascarado e
  `chaveIdempotencia` igual à enviada

### Implementação da fundação

- [ ] T003 [P] Acrescentar `TransferenciaNaoEncontrada` em
  `back/src/Bank.Domain/Transferencias/ErrosTransferencia.cs`: código
  `transferencia-nao-encontrada`, mensagem "Transferência não encontrada.",
  `TipoErro.NaoEncontrado` (data-model "Erro novo")
- [ ] T004 Em `back/src/Bank.Application/Abstracoes/ITransferencias.cs`: `Comprovante.ChaveIdempotencia`
  passa a `Guid?`; `PagadorComprovante(Guid UsuarioId, string Nome, string Documento)`;
  `ITransferenciaRepositorio.ObterComprovantePorIdAsync(Guid transferenciaId, CancellationToken)`
  com XML doc "documento do pagador e do recebedor sem máscara; a Application mascara"
- [ ] T005 [P] Em `back/src/Bank.Application/Observabilidade/MetricasTransferencias.cs`, criar o
  contador `bank.transferencias.consultas` (`{consulta}`, "Consultas de transferência por
  identificador, por resultado.") e `RegistrarConsulta(string resultado)` com o rótulo
  `resultado` (`encontrada` · `nao-encontrada` · `invalida`)
- [ ] T006 Em `back/src/Bank.Infrastructure/Persistencia/TransferenciaRepositorio.cs`, extrair a
  projeção do comprovante para uma consulta base (transferência + carteiras + usuários, incluindo
  `pagador.Documento`) usada por `ObterComprovantePorChaveAsync` e pelo novo
  `ObterComprovantePorIdAsync` (filtro por `t.Id`, `AsNoTracking`)
- [ ] T007 Em `back/src/Bank.Application/Transferencias/RealizarTransferencia.cs`, preencher
  `Pagador.Documento` com `Documento.Ocultar` do documento do titular
  (`IUsuarioConsultas.ObterTitularAsync`, lido **antes** de iniciar a transação, junto com a carteira
  do pagador, para não alongar o bloqueio) na transferência nova e mascarar o documento vindo do
  repositório no reenvio (R4)
- [ ] T008 Em `back/src/Bank.Api/Endpoints/TransferenciasEndpoints.cs`:
  `PagadorResposta(Guid UsuarioId, string Nome, string Documento)`, `ComprovanteResposta.ChaveIdempotencia`
  como `Guid?` e `ComprovanteResposta.De` mapeando o documento
- [ ] T009 Atualizar `back/tests/Bank.UnitTests/Application/DublesTransferencia.cs` para o novo
  método do repositório (comprovantes guardados por id) e para o `PagadorComprovante` com
  documento; `dotnet build` e `dotnet test` verdes (T001–T002 passando)

**Checkpoint**: envio com `pagador.documento` mascarado; fundação pronta

---

## Phase 3: User Story 1 - Pagador reabre o comprovante (Priority: P1) 🎯 MVP

**Goal**: o pagador consulta a transferência e recebe o mesmo comprovante do envio

**Independent Test**: transferir, autenticar de novo e consultar; comparar com o comprovante do
envio (quickstart, cenário 2, linha A)

### Tests for User Story 1

- [ ] T010 [P] [US1] Criar `back/tests/Bank.UnitTests/Application/ConsultarTransferenciaTests.cs`:
  pagador → `Resultado.Ok` com chave preenchida, `Pagador.Documento` mascarado, `Recebedor.Documento`
  completo e métrica `encontrada` registrada uma vez
- [ ] T011 [P] [US1] Criar `back/tests/Bank.IntegrationTests/ConsultaTransferenciaTests.cs`: PF
  transfere para PJ; o pagador, com um **novo login**, faz
  `GET /api/v1/transferencias/{id}` → 200 com corpo igual ao do `POST` (todos os campos); duas
  consultas seguidas devolvem o mesmo corpo; saldos, total de lançamentos das duas carteiras e
  total de `outbox_eventos` inalterados (SC-006); 20 consultas seguidas com pelo menos 19 abaixo
  de 1 s (SC-004)

### Implementation for User Story 1

- [ ] T012 [US1] Criar `back/src/Bank.Application/Transferencias/ConsultarTransferencia.cs`:
  `ConsultarTransferenciaConsulta(Guid UsuarioId, string? TransferenciaId)`;
  `ExecutarAsync` → valida o UUID (R2), lê `ObterComprovantePorIdAsync`, aplica a regra de acesso
  do data-model e mascara o documento do pagador; registra a métrica por resultado
- [ ] T013 [US1] Registrar `ConsultarTransferencia` em
  `back/src/Bank.Application/DependencyInjection.cs` (`AddScoped`)
- [ ] T014 [US1] Em `back/src/Bank.Api/Endpoints/TransferenciasEndpoints.cs`, mapear
  `GET /{transferenciaId}` (texto, sem restrição de tipo) com a política `UsuarioAutenticado`,
  nome `ConsultarTransferencia`, resumo "Reabre o comprovante de uma transferência (pagador ou
  recebedor).", `Produces<ComprovanteResposta>(200)`, `ProducesValidationProblem()` e problemas
  401/403/404; claim ausente → `Forbid`; resultado → `ComprovanteResposta.De` ou `ParaProblema()`

**Checkpoint**: pagador reabre o comprovante — **MVP**

---

## Phase 4: User Story 2 - Recebedor consulta uma transferência recebida (Priority: P1)

**Goal**: o recebedor (PF ou PJ) vê o comprovante sem a chave do pagador

**Independent Test**: quickstart, cenário 2, linha B

### Tests for User Story 2

- [ ] T015 [P] [US2] Acrescentar a `ConsultarTransferenciaTests.cs`: recebedor →
  `ChaveIdempotencia` nula, demais campos iguais aos do pagador, documento do pagador mascarado
- [ ] T016 [P] [US2] Acrescentar a `ConsultaTransferenciaTests.cs`: PJ recebedora e PF recebedora
  → 200 com `chaveIdempotencia: null`, `pagador.documento` mascarado e `recebedor.documento`
  completo (o próprio); o JSON não contém a chave enviada pelo pagador (SC-003)

### Implementation for User Story 2

- [ ] T017 [US2] Garantir que T015–T016 passam com a regra de acesso de T012 (`ChaveIdempotencia =
  null` para o recebedor em `ConsultarTransferencia.cs`); ajustar o caso de uso só se falharem

**Checkpoint**: pagador e recebedor consultam

---

## Phase 5: User Story 3 - Terceiros não descobrem transferências alheias (Priority: P1)

**Goal**: 404 idêntico para terceiro e inexistente; 400, 401 e 403 padronizados

**Independent Test**: quickstart, cenário 2, linhas C, UUID aleatório, `abc` e sem token

### Tests for User Story 3

- [ ] T018 [P] [US3] Acrescentar a `ConsultarTransferenciaTests.cs`: terceiro e inexistente →
  mesmo `Erro` (`transferencia-nao-encontrada`) e métrica `nao-encontrada`; `"abc"`, vazio e
  nulo → erro de validação no campo `transferenciaId` e métrica `invalida`; UUID em maiúsculas
  e sem hífens aceito; o repositório não é chamado com id inválido
- [ ] T019 [P] [US3] Acrescentar a `ConsultaTransferenciaTests.cs`: terceiro (PF e PJ) → 404
  `urn:bank:problema:transferencia-nao-encontrada`; UUID aleatório → 404 com `status`, `type`,
  `title` e `detail` **idênticos** aos do terceiro (só o `traceId` muda); `abc` → 400 com
  `errors.transferenciaId`; sem token e token inválido → 401; token sem claims
  (`TokensDeTeste`) → 403 `acesso-negado`; `GET /api/v1/transferencias/extrato` continua
  respondendo o extrato (precedência da rota literal)

### Implementation for User Story 3

- [ ] T020 [US3] Em `ConsultarTransferencia.cs`, garantir um único caminho de recusa para
  inexistente e terceiro (mesma leitura, mesmo `Erro`), e a mensagem de validação "Informe um
  identificador de transferência válido." em `transferenciaId` (R3)

**Checkpoint**: todas as histórias completas

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T021 [P] Em `back/tests/Bank.IntegrationTests/OpenApiTests.cs`, conferir a operação
  `ConsultarTransferencia` em `/api/v1/transferencias/{transferenciaId}` com `Bearer` e respostas
  200/400/401/403/404, e `ComprovanteResposta` com `pagador.documento` e `chaveIdempotencia`
  anulável (FR-008)
- [ ] T022 [P] Em `back/tests/Bank.IntegrationTests/TelemetryTests.cs`, conferir
  `bank.transferencias.consultas` com os rótulos `encontrada`, `nao-encontrada` e `invalida`, sem
  outros rótulos; em `DadosSensiveisTelemetriaTests.cs`, incluir consultas (pagador, recebedor,
  terceiro) e conferir que documento, nome e e-mail não aparecem (SC-005)
- [ ] T023 [P] Atualizar `docs/desenvolvimento.md` (seção "Transferências e extrato") com a
  consulta por identificador, a regra de acesso e o documento mascarado do pagador; e acrescentar
  em `specs/006-transferencias/contracts/transferencias-api.md` uma nota "Versão atual: v1.1.0 em
  specs/008" no Histórico
- [ ] T024 Executar `dotnet build`, `dotnet test` (unitários, integração e arquitetura) e o
  [quickstart.md](quickstart.md) cenários 2 e 3 contra o Compose; registrar desvios na spec, no
  plano ou no contrato no mesmo PR (Princípio I)

---

## Dependencies & Execution Order

- **Foundational (2)** → US1 (3) → US2 (4) e US3 (5) (estendem o mesmo caso de uso e os mesmos
  arquivos de teste) → Polish (6)
- Dentro de cada história: testes primeiro (falhando), depois implementação

### Parallel Opportunities

- T001, T002, T003, T005 em paralelo
- T010 e T011; T015 e T016; T018 e T019 em paralelo (arquivos de unidade e de integração)
- T021, T022 e T023 em paralelo

## Parallel Example: User Story 1

```text
T010 ConsultarTransferenciaTests.cs   T011 ConsultaTransferenciaTests.cs
```

---

## Implementation Strategy

### MVP First

1. Fundação (T001–T009)
2. US1 (T010–T014) → pagador reabre o comprovante
3. **PARAR e VALIDAR** com o quickstart (linha A)

### Incremental Delivery

1. US2 → recebedor
2. US3 → privacidade e erros
3. Polish → OpenAPI, telemetria, documentação

### Commits

Conventional Commits por fase, escopo `consulta-transferencia` (por exemplo
`feat(consulta-transferencia): ...`, `test(consulta-transferencia): ...`); PR com `Closes #36`.
Depois do merge, a fase 10 da spec 007 (T051–T054) é feita no PR que finaliza a issue #32.
