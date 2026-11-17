# Implementation Plan: Dados do titular autenticado

**Branch**: `005-usuarios-me` | **Date**: 2026-11-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-usuarios-me/spec.md` (issue #18; desbloqueia a
Fase 8 da spec 004 / issue #17)

## Summary

Novo endpoint `GET /api/v1/usuarios/me` que devolve os dados cadastrais do titular do token
(identificador, nome, e-mail, documento completo, tipo de conta e data de cadastro). Segue o
padrão de `/carteiras/minha` da spec 003: erro de domínio `usuario-nao-encontrado`, abstração de
leitura e caso de uso na Application, projeção somente-leitura na Infrastructure e endpoint de
transporte na Api com a política `UsuarioAutenticado`, `Cache-Control: no-store` e publicação no
OpenAPI. Sem dependências nem migrations novas.

## Technical Context

**Language/Version**: C# / .NET 10 (spec 002)

**Primary Dependencies**: as da spec 003 (ASP.NET Core Minimal APIs, EF Core + Npgsql, JwtBearer); nenhuma nova

**Storage**: PostgreSQL — leitura da tabela `usuarios` (spec 003); sem migration

**Testing**: xUnit v3, `WebApplicationFactory` + Testcontainers (PostgreSQL), ArchUnitNET

**Target Platform**: contêiner Linux (Compose) e SDK local

**Project Type**: web-service (backend)

**Performance Goals**: p95 < 1 s no ambiente local (SC-004); uma consulta indexada por chave primária

**Constraints**: titular só pelo token; documento e e-mail fora de logs/traces/métricas;
resposta não armazenável em cache; erros `ProblemDetails` com `type` estável

**Scale/Scope**: 1 endpoint, 1 caso de uso, 1 consulta, 1 tipo de erro novo

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com clarificação registrada, issue #18, branch `005-usuarios-me` = pasta | ✅ |
| II. Endpoints só transporte; Domain isolado | Endpoint lê `sub` e chama `ObterTitular`; leitura atrás de `IUsuarioConsultas` implementada na Infrastructure; regras de arquitetura existentes cobrem | ✅ |
| III. ProblemDetails com `type` estável | `usuario-nao-encontrado` novo e documentado; 401/403 reaproveitados (R3) | ✅ |
| IV. Integridade financeira | Não envolve saldo | N/A |
| V. Contratos versionados + OpenAPI | [contracts/usuarios-me-api.md](contracts/usuarios-me-api.md) v1.0.0; endpoint no documento `v1` com `Bearer` | ✅ |
| VI. Observabilidade e dados sensíveis | Nenhum log/span/métrica com documento ou e-mail; teste de dados sensíveis estendido; métricas HTTP cobrem o endpoint (R5) | ✅ |
| VII. Unitários, integração, 401/403, Testcontainers | Caso de uso com unitários; integração com 200/401/403/404 contra PostgreSQL real (R6) | ✅ |
| Restrições: dependência nova justificada | Nenhuma | ✅ |
| Convenções: linguagem ubíqua em português | `ObterTitular`, `DadosTitular`, `UsuariosEndpoints` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado, sem itens em
Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/005-usuarios-me/
├── plan.md
├── research.md                 # R1–R6
├── data-model.md               # visão DadosTitular e erro novo
├── quickstart.md
├── contracts/
│   └── usuarios-me-api.md      # v1.0.0
├── checklists/
│   └── requirements.md
└── tasks.md                    # /speckit-tasks
```

### Source Code (repository root)

```text
src/backend/
├── src/
│   ├── Bank.Domain/Usuarios/ErrosUsuario.cs                 # + NaoEncontrado
│   ├── Bank.Application/
│   │   ├── Abstracoes/IUsuarioConsultas.cs                  # novo (DadosTitular)
│   │   ├── Usuarios/ObterTitular.cs                         # novo caso de uso
│   │   └── DependencyInjection.cs                           # + ObterTitular
│   ├── Bank.Infrastructure/
│   │   ├── Persistencia/UsuarioConsultas.cs                 # novo (projeção AsNoTracking)
│   │   └── DependencyInjection.cs                           # + IUsuarioConsultas
│   └── Bank.Api/
│       ├── Endpoints/UsuariosEndpoints.cs                   # novo GET /api/v1/usuarios/me
│       ├── Endpoints/CarteirasEndpoints.cs                  # usa a extensão de sub
│       ├── Seguranca/ClaimsPrincipalExtensions.cs           # novo TentarObterUsuarioId
│       ├── ErrorHandling/ResultadoHttpExtensions.cs         # + título usuario-nao-encontrado
│       └── Program.cs                                       # + MapUsuariosEndpoints
└── tests/
    ├── Bank.UnitTests/Application/ObterTitularTests.cs
    └── Bank.IntegrationTests/
        ├── UsuariosMeTests.cs
        ├── OpenApiTests.cs                                  # + /usuarios/me
        └── DadosSensiveisTelemetriaTests.cs                 # + consulta /usuarios/me
```

**Structure Decision**: mesma organização da spec 003 (por conceito em cada camada).

## Complexity Tracking

Nenhum item.
