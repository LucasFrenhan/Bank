# Implementation Plan: Consulta de transferência por identificador

**Branch**: `008-consulta-transferencia` | **Date**: 2026-11-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/008-consulta-transferencia/spec.md` (issue #36)

## Summary

Novo endpoint `GET /api/v1/transferencias/{transferenciaId}` (PF e PJ) que devolve o comprovante
de uma transferência ao pagador ou ao recebedor. O caso de uso `ConsultarTransferencia` valida o
UUID, lê o comprovante por identificador (nova leitura no `ITransferenciaRepositorio`, mesma
projeção do comprovante por chave) e decide o acesso; terceiros e identificadores inexistentes
recebem o mesmo 404 `transferencia-nao-encontrada`. Para o recebedor, `chaveIdempotencia` vem
`null`. O comprovante (envio e consulta) ganha `pagador.documento` mascarado. Contrato de
transferências v1.1.0 (mudanças aditivas), métrica `bank.transferencias.consultas` por resultado,
testes unitários e de integração. Sem migration nem dependência nova.

## Technical Context

**Language/Version**: C# / .NET 10 (spec 002)

**Primary Dependencies**: as da spec 006; nenhuma nova

**Storage**: PostgreSQL 18 — leitura de `transferencias`, `carteiras` e `usuarios` por chave
primária; sem migration

**Testing**: xUnit v3, `WebApplicationFactory` + Testcontainers (PostgreSQL), ArchUnitNET

**Target Platform**: contêiner Linux (Compose) e SDK local

**Project Type**: web-service (backend); consumidor: fase 10 da spec 007 (issue #32)

**Performance Goals**: p95 < 1 s por consulta no ambiente local (SC-004)

**Constraints**: somente leitura; 404 idêntico para inexistente e terceiro; nenhum documento,
nome ou e-mail na telemetria; mudanças só aditivas nos endpoints existentes

**Scale/Scope**: 1 endpoint novo, 1 caso de uso, 1 método de repositório, 1 erro, 1 métrica; 2
records alterados (`Comprovante`, `PagadorComprovante`)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 2 clarificações, issue #36, branch `008-consulta-transferencia` = pasta | ✅ |
| II. Isolamento de camadas | Endpoint só converte claims/rota em consulta e o resultado em HTTP; regra de acesso no caso de uso; leitura via `ITransferenciaRepositorio` (Infrastructure). Testes de arquitetura existentes cobrem | ✅ |
| III. Erros padronizados | `ProblemDetails` com `traceId`; novo `type` estável `transferencia-nao-encontrada`; 400 com `errors.transferenciaId` | ✅ |
| IV. Integridade financeira | Somente leitura; nenhum saldo, lançamento ou evento alterado (SC-006 testado) | ✅ |
| V. Contratos explícitos | [contracts/transferencias-api.md](contracts/transferencias-api.md) v1.1.0; mudanças nos endpoints existentes só aditivas (campo novo; `chaveIdempotencia` anulável, sempre preenchida no envio); OpenAPI com a operação nova | ✅ |
| VI. Observabilidade | Métrica `bank.transferencias.consultas` por resultado; sem documento, nome ou e-mail em logs/traces/métricas; identificador da transferência só no caminho HTTP (não é dado pessoal, FR-007) | ✅ |
| VII. Qualidade verificável | Unidade do caso de uso; integração com 200 (pagador e recebedor), 400, 401, 403 e 404 contra PostgreSQL em contêiner | ✅ |
| Restrições: SQL puro em `.sql` | Não há SQL puro: LINQ/EF Core | ✅ |
| Restrições: dependência justificada | Nenhuma | ✅ |

**Resultado pré-pesquisa**: aprovado.
**Re-check pós-design (Phase 1)**: aprovado; a mudança de `chaveIdempotencia` para anulável está
em Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/008-consulta-transferencia/
├── plan.md                 # Este arquivo
├── research.md             # Phase 0: decisões R1–R7
├── data-model.md           # Phase 1: comprovante alterado, regra de acesso, erro e métrica
├── quickstart.md           # Phase 1: roteiro de validação
├── contracts/
│   └── transferencias-api.md   # Phase 1: contrato v1.1.0 (delta da v1.0.0 da spec 006)
├── checklists/
│   └── requirements.md
└── tasks.md                # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/backend/
├── src/
│   ├── Bank.Domain/Transferencias/ErrosTransferencia.cs        # + TransferenciaNaoEncontrada
│   ├── Bank.Application/
│   │   ├── Abstracoes/ITransferencias.cs                         # Comprovante (chave anulável), PagadorComprovante (+ Documento),
│   │   │                                                         # ITransferenciaRepositorio.ObterComprovantePorIdAsync
│   │   ├── Observabilidade/MetricasTransferencias.cs             # + RegistrarConsulta(resultado)
│   │   ├── Transferencias/ConsultarTransferencia.cs              # novo caso de uso
│   │   ├── Transferencias/RealizarTransferencia.cs               # documento mascarado do pagador no comprovante
│   │   └── DependencyInjection (registro do caso de uso)
│   ├── Bank.Infrastructure/Persistencia/TransferenciaRepositorio.cs  # consulta base + por id; documento do pagador
│   └── Bank.Api/Endpoints/TransferenciasEndpoints.cs             # GET /{transferenciaId}; PagadorResposta + Documento; chave anulável
└── tests/
    ├── Bank.UnitTests/Application/ConsultarTransferenciaTests.cs # novo
    ├── Bank.UnitTests/Application/RealizarTransferenciaTests.cs  # documento do pagador
    ├── Bank.UnitTests/Application/DublesTransferencia.cs         # repositório por id
    └── Bank.IntegrationTests/
        ├── ConsultaTransferenciaTests.cs                         # novo
        ├── TransferenciasTests.cs                                # pagador.documento no comprovante
        ├── OpenApiTests.cs, TelemetryTests.cs, DadosSensiveisTelemetriaTests.cs

docs/desenvolvimento.md                                           # + consulta por identificador
specs/006-transferencias/contracts/transferencias-api.md          # nota apontando para a v1.1.0
```

**Structure Decision**: mesma solução da spec 006; nenhum projeto novo. A regra de acesso fica no
caso de uso (Application) e a leitura no repositório (Infrastructure), conforme o Princípio II.

## Fases de entrega

1. **Fundação**: erro, records do comprovante, métrica, leitura por id no repositório, documento
   do pagador no envio.
2. **US1–US3**: caso de uso, endpoint, testes unitários e de integração (pagador, recebedor,
   terceiro/inexistente, 400/401/403).
3. **Transversais**: OpenAPI, telemetria, documentação e nota no contrato da spec 006.

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| `chaveIdempotencia` anulável no esquema compartilhado do comprovante | Clarification 1: mesmo formato para pagador e recebedor, com a chave vazia para o recebedor | Esquema separado para a consulta foi rejeitado pelo usuário; no envio o campo continua sempre preenchido, então consumidores atuais não recebem `null` |
