# Implementation Plan: Setup da API, Documentação Interativa e Observabilidade

**Branch**: `002-setup-api` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-setup-api/spec.md` (issue #3)

## Summary

Criar a solução `.NET 10` do backend em `src/backend/` com as quatro camadas da
constituição (`Bank.Api`, `Bank.Application`, `Bank.Domain`, `Bank.Infrastructure`) e três
projetos de teste (unitário, integração e arquitetura). A API Minimal expõe `/health`,
gera OpenAPI nativamente e publica o Scalar em `/scalar/v1` fora de produção; exporta
traces, métricas e logs via OTLP para o Aspire Dashboard como `bank-api`; e responde todo
erro em `ProblemDetails` com `traceId` pesquisável no dashboard. A API entra no Compose
dependendo da infraestrutura saudável, e um CI geral (`build-test`, sem filtro de paths)
compila e testa todo PR.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.4xx fixado por `global.json`, runtime 10.0.15)

**Primary Dependencies**: ASP.NET Core Minimal APIs, `Microsoft.AspNetCore.OpenApi`
10.0.15, `Scalar.AspNetCore` 2.21.0, OpenTelemetry 1.21.0 (Hosting, OTLP Exporter,
Instrumentation AspNetCore/Http) — justificativas em [research.md R3](research.md#r3-pacotes-nuget-justificativa-exigida-pela-constituição)

**Storage**: N/A (a API ainda não usa PostgreSQL nem RabbitMQ)

**Testing**: xUnit v3 4.0.3, `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory),
TngTech.ArchUnitNET 0.13.5

**Target Platform**: Linux container (`aspnet:10.0.15-alpine3.24`) e execução local pelo
SDK em Windows/macOS/Linux

**Project Type**: web-service (backend), sem frontend nesta spec

**Performance Goals**: telemetria visível em ≤ 10s (SC-003); sem metas de throughput nesta
spec

**Constraints**: OpenAPI/Scalar desligados em `Production`; avisos de compilação são
erros; nenhuma dependência sem justificativa; nenhuma regra de negócio

**Scale/Scope**: 7 projetos, 1 endpoint público (`/health`) + 2 de documentação, 2
endpoints internos de teste, 2 workflows de CI

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com esclarecimento registrado, issue #3, branch `002-setup-api` = pasta | ✅ |
| II. Isolamento da camada de Application | Camadas Api/Application/Domain/Infrastructure; endpoints sem acesso a infraestrutura; testes de arquitetura (R9) falham o build | ✅ |
| III. Erros via ProblemDetails | Pipeline com `AddProblemDetails`, `UseExceptionHandler`, `UseStatusCodePages`, `traceId` e 5xx sem detalhes (R6) | ✅ |
| IV. Integridade financeira | Sem operações financeiras | N/A |
| V. Contratos versionados | OpenAPI nativo + Scalar `/scalar/v1` fora de produção; [contracts/api-base.md](contracts/api-base.md) v1.0.0 | ✅ |
| VI. Observabilidade nativa | Traces, métricas e logs via OTLP, logs estruturados correlacionados, sem dados sensíveis (R5) | ✅ |
| VII. Qualidade verificável | Testes unitários, de integração (caminho feliz + erros) e de arquitetura; CI `build-test` em todo PR (R10) | ✅ |
| VII. 401/403 em endpoints protegidos | Nenhum endpoint protegido nesta spec (autenticação é spec própria) | N/A |
| VII. Testcontainers em testes de integração | Nenhuma dependência externa nesta spec; entra com a primeira spec que usar banco/broker | N/A |
| Restrições: .NET 10, Minimal APIs, OpenAPI + Scalar, OpenTelemetry/OTLP | Exatamente os definidos | ✅ |
| Restrições: endpoint só roteamento/transporte; regras em Application/Domain | Sem regras de negócio; `/health` usa Health Checks do framework | ✅ |
| Restrições: nova dependência NuGet justificada | Tabela em R3, com os pacotes rejeitados | ✅ |
| Restrições: segredos não versionados | Nenhum segredo nesta spec | ✅ |
| Convenções: linguagem ubíqua em português, termos técnicos no original | Nomes técnicos (`Bank.Api`, `ProblemDetailsTraceIdCustomizer`) — sem termos de domínio ainda | ✅ |
| Fluxo: Conventional Commits, `Closes #3`, merge commit | Seguido | ✅ |

**Resultado pré-pesquisa**: aprovado.
**Re-check pós-design (Phase 1)**: aprovado. Ponto de atenção registrado em Complexity
Tracking: endpoints de teste dentro do assembly da API (R7).

## Project Structure

### Documentation (this feature)

```text
specs/002-setup-api/
├── plan.md                 # Este arquivo
├── research.md             # Phase 0: decisões R1–R10
├── data-model.md           # Phase 1: configuração e formato de erro
├── quickstart.md           # Phase 1: roteiro de validação
├── contracts/
│   └── api-base.md         # Phase 1: endpoints base, garantias de erro e telemetria
├── checklists/
│   └── requirements.md
└── tasks.md                # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
/
├── global.json                              # SDK 10.0.400 + latestPatch (R1)
├── docker-compose.yml                       # + serviço api (R8)
├── .env.example                             # + API_PORT
├── .github/workflows/
│   ├── ci.yml                               # novo: build-test em todo PR (R10)
│   └── infra-local.yml                      # paths + src/backend/** e global.json
└── src/backend/
    ├── Bank.slnx
    ├── Directory.Build.props
    ├── Directory.Packages.props
    ├── Dockerfile                           # sdk alpine → aspnet alpine (R8)
    ├── .dockerignore
    ├── src/
    │   ├── Bank.Domain/
    │   │   └── DomainAssemblyMarker.cs
    │   ├── Bank.Application/
    │   │   └── DependencyInjection.cs       # AddApplication()
    │   ├── Bank.Infrastructure/
    │   │   └── DependencyInjection.cs       # AddInfrastructure()
    │   └── Bank.Api/
    │       ├── Program.cs                   # raiz de composição
    │       ├── Properties/launchSettings.json
    │       ├── appsettings.json
    │       ├── appsettings.Development.json
    │       ├── Endpoints/
    │       │   └── HealthEndpoints.cs
    │       ├── ErrorHandling/
    │       │   ├── ProblemDetailsTraceIdCustomizer.cs
    │       │   └── ErrorHandlingExtensions.cs
    │       ├── Observability/
    │       │   └── TelemetryExtensions.cs
    │       ├── OpenApi/
    │       │   └── OpenApiExtensions.cs
    │       └── Testing/
    │           └── TestEndpoints.cs         # só no ambiente Testing (R7)
    └── tests/
        ├── Bank.UnitTests/
        │   └── ErrorHandling/ProblemDetailsTraceIdCustomizerTests.cs
        ├── Bank.IntegrationTests/
        │   ├── BankApiFactory.cs            # WebApplicationFactory, ambiente Testing
        │   ├── HealthEndpointTests.cs
        │   ├── OpenApiTests.cs
        │   └── ErrorHandlingTests.cs
        └── Bank.ArchitectureTests/
            └── LayerDependencyTests.cs
```

**Structure Decision**: segue a estrutura da constituição (`src/backend` contém a solução e
seus testes). Os nomes de pasta dentro de `Bank.Api` separam transporte (`Endpoints`) de
preocupações transversais, o que permite a regra de arquitetura 4 (R9) apontar
exatamente para os endpoints.

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| Endpoints de teste (`/__test/*`) no assembly da API, só no ambiente `Testing` | Provar 500 sem detalhes internos e 400 por JSON malformado através do pipeline real (US4, SC-004) sem endpoints de negócio | Registrá-los pelo projeto de testes (via `IStartupFilter`) os colocaria antes do `UseExceptionHandler`, testando outro pipeline; sem eles, SC-004 ficaria sem prova automatizada |
| Imagem `aspnet` Alpine em vez da Debian padrão | Ter `wget` para o healthcheck do Compose (FR-012) | A Debian não tem `curl` nem `wget`; uma imagem derivada extra seria mais complexa |
