# Implementation Plan: Ambiente de Infraestrutura Local

**Branch**: `001-infra-local` | **Date**: 2026-11-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-infra-local/spec.md` (issue #2)

## Summary

Prover, com um único `docker compose up -d --wait` na raiz do repositório, o ambiente local
de apoio ao backend: PostgreSQL 18, RabbitMQ 4.3 com Management e **Aspire Dashboard**
como coletor e visualizador OTLP de traces, métricas e logs (decisão do time). Todo serviço
tem healthcheck, versões fixadas, portas publicadas apenas em `127.0.0.1` e configuráveis
por `.env` (com `.env.example` versionado), volumes nomeados para persistência e reset por
`docker compose down -v`. Um workflow de CI sobe o ambiente e exige que todos os serviços
fiquem saudáveis.

## Technical Context

**Language/Version**: YAML (Compose Specification, Docker Compose v2); sem código de aplicação

**Primary Dependencies**: `postgres:18.7-alpine`, `rabbitmq:4.3.7-management-alpine`,
`mcr.microsoft.com/dotnet/aspire-dashboard:13.6.5` (ver [research.md](research.md#r2-versões-das-imagens-fr-012))

**Storage**: volumes nomeados `postgres-data` e `rabbitmq-data`; telemetria em memória no dashboard

**Testing**: `docker compose config -q` + `docker compose up -d --wait` no GitHub Actions;
roteiro manual em [quickstart.md](quickstart.md)

**Target Platform**: estações de desenvolvimento Windows/macOS (Docker Desktop) e Linux
(Docker Engine); runner `ubuntu-latest` no CI

**Project Type**: infraestrutura de desenvolvimento local (sem serviço de produção)

**Performance Goals**: todos os serviços `healthy` em ≤ 60s com imagens em cache (SC-002);
telemetria visível em ≤ 10s (SC-004)

**Constraints**: nenhuma tag flutuante; nenhum segredo real versionado; portas só em
`127.0.0.1`; ambiente sobe sem `.env`

**Scale/Scope**: 3 serviços, 2 volumes, 1 rede, ~12 variáveis de configuração

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec aprovada em `specs/001-infra-local/`, vinculada à issue #2; branch `001-infra-local` = nome da pasta | ✅ |
| II. Isolamento da camada de Application | Sem código de aplicação nesta feature | N/A |
| III. Erros via ProblemDetails | Sem API nesta feature | N/A |
| IV. Integridade financeira | Sem operações financeiras | N/A |
| V. Contratos versionados | Ambiente exposto como contrato versionado em [contracts/ambiente-local.md](contracts/ambiente-local.md) | ✅ |
| VI. Observabilidade nativa | Aspire Dashboard recebe traces, métricas e logs via OTLP (gRPC e HTTP) | ✅ |
| VII. Qualidade verificável | CI valida o Compose e exige todos os serviços `healthy` (R8) | ✅ |
| Restrições: PostgreSQL / RabbitMQ | Exatamente os definidos na constituição | ✅ |
| Restrições: Docker Compose com healthchecks em toda a infraestrutura | Healthcheck em 100% dos serviços (R3) | ✅ (ver ressalva do dashboard em R3) |
| Restrições: segredos não versionados | `.env` ignorado; `.env.example` só com valores de desenvolvimento | ✅ |
| Restrições: novas dependências NuGet/npm justificadas | Nenhuma dependência NuGet/npm; imagens justificadas em R2 | ✅ |
| Fluxo: Conventional Commits, PR com `Closes #2`, merge commit | Seguido na branch | ✅ |

**Resultado pré-pesquisa**: aprovado, sem violações.
**Re-check pós-design (Phase 1)**: aprovado. O contrato, o modelo e o quickstart não
introduzem desvios. A única ressalva é a possível ausência de ferramentas de healthcheck na
imagem do Aspire Dashboard (R3); nesse caso, a solução é uma imagem derivada mínima com a
ferramenta de checagem, nunca a dispensa do healthcheck.

## Project Structure

### Documentation (this feature)

```text
specs/001-infra-local/
├── plan.md                      # Este arquivo
├── research.md                  # Phase 0: decisões técnicas (R1–R8)
├── data-model.md                # Phase 1: serviços, configuração e volumes
├── quickstart.md                # Phase 1: roteiro de validação
├── contracts/
│   └── ambiente-local.md        # Phase 1: portas, endpoints, credenciais e comandos
├── checklists/
│   └── requirements.md          # Checklist de qualidade da spec
└── tasks.md                     # Phase 2 (/speckit-tasks — ainda não criado)
```

### Source Code (repository root)

```text
/
├── docker-compose.yml           # projeto "bank": postgres, rabbitmq, aspire-dashboard
├── .env.example                 # variáveis com padrões de desenvolvimento (versionado)
├── docker/
│   └── aspire-dashboard/
│       └── Dockerfile           # SOMENTE se a imagem não tiver ferramenta de healthcheck (R3)
├── .github/
│   └── workflows/
│       └── infra-local.yml      # valida o Compose e exige serviços healthy
└── README.md                    # nova seção "Ambiente local" (FR-011)
```

**Structure Decision**: o `docker-compose.yml` fica na raiz (nome exigido pela issue #2 e
local onde o comando é executado, FR-001). Nenhum arquivo é criado em `src/` nesta
feature. A API (issue #3) adicionará seu serviço ao mesmo arquivo, dependendo dos
healthchecks aqui definidos.

## Complexity Tracking

Nenhuma violação da constituição até o momento. Se a imagem do Aspire Dashboard não
oferecer ferramenta para healthcheck (R3), o motivo da imagem derivada
(`docker/aspire-dashboard/Dockerfile`) será registrado aqui durante a implementação.
