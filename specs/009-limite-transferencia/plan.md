# Implementation Plan: Limite de transferência

**Branch**: `009-limite-transferencia` | **Date**: 2026-11-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/009-limite-transferencia/spec.md` (issue #33)

## Summary

Acrescenta ao motor de transferências da spec 006 dois limites para Pessoa Física: por operação
(R$ 1.000,00) e diário (R$ 5.000,00, dia civil de Brasília), configuráveis por ambiente. A regra
fica num objeto de valor do Domain (`LimitesTransferencia`). O limite por operação é verificado
antes do bloqueio. O diário é verificado depois do `FOR UPDATE` da carteira de origem, somando as
transferências concluídas do dia na mesma transação `ReadCommitted`, o que impede que envios
simultâneos furem o limite sem criar contador novo. Replays idempotentes não passam pelos limites.
A recusa é um 422 `limite-excedido` com as extensões `tipoLimite`, `limite` e `valorDisponivel`;
para isso o `Erro` ganha detalhes opcionais. Um endpoint novo, `GET /transferencias/limites`,
alimenta a dica e o aviso da tela Transferir. O fuso de Brasília é resolvido nos dois sistemas
(IANA no Linux, com `tzdata` instalado na imagem, e ID do Windows no SDK local).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript + React (frontend, spec 004)

**Primary Dependencies**: as das specs 003–008; nenhuma nova (NuGet ou npm). Pacote de sistema
`tzdata` na imagem Alpine da API (R4)

**Storage**: PostgreSQL 18 — sem tabela nova; índice `ix_transferencias_origem_criado_em`
(migration `AdicionarIndiceTransferenciasOrigemCriadoEm`)

**Testing**: xUnit v3, `WebApplicationFactory` + Testcontainers (PostgreSQL), ArchUnitNET;
Vitest + Testing Library e Playwright no frontend

**Target Platform**: contêiner Linux (Compose) e SDK local (Windows e runner Linux do CI)

**Project Type**: web-service + web-app (backend e frontend na mesma spec)

**Performance Goals**: p95 da transferência < 1 s com a soma do dia (SC-007), apoiada pelo índice

**Constraints**: limite diário nunca furado por concorrência; replay não consome limite; recusa não
grava nada; nenhum dado pessoal na telemetria; contrato só aditivo (MINOR)

**Scale/Scope**: 1 endpoint novo, 1 erro novo, 1 índice, 2 casos de uso (1 novo, 1 alterado),
1 opção de configuração, 2 telas alteradas (Transferir e Confirmação)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 4 clarificações; issue #33; branch `009-limite-transferencia` = pasta | ✅ |
| II. Endpoints só transporte; Domain isolado | `GET /limites` chama `ConsultarLimites`; a regra fica em `LimitesTransferencia` (Domain); soma do dia atrás de `ITransferenciaRepositorio` | ✅ |
| III. ProblemDetails com `type` estável | `limite-excedido` (422) documentado, com extensões tipadas; `detail` não é usado pelo cliente (R5) | ✅ |
| IV. `decimal`/`numeric` | Limites `decimal`; soma sobre `numeric(18,2)` | ✅ |
| IV. Atomicidade e idempotência | Verificação diária na mesma transação; recusa desfaz tudo; replay devolve o original sem reavaliar (FR-008, FR-010) | ✅ |
| IV. Controle de concorrência definido no plano | Reaproveita o bloqueio pessimista ordenado da spec 006; soma após o `FOR UPDATE` em `ReadCommitted` (R3) | ✅ |
| IV. Lançamentos imutáveis | Nenhuma escrita nova; uso diário derivado de dados imutáveis | ✅ |
| V. Contratos versionados | API de transferências v1.2.0 e frontend v1.2.0, ambos aditivos | ✅ |
| VI. Métricas e logs sem dados sensíveis | Tag `tipo_limite` no contador de recusas; contador `bank.transferencias.limites.consultas` para o caso de uso novo; log de auditoria com identificadores e valores (R7) | ✅ |
| VII. Unitários, integração, 401/403, concorrência | R9, incluindo `ConcorrenciaLimiteTests` e 401/403 de `GET /limites` | ✅ |
| Restrições: schema por migrations | Índice pela migration | ✅ |
| Restrições: SQL puro em `.sql` | Nenhum SQL puro novo (soma por LINQ) | ✅ |
| Restrições: dependência nova justificada | Nenhuma NuGet/npm; `tzdata` justificado em R4 | ✅ |
| Restrições: telas responsivas | Dica e avisos validados em desktop e mobile (quickstart, cenário 3) | ✅ |
| Convenções: português | `LimitesTransferencia`, `ConsultarLimites`, `CalendarioBrasilia`, `limite-excedido` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado; um item em Complexity
Tracking (mudança em `Erro`, compartilhado).

## Project Structure

### Documentation (this feature)

```text
specs/009-limite-transferencia/
├── plan.md
├── research.md                    # R1–R9
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── transferencias-api.md      # HTTP v1.2.0
│   └── frontend.md                # frontend v1.2.0
├── checklists/requirements.md
└── tasks.md                       # /speckit-tasks
```

### Source Code (repository root)

```text
docker-compose.yml                                  # api: LIMITE_PF_POR_OPERACAO / LIMITE_PF_DIARIO
.env.example                                        # + as duas variáveis, com os padrões

src/backend/
├── Dockerfile                                      # + apk add tzdata (R4)
├── src/
│   ├── Bank.Domain/
│   │   ├── Comum/Erro.cs                           # + Detalhes opcionais
│   │   └── Transferencias/
│   │       ├── LimitesTransferencia.cs             # TipoLimite + objeto de valor (R1)
│   │       └── ErrosTransferencia.cs               # + LimiteExcedido(...)
│   ├── Bank.Application/
│   │   ├── Abstracoes/ITransferencias.cs           # + SomarEnviadasAsync
│   │   ├── Transferencias/
│   │   │   ├── LimitesTransferenciaOptions.cs      # opções + validador (R2)
│   │   │   ├── CalendarioBrasilia.cs               # dia de Brasília em UTC (R4)
│   │   │   ├── RealizarTransferencia.cs            # verificações de limite (FR-008)
│   │   │   └── ConsultarLimites.cs                 # novo caso de uso (R6)
│   │   ├── Observabilidade/MetricasTransferencias.cs # + tag tipo_limite (R7)
│   │   └── DependencyInjection.cs
│   ├── Bank.Infrastructure/Persistencia/
│   │   ├── TransferenciaRepositorio.cs             # + soma do dia (LINQ)
│   │   ├── Configuracoes/TransferenciaConfiguracao.cs # + índice
│   │   └── Migrations/*_AdicionarIndiceTransferenciasOrigemCriadoEm.cs
│   └── Bank.Api/
│       ├── Endpoints/TransferenciasEndpoints.cs    # + GET /limites, LimitesResposta
│       ├── ErrorHandling/ResultadoHttpExtensions.cs # extensões + título do erro novo
│       └── appsettings.json                        # Transferencias:Limites (padrões)
└── tests/
    ├── Bank.UnitTests/
    │   ├── Domain/LimitesTransferenciaTests.cs
    │   ├── Application/(CalendarioBrasiliaTests, LimitesTransferenciaOptionsTests,
    │   │                ConsultarLimitesTests, RealizarTransferenciaTests +limites)
    │   └── ErrorHandling/ResultadoHttpExtensionsTests.cs (novo: extensões)
    └── Bank.IntegrationTests/
        ├── LimiteTransferenciaTests.cs             # por operação, replay, GET /limites
        ├── LimiteDiarioTests.cs                    # diário e virada do dia de Brasília
        ├── ConcorrenciaLimiteTests.cs              # 5 × R$ 300 / diário R$ 1.000
        └── (ajustes) OpenApiTests, DadosSensiveisTelemetriaTests

src/frontend/src/
├── api/
│   ├── erros.ts                                    # limite-excedido + extensões
│   └── schema.d.ts                                 # npm run api:tipos
├── transferencia/useTransferencia.ts               # continuar() com máximo
└── paginas/
    ├── useLimites.ts                               # GET /limites + máximo agora
    ├── Transferir.tsx                              # dica e aviso
    ├── ConfirmarTransferencia.tsx                  # aviso do erro da API
    └── avisosTransferencia.tsx                     # textos por tipo de limite
src/frontend/e2e/transferencia.spec.ts              # dica com o limite
```

**Structure Decision**: mesma organização por conceito das specs 006–008. Backend e frontend
ficam na mesma spec porque o reflexo na tela Transferir é pequeno e faz parte da issue
(Assumptions da spec).

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| `Erro` ganha `Detalhes` (tipo compartilhado por todos os casos de uso) | O 422 precisa de extensões tipadas (Clarification Q3, R5) | Detalhes só no texto obrigariam o frontend a interpretar `detail` (Princípio III) |
| `tzdata` na imagem e resolução dupla do fuso | O dia de Brasília precisa funcionar no contêiner Alpine e no SDK local com `InvariantGlobalization` (verificado, R4) | Deslocamento fixo de −3h fica errado se o horário de verão voltar |
