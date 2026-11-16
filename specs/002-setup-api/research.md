# Research: Setup da API, Documentação Interativa e Observabilidade

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-16

Versões consultadas no NuGet, no MCR e nos metadados de release do .NET em 2026-11-16.
Regra geral (mesma da spec 001): versão completa fixada, evitando releases `.0` de major
publicadas há poucos dias.

## R1. SDK e runtime

- **Decision**: .NET 10 (LTS), exigido pela constituição. `global.json` na **raiz** do
  repositório com `"version": "10.0.400"` e `"rollForward": "latestPatch"` — aceita
  10.0.402 (instalado localmente) e 10.0.404 (último), mas nunca o SDK 11 instalado na
  mesma máquina. Runtime 10.0.15.
- **Rationale**: a raiz garante o mesmo SDK para qualquer comando `dotnet` do repositório
  (local, CI e Dockerfile). `latestPatch` evita exigir uma atualização de SDK a cada patch.
- **Alternatives considered**: .NET 11 (instalado, recém-lançado, STS) — rejeitado pela
  constituição; `global.json` em `src/backend` — rejeitado, pois comandos executados na
  raiz (CI, quickstart) usariam o SDK 11.

## R2. Estrutura da solução

- **Decision**: solução `src/backend/Bank.slnx` (formato XML padrão do .NET 10) com:

  | Projeto | Tipo | Referencia |
  | --- | --- | --- |
  | `src/Bank.Domain` | classlib | — |
  | `src/Bank.Application` | classlib | Domain |
  | `src/Bank.Infrastructure` | classlib | Application, Domain |
  | `src/Bank.Api` | web (Minimal API) | Application, Infrastructure (somente composição/DI) |
  | `tests/Bank.UnitTests` | xUnit | Api, Application, Domain |
  | `tests/Bank.IntegrationTests` | xUnit | Api |
  | `tests/Bank.ArchitectureTests` | xUnit | todos os projetos de `src/` |

- **Build comum**: `src/backend/Directory.Build.props` (`net10.0`, `Nullable`,
  `ImplicitUsings`, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`) e
  `src/backend/Directory.Packages.props` (Central Package Management — uma única fonte de
  versões, facilitando a justificativa exigida pela constituição).
- **Composição**: cada camada expõe uma extensão de registro (`AddApplication()`,
  `AddInfrastructure()`); `Program.cs` é a raiz de composição e o único ponto da API que
  conhece `Bank.Infrastructure`.
- **Alternatives considered**: `.sln` clássico — substituído pelo `.slnx` no .NET 10;
  projeto único com pastas — rejeitado pela opção A do esclarecimento.

## R3. Pacotes NuGet (justificativa exigida pela constituição)

| Pacote | Versão | Projeto | Justificativa |
| --- | --- | --- | --- |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.15 | Application, Infrastructure | `IServiceCollection` para as extensões `AddApplication()`/`AddInfrastructure()` sem acoplar essas camadas ao framework do ASP.NET Core (identificado na implementação). |
| `Microsoft.AspNetCore.OpenApi` | 10.0.15 | Api | Geração nativa do documento OpenAPI (FR-003, issue #3). |
| `Scalar.AspNetCore` | 2.21.0 | Api | UI interativa em `/scalar/v1` (FR-004, issue #3). A 3.0.0 saiu hoje (major nova) e é evitada. |
| `OpenTelemetry.Extensions.Hosting` | 1.21.0 | Api | Integração do SDK OpenTelemetry com o host (FR-006). |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.21.0 | Api | Exportação OTLP dos três sinais via `UseOtlpExporter()` (FR-006). |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.21.0 | Api | Spans de requisições recebidas com filtro de rotas de ruído (FR-007, edge case). |
| `OpenTelemetry.Instrumentation.Http` | 1.21.0 | Api | Spans de chamadas HTTP de saída (FR-007). |
| `OpenTelemetry.Exporter.InMemory` | 1.21.0 | IntegrationTests | Captura em memória dos spans exportados, para testar o que de fato é exportado, inclusive o filtro de rotas (o filtro não impede a criação da `Activity`, só a gravação). |
| `xunit.v3` | 4.0.3 | testes | Framework de testes (padrão do ecossistema .NET). |
| `xunit.runner.visualstudio` | 4.0.1 | testes | Execução via `dotnet test`. |
| `Microsoft.NET.Test.Sdk` | 18.12.0 | testes | Host de testes do `dotnet test`. |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.15 | IntegrationTests | `WebApplicationFactory` para testes de integração em memória (FR-015). |
| `TngTech.ArchUnitNET` | 0.13.5 | ArchitectureTests | Regras de dependência entre camadas e tipos (FR-013, Princípio II). |

- **Não adotados**:
  - `OpenTelemetry.Instrumentation.Runtime`: desde o .NET 9 o runtime publica o meter
    nativo `System.Runtime`; basta `AddMeter("System.Runtime")`.
  - `TngTech.ArchUnitNET.xUnitV3`: o pacote principal já oferece `HasNoViolations`; o
    adaptador acrescentaria dependência de versão do xUnit sem ganho.
  - Biblioteca de asserções (FluentAssertions/AwesomeAssertions/Shouldly): o `Assert` do
    xUnit cobre os testes desta spec; a FluentAssertions 8+ tem licença comercial. Pode ser
    reavaliada por uma spec futura, com justificativa.
  - Testcontainers: a API ainda não usa banco nem mensageria; entra com a primeira spec
    que usar PostgreSQL ou RabbitMQ.
  - `coverlet.collector`: cobertura não é critério desta spec.

## R4. OpenAPI e Scalar

- **Decision**: `AddOpenApi()` com documento `v1`; `MapOpenApi()` (`/openapi/v1.json`) e
  `MapScalarApiReference()` (`/scalar/v1`) mapeados **somente** quando
  `!app.Environment.IsProduction()` (FR-005).
- O `/health` é publicado no contrato, para atender o SC-002 ("100% dos endpoints
  publicados aparecem"). Somente os endpoints de teste de falhas (R7) usam
  `ExcludeFromDescription()`, pois não fazem parte do contrato.
- O documento descreve respostas de erro com `ProducesProblem(...)` para que o formato
  `ProblemDetails` apareça no contrato (US2, cenário 3).

## R5. OpenTelemetry

- **Decision**:
  - `builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("bank-api"))`
    com `.WithTracing(AspNetCore + HttpClient)`, `.WithMetrics(AspNetCore + HttpClient +
    meter "System.Runtime")`, `.WithLogging()` e `.UseOtlpExporter()`.
  - O endpoint vem da variável padrão `OTEL_EXPORTER_OTLP_ENDPOINT` (lida da configuração
    pelo exportador): `http://localhost:4317` ao rodar pelo SDK (via `launchSettings.json`)
    e `http://aspire-dashboard:18889` no Compose (FR-011).
  - Logs: `IncludeFormattedMessage` e `IncludeScopes` habilitados; mensagens com templates
    nomeados (Princípio VI).
  - Filtro de traces: requisições a `/health`, `/openapi` e `/scalar` não geram spans
    (edge case de ruído); métricas continuam contabilizando-as.
- **Ambiente `Testing`**: o `UseOtlpExporter()` não é registrado (não há coletor nos
  testes nem no CI); os testes de telemetria acrescentam o exportador em memória.
- **Resiliência (FR-009, SC-006)**: o exportador OTLP usa processamento em lote em
  segundo plano; falhas de conexão são descartadas e registradas apenas no
  `EventSource` interno do SDK, sem afetar as requisições. Comprovado na validação
  derrubando o coletor.
- **Alternatives considered**: `AddSource("Microsoft.AspNetCore")` sem o pacote de
  instrumentação — o .NET 10 emite as atividades nativamente, mas o filtro e o
  enriquecimento ficariam manuais; OpenTelemetry Collector intermediário — desnecessário
  com o Aspire Dashboard (spec 001, R1).

## R6. Erros padronizados (ProblemDetails)

- **Decision**:
  - `AddProblemDetails()` com `CustomizeProblemDetails` que define a extensão `traceId`
    como `Activity.Current.TraceId` em hexadecimal (32 caracteres). O padrão do ASP.NET
    Core grava o `traceparent` completo (`00-<trace>-<span>-00`), que não é localizável
    diretamente pela busca do dashboard (US4, cenário 3). Sem atividade, usa
    `HttpContext.TraceIdentifier`.
  - `UseExceptionHandler()` (sem página de desenvolvedor em nenhum ambiente, para que o
    formato seja o mesmo em todos — SC-004) e `UseStatusCodePages()` para converter
    respostas de erro sem corpo (404, 405, 400 de binding) em `ProblemDetails`.
  - `RouteHandlerOptions.ThrowOnBadRequest = false` em todos os ambientes, para que JSON
    malformado resulte em 400 + `ProblemDetails` (e não em exceção tratada como 500).
  - A lógica de customização fica em uma classe própria do `Bank.Api`
    (`ProblemDetailsTraceIdCustomizer`), coberta por teste unitário.
- **Alternatives considered**: middleware próprio de erros — duplicaria o que o framework
  já oferece; `IExceptionHandler` customizado — desnecessário enquanto não houver exceções
  de domínio mapeáveis (virá com a primeira spec de negócio).

## R7. Endpoints de teste de falhas

- **Problema**: US4 e SC-004 exigem provar 500 sem detalhes internos e 400 por JSON
  malformado, mas esta spec não tem endpoints de negócio.
- **Decision**: `Bank.Api` contém `TestEndpoints.MapTestEndpoints()` com
  `GET /__test/falha` (lança exceção com mensagem "segredo-interno") e
  `POST /__test/eco` (recebe um corpo JSON). Mapeados **somente** quando o ambiente é
  `Testing` (usado pelos testes de integração) e excluídos do OpenAPI.
- **Alternatives considered**: registrar endpoints a partir do projeto de testes — um
  `IStartupFilter` roda antes do pipeline da aplicação, portanto fora do
  `UseExceptionHandler`, e não testaria o comportamento real.

## R8. Execução local e no Compose

- **Pelo SDK**: `dotnet run --project src/backend/src/Bank.Api` com perfil `http` em
  `launchSettings.json`: `http://localhost:5080`, `ASPNETCORE_ENVIRONMENT=Development`,
  `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`. Outra porta: `--urls`.
- **No Compose**: serviço `api` construído por `src/backend/Dockerfile` (multi-stage:
  `sdk:10.0.404-alpine3.24` → `aspnet:10.0.15-alpine3.24`, usuário não-root `app`, porta
  8080), publicado em `127.0.0.1:${API_PORT:-8080}`, com
  `depends_on` de `postgres` e `rabbitmq` em `service_healthy` (FR-012 — comprova o
  adiado da spec 001) e de `aspire-dashboard` em `service_started`, para que um coletor
  indisponível não impeça a API de subir (caso de borda da spec, SC-006).
- **Healthcheck**: a variante Alpine do `aspnet` inclui o `wget` do BusyBox:
  `wget -q -O /dev/null http://localhost:8080/health`. (A variante Debian não tem `curl`
  nem `wget`.)
- **Ambiente no Compose**: `Development`, para disponibilizar o Scalar localmente.
- `.env.example` ganha `API_PORT=8080`; `src/backend/.dockerignore` exclui `bin/`, `obj/`
  e testes.

## R9. Testes de arquitetura

Regras com ArchUnitNET (uma por teste, para mensagens claras):

1. `Bank.Domain` não depende de `Bank.Application`, `Bank.Infrastructure`, `Bank.Api`,
   `Microsoft.AspNetCore*` nem `Microsoft.EntityFrameworkCore*`.
2. `Bank.Application` não depende de `Bank.Infrastructure`, `Bank.Api` nem
   `Microsoft.EntityFrameworkCore*`.
3. `Bank.Infrastructure` não depende de `Bank.Api`.
4. Tipos de `Bank.Api` em `Bank.Api.Endpoints*` não dependem de `Bank.Infrastructure*`
   nem de `Microsoft.EntityFrameworkCore*` (endpoints não acessam `DbContext` —
   Princípio II). Só a raiz de composição (`Program`) referencia a infraestrutura.

- Para que as regras não passem "por vacuidade", cada camada tem ao menos um tipo
  (as extensões de registro e um marcador de assembly).
- **SC-005**: validado introduzindo uma violação proposital localmente e, no CI, por um PR
  rascunho a partir de uma branch descartável (`tmp/sc005-violacao`), fechado sem merge —
  assim a violação não entra no histórico da `main`.

## R10. CI

- **Decision**: `.github/workflows/ci.yml` em todo `pull_request` e `push` para `main`,
  **sem filtro de paths** (FR-014): `actions/checkout@v7`, `actions/setup-dotnet@v6` com
  `global-json-file: global.json`, `dotnet restore`, `dotnet build --no-restore -c
  Release`, `dotnet test --no-build -c Release`. Nome do job estável (`build-test`) para
  ser marcado como check obrigatório da `main` após o merge.
- O workflow `infra-local` (spec 001) passa a incluir `src/backend/**` e `global.json` no
  filtro de paths, pois o Compose agora constrói a API.
