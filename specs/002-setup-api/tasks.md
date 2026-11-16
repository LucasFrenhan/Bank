---

description: "Lista de tarefas da feature 002-setup-api"
---

# Tasks: Setup da API, Documentação Interativa e Observabilidade

**Input**: Design documents from `specs/002-setup-api/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/api-base.md](contracts/api-base.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios por esta spec (FR-013, FR-015) e pela constituição (Princípios II
e VII). Em cada história, os testes são escritos primeiro e devem falhar antes da
implementação.

**Organization**: tarefas agrupadas por história de usuário (US1–US5).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (abreviado abaixo como `backend/`): projetos em
`backend/src/<Projeto>/` e testes em `backend/tests/<Projeto>/`. Arquivos de raiz
(`global.json`, `docker-compose.yml`, `.env.example`, `.github/`) usam o caminho completo.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: SDK, build comum e solução com os 7 projetos

- [x] T001 [P] Criar `global.json` na raiz com `"sdk": { "version": "10.0.400", "rollForward": "latestPatch" }` (R1)
- [x] T002 [P] Criar `backend/Directory.Build.props`: `TargetFramework net10.0`,
  `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`,
  `AnalysisLevel latest-recommended`, `InvariantGlobalization true` (R2)
- [x] T003 [P] Criar `backend/Directory.Packages.props` com
  `ManagePackageVersionsCentrally` e exatamente as versões da tabela do R3
  (`Microsoft.AspNetCore.OpenApi` 10.0.15, `Scalar.AspNetCore` 2.21.0, `OpenTelemetry.*`
  1.21.0, `xunit.v3` 4.0.3, `xunit.runner.visualstudio` 4.0.1, `Microsoft.NET.Test.Sdk`
  18.12.0, `Microsoft.AspNetCore.Mvc.Testing` 10.0.15, `TngTech.ArchUnitNET` 0.13.5,
  `OpenTelemetry.Exporter.InMemory` 1.21.0)
- [x] T004 Criar `backend/Bank.slnx` e os projetos `Bank.Domain`, `Bank.Application`,
  `Bank.Infrastructure` (classlib), `Bank.Api` (web), `Bank.UnitTests`,
  `Bank.IntegrationTests` e `Bank.ArchitectureTests` (xUnit v3), com as referências da
  tabela do R2 (Api → Application e Infrastructure; Infrastructure → Application e Domain;
  Application → Domain; testes conforme R2); os `.csproj` de teste são escritos à mão
  (o template `xunit3` não vem com o SDK); remover arquivos de exemplo dos templates
  (depende de T001–T003)
- [x] T005 Executar `dotnet build backend/Bank.slnx` e confirmar build sem avisos

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: composição das camadas, fábrica de testes e CI

- [x] T006 [P] Criar `backend/src/Bank.Domain/DomainAssemblyMarker.cs`,
  `backend/src/Bank.Application/DependencyInjection.cs` (`AddApplication()`) e
  `backend/src/Bank.Infrastructure/DependencyInjection.cs` (`AddInfrastructure()`), ainda
  sem registros (R2, R9 — evitam regras "vazias")
- [x] T007 Criar `backend/src/Bank.Api/Program.cs` como raiz de composição mínima
  (`AddApplication()`, `AddInfrastructure()`), terminando com `public partial class Program;`
  para uso pelo `WebApplicationFactory`
- [x] T008 [P] Criar `backend/tests/Bank.IntegrationTests/BankApiFactory.cs`
  (`WebApplicationFactory<Program>` com ambiente `Testing`)
- [x] T009 [P] Criar `.github/workflows/ci.yml` (R10): `pull_request` e `push` para `main`
  sem filtro de paths; job `build-test`; `actions/checkout@v7`; `actions/setup-dotnet@v6`
  com `global-json-file: global.json`; `dotnet restore`, `dotnet build --no-restore -c
  Release` e `dotnet test --no-build -c Release` em `src/backend/Bank.slnx`

**Checkpoint**: solução compila; histórias podem começar

---

## Phase 3: User Story 1 - Executar a API localmente (Priority: P1) 🎯 MVP

**Goal**: API executável pelo SDK e pelo Compose, com `/health`.

**Independent Test**: [quickstart — Cenários 2 e 6](quickstart.md#cenário-2--api-pelo-sdk-us1-fr-001-fr-002).

### Tests for User Story 1

- [x] T010 [US1] Criar `backend/tests/Bank.IntegrationTests/HealthEndpointTests.cs`:
  `GET /health` → 200, corpo `Healthy`, sem autenticação; com a fábrica registrando um
  health check que falha (`ConfigureTestServices`) → 503 `Unhealthy` (deve falhar antes
  de T011)

### Implementation for User Story 1

- [x] T011 [US1] Criar `backend/src/Bank.Api/Endpoints/HealthEndpoints.cs`
  (`AddHealthChecks()` + `MapHealthChecks("/health")`) e registrá-lo em `Program.cs`
- [x] T012 [P] [US1] Criar `backend/src/Bank.Api/Properties/launchSettings.json` (perfil
  `http`: `http://localhost:5080`, `ASPNETCORE_ENVIRONMENT=Development`,
  `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`) e `appsettings.json` /
  `appsettings.Development.json` com níveis de log (R8, data-model)
- [x] T013 [P] [US1] Criar `backend/Dockerfile` (multi-stage `sdk:10.0.404-alpine3.24` →
  `aspnet:10.0.15-alpine3.24`, usuário `app`, porta 8080) e `backend/.dockerignore`
  (`bin/`, `obj/`, `tests/`) (R8)
- [x] T014 [US1] Adicionar o serviço `api` em `docker-compose.yml`: build
  `src/backend`, `ASPNETCORE_ENVIRONMENT=Development`,
  `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889`, porta
  `127.0.0.1:${API_PORT:-8080}:8080`, `depends_on` de `postgres` e `rabbitmq` com
  `condition: service_healthy` e de `aspire-dashboard` com `condition: service_started`
  (R8), healthcheck
  `wget -q -O /dev/null http://localhost:8080/health`; adicionar `API_PORT=8080` ao
  `.env.example` (depende de T013)
- [x] T015 [P] [US1] Incluir `src/backend/**` e `global.json` no filtro de paths de
  `.github/workflows/infra-local.yml` (R10)
- [x] T016 [US1] Executar os Cenários 2 e 6 do `quickstart.md` (SDK e Compose, incluindo
  ordem de inicialização, `api (healthy)` e porta ocupada resolvida por `--urls` /
  `API_PORT`)

**Checkpoint**: MVP — API executável e saudável

---

## Phase 4: User Story 2 - Documentação interativa (Priority: P1)

**Goal**: OpenAPI nativo e Scalar em `/scalar/v1`, fora de produção.

**Independent Test**: [quickstart — Cenário 3](quickstart.md#cenário-3--documentação-interativa-us2-fr-003005-sc-002).

### Tests for User Story 2

- [x] T017 [US2] Criar `backend/tests/Bank.IntegrationTests/OpenApiTests.cs`:
  `/openapi/v1.json` → 200 com `/health` e sem `/__test`; `/scalar/v1` → 200 HTML; ambos →
  404 com a fábrica configurada para `Production` (deve falhar antes de T018)

### Implementation for User Story 2

- [x] T018 [US2] Criar `backend/src/Bank.Api/OpenApi/OpenApiExtensions.cs`
  (`AddOpenApi()`; `MapOpenApi()` + `MapScalarApiReference()` só quando
  `!IsProduction()`) e registrar em `Program.cs`; declarar no `/health` as respostas 200 e
  503 (R4)
- [x] T019 [US2] Executar o Cenário 3 do `quickstart.md`, incluindo executar `/health` pela
  UI do Scalar (SC-002)

**Checkpoint**: contrato navegável

---

## Phase 5: User Story 3 - Telemetria (Priority: P2)

**Goal**: traces, métricas e logs via OTLP como `bank-api`.

**Independent Test**: [quickstart — Cenário 5](quickstart.md#cenário-5--telemetria-us3-fr-006009-sc-003-sc-006).

### Tests for User Story 3

- [ ] T020 [US3] Criar `backend/tests/Bank.IntegrationTests/TelemetryTests.cs` usando o
  exportador em memória (`OpenTelemetry.Exporter.InMemory`, R3): uma requisição a
  `/nao-existe` exporta um span com rota/status e recurso `service.name = bank-api`; uma
  requisição a `/health` não exporta span (filtro, R5) (deve falhar antes de T021)

### Implementation for User Story 3

- [ ] T021 [US3] Criar `backend/src/Bank.Api/Observability/TelemetryExtensions.cs`
  (R5): recurso `bank-api`; tracing AspNetCore (filtro `/health`, `/openapi`, `/scalar`) +
  HttpClient; métricas AspNetCore + HttpClient + meter `System.Runtime`; `WithLogging()`
  com `IncludeFormattedMessage` e `IncludeScopes`; `UseOtlpExporter()` exceto no ambiente
  `Testing`; registrar em `Program.cs`
- [ ] T022 [US3] Ajustar `appsettings.json` para que os logs por requisição
  (`Microsoft.AspNetCore.Hosting.Diagnostics`) saiam em `Information`, garantindo logs
  correlacionados por requisição sem registrar corpos ou cabeçalhos (Princípio VI)
- [ ] T023 [US3] Executar o Cenário 5 do `quickstart.md` (traces, logs correlacionados,
  métricas `http.server.request.duration` e `dotnet.*`; coletor parado → 20 respostas 200)

**Checkpoint**: API observável

---

## Phase 6: User Story 4 - Erros padronizados (Priority: P2)

**Goal**: `ProblemDetails` com `traceId` em toda resposta de erro.

**Independent Test**: [quickstart — Cenário 4](quickstart.md#cenário-4--erros-padronizados-us4-fr-010-sc-004).

### Tests for User Story 4

- [ ] T024 [P] [US4] Criar
  `backend/tests/Bank.UnitTests/ErrorHandling/ProblemDetailsTraceIdCustomizerTests.cs`:
  com `Activity` ativa, `traceId` = trace ID em 32 hex; sem `Activity`, usa
  `TraceIdentifier`
- [ ] T025 [P] [US4] Criar `backend/tests/Bank.IntegrationTests/ErrorHandlingTests.cs`:
  404 (rota inexistente), 405 (`POST /health`), 400 (`POST /__test/eco` com JSON
  malformado) e 500 (`GET /__test/falha`) com `application/problem+json`, `status`
  correto e `traceId` de 32 hex; o 500 não contém "segredo-interno", nome de exceção nem
  stack trace (SC-004)

### Implementation for User Story 4

- [ ] T026 [US4] Criar `backend/src/Bank.Api/ErrorHandling/ProblemDetailsTraceIdCustomizer.cs`
  e `ErrorHandlingExtensions.cs` (`AddProblemDetails` com o customizador,
  `RouteHandlerOptions.ThrowOnBadRequest = false`, `UseExceptionHandler()`,
  `UseStatusCodePages()`) e registrar em `Program.cs` antes dos endpoints (R6)
- [ ] T027 [US4] Criar `backend/src/Bank.Api/Testing/TestEndpoints.cs`
  (`GET /__test/falha`, `POST /__test/eco`, `ExcludeFromDescription()`), mapeado em
  `Program.cs` somente no ambiente `Testing` (R7)
- [ ] T028 [US4] Executar o Cenário 4 do `quickstart.md` e localizar no dashboard o
  `traceId` de uma resposta de erro (US4, cenário 3)

**Checkpoint**: padrão de erro estabelecido para todas as specs

---

## Phase 7: User Story 5 - Base arquitetural verificada (Priority: P3)

**Goal**: testes de arquitetura e CI protegendo a direção das camadas.

**Independent Test**: [quickstart — Cenário 7](quickstart.md#cenário-7--violação-de-camada-us5-sc-005).

- [ ] T029 [US5] Criar `backend/tests/Bank.ArchitectureTests/LayerDependencyTests.cs`
  com as 4 regras do R9 (uma por teste), carregando os assemblies pelos tipos marcadores
- [ ] T030 [US5] Executar o Cenário 7 do `quickstart.md`: violação proposital em
  `Bank.Api.Endpoints` → teste falha localmente; reverter

**Checkpoint**: todas as histórias funcionais e verificadas

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T031 [P] Adicionar a seção "Backend (API)" ao `README.md`: requisitos (SDK 10),
  build, testes, `dotnet run`, URLs (`/health`, `/scalar/v1`, porta 5080 no SDK e 8080 no
  Compose) e link para `specs/002-setup-api/contracts/api-base.md`; atualizar a tabela do
  ambiente local com o serviço `api` e o status do README
- [ ] T032 [P] Atualizar `specs/001-infra-local/contracts/ambiente-local.md` para a
  versão 1.1.0 (mudança compatível): novo serviço `api` com portas e dependências
  (documentação viva, Princípio I)
- [ ] T033 Executar o Cenário 1 do `quickstart.md` (build Release sem avisos e todos os
  testes), conferir que todos os pacotes de `Directory.Packages.props` estão
  justificados no R3 e cronometrar o onboarding a partir de um clone limpo seguindo só o
  README até o `/health` responder (SC-001: ≤ 10 min sem downloads)
- [ ] T034 Fazer push da branch, confirmar `ci / build-test` e `infra-local` verdes e abrir
  o PR com `Closes #3`; validar o SC-005 no CI com um PR rascunho a partir de
  `tmp/sc005-violacao` (CI vermelho), fechado sem merge e com a branch apagada (R9)
- [ ] T035 Após o merge, adicionar `build-test` como check obrigatório da proteção da
  `main` (FR-014)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → histórias
- **US1 (3)**: depende da Phase 2
- **US2 (4)**: depende de US1 (documenta o `/health`)
- **US3 (5)**: depende da Phase 2; validação manual depende de US1
- **US4 (6)**: depende da Phase 2; T028 depende de US3 (busca do `traceId` no dashboard)
- **US5 (7)**: depende da Phase 2 e de existir `Bank.Api.Endpoints` (T011)
- **Polish (8)**: depende de todas as histórias

```text
T001,T002,T003 → T004 → T005 → T006 → T007 ─┬→ US1 (T010→T011, T012, T013→T014, T015 → T016)
                         T008, T009 ────────┤      └→ US2 (T017→T018→T019)
                                            ├→ US3 (T020→T021→T022→T023)
                                            ├→ US4 (T024,T025 → T026→T027 → T028)
                                            └→ US5 (T029→T030) ──→ Polish (T031..T035)
```

### Within Each User Story

- Testes antes da implementação (devem falhar primeiro)
- `Program.cs` é editado por várias histórias: essas tarefas são sequenciais

### Parallel Opportunities

- T001, T002, T003
- T006, T008, T009
- T012, T013, T015 (US1)
- T024, T025 (US4)
- T031, T032 (Polish)

---

## Parallel Example: User Story 4

```text
Task: "T024 ProblemDetailsTraceIdCustomizerTests em backend/tests/Bank.UnitTests"
Task: "T025 ErrorHandlingTests em backend/tests/Bank.IntegrationTests"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phases 1 e 2 (T001–T009)
2. US1 (T010–T016)
3. **Validar**: API respondendo `/health` pelo SDK e pelo Compose

### Incremental Delivery

1. Setup + Foundational → solução compilando, CI criado
2. US1 → API executável (MVP)
3. US2 → contrato navegável (entregável explícito da issue)
4. US3 → telemetria (entregável explícito da issue)
5. US4 → padrão de erro
6. US5 → proteção arquitetural
7. Polish → README, contrato 001, PR e check obrigatório

### Commits

Um commit por fase, por exemplo:

- `build(setup-api): cria solução .NET 10 com camadas e projetos de teste`
- `ci(setup-api): adiciona pipeline build-test`
- `feat(setup-api): adiciona endpoint de saúde e serviço api no compose`
- `feat(setup-api): publica OpenAPI e Scalar fora de produção`
- `feat(setup-api): exporta telemetria via OTLP`
- `feat(setup-api): padroniza erros com ProblemDetails e traceId`
- `test(setup-api): adiciona testes de arquitetura das camadas`
- `docs(setup-api): documenta backend no README`

---

## Notes

- Marcar cada tarefa como `[x]` ao concluí-la
- T016, T019, T023, T028 e T030 exigem Docker em execução
- T035 altera a proteção da `main` (configuração do GitHub)
