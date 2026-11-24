# Implementation Plan: Frontend de cartão de crédito, cobranças, faturas e recebíveis

**Branch**: `015-frontend-cartao-credito` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/015-frontend-cartao-credito/spec.md` (issue #54)

## Summary

O frontend ganha as telas da API da spec 014: cartão (com olho para exibir os dados), pagamento de cobrança,
faturas e pagamento (PF); cobranças com código curto e link, e recebíveis com antecipação (PJ); e o aviso de
fatura no início da PF. Dados do cartão ficam só em memória. Cada operação financeira usa uma chave de
idempotência guardada na aba, sem dados do cartão. No backend, uma mudança aditiva: o código de 8 caracteres da
cobrança, o preenchimento das existentes e a consulta por ele (contrato 1.1.0). O visual segue o
[protótipo aprovado](https://claude.ai/artifact/9mhoKhZ8E9guBLsVE53oEY).

## Technical Context

**Language/Version**: TypeScript 5.9.3, React 19.3.1, Node 26; C# / .NET 10; SQL (PostgreSQL 18)

**Primary Dependencies**: as atuais; **nenhuma dependência nova**

**Storage**: `sessionStorage` (`bank.operacoes`, só chaves); PostgreSQL — coluna `cobrancas.codigo`

**Testing**: Vitest + Testing Library + MSW; Playwright + axe contra o Compose; xUnit v3 + Testcontainers

**Target Platform**: navegadores atuais, desktop e mobile; contêineres do Compose (sem serviço novo)

**Project Type**: web application (frontend) + web-service (mudança aditiva)

**Performance Goals**: SC-001 a SC-003 (até 1 minuto por fluxo); atualização do detalhe em até 10 s

**Constraints**: número e CVV só em memória; WCAG 2.2 AA; 320 a 1920 px sem rolagem horizontal; centavos inteiros;
sem mudança nas telas atuais além de menu, aviso no início e permissões

**Scale/Scope**: 7 rotas e 10 telas; 13 chamadas à API; 1 migration; 1 endpoint novo

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 8 clarificações e protótipo aprovado; issue #54; branch = pasta | ✅ |
| II. Isolamento de camadas | `CodigoCobranca` no Domain; consulta na Application; SQL na Infrastructure; endpoint só transporte. No frontend, só `src/api/` acessa a rede | ✅ |
| III. Erros padronizados | Decisão pelo `type`; `traceId` exibido em inesperados; nenhum erro novo na API. Código malformado responde 404 em vez de 400 | ⚠️ justificado |
| IV. Integridade financeira | Chave reaproveitada até o sucesso (R6); centavos inteiros; o valor final é o da API | ✅ |
| IV. Concorrência | Unicidade do código pelo índice, com nova tentativa (R1) | ✅ |
| V. Contratos versionados | API 1.1.0 e frontend 1.4.0, aditivos; tipos regenerados do OpenAPI | ✅ |
| VI. Observabilidade, sem dados sensíveis | Nenhum sinal novo; dados do cartão proibidos e verificados em e2e (R10, R13) | ✅ |
| VII. Testes | Unitários, integração com 401/403, componentes por estado e recusa, e2e com axe | ✅ |
| Restrições: SQL em `.sql` | `Sql/Migracoes/PreencherCodigosDasCobrancas.sql`, `Sql/Consultas/BloquearCobranca.sql` | ✅ |
| Restrições: identificadores tipados | `CodigoCobranca` é value object; nenhum id novo | ✅ |
| Restrições: telas desktop e mobile | Protótipo e e2e nos dois; larguras de 320 a 1920 px | ✅ |
| Restrições: dependência nova justificada | Nenhuma | ✅ |
| Convenções: português | `Cartao`, `Cobranca`, `Fatura`, `Recebivel`, `extrairCodigo` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado, com um item em
Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/015-frontend-cartao-credito/
├── plan.md
├── research.md                      # R1–R13
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── frontend.md                  # v1.4.0
│   └── cartoes-cobrancas-api.md     # v1.1.0
├── checklists/requirements.md
└── tasks.md                         # /speckit-tasks
```

### Source Code (repository root)

```text
docs/design/                         # fontes do protótipo e capturas das telas prontas
docs/desenvolvimento.md
src/backend/src/
├── Bank.Domain/Cobrancas/           # CodigoCobranca (novo), Cobranca
├── Bank.Application/                # EmitirCobranca, ConsultarCobranca, ICobrancaConsultas, IUnidadeDeTrabalho
├── Bank.Infrastructure/Persistencia/# CobrancaConfiguracao, CobrancaConsultas, UnidadeDeTrabalho, Sql/, Migrations/
└── Bank.Api/Endpoints/CobrancasEndpoints.cs
src/backend/tests/                   # unitários e integração
src/frontend/
├── src/
│   ├── App.tsx                      # rotas novas
│   ├── api/{schema.d.ts,cliente.ts,erros.ts}
│   ├── cartao/                      # useDadosCartao, validacao, CartaoDesenhado
│   ├── cobrancas/codigo.ts
│   ├── operacoes/                   # chaves.ts, useOperacao.ts
│   ├── formatacao/dia.ts
│   ├── design/componentes/          # Icones (olho, cartão), Filtros, ListaPaginada, SeloSituacao, Confirmacao, Layout
│   ├── sessao/                      # SomentePerfil, permissoes, armazenamento
│   ├── telemetria/ponte.ts
│   ├── testes/handlers.ts
│   └── paginas/                     # cartao/, cobrancas/, faturas/, recebiveis/, Inicio (AvisoFatura), Extrato (Filtros)
└── e2e/                             # cartao, cobrancas, pagamento, faturas, recebiveis, privacidadeCartao; apoio
```

**Structure Decision**: a lógica sem tela (código, chaves, validação, dias) fica em módulos próprios, testáveis
sem componente; cada área ganha uma pasta em `paginas/`. Os hooks e telas atuais não mudam, salvo a extração de
`Filtros`.

## Complexity Tracking

| Violação | Por que é necessária | Alternativa mais simples rejeitada |
| --- | --- | --- |
| `GET /cobrancas/codigo/{codigo}` responde 404, e não 400, para código malformado (Princípio III) | O código é ditado e digitado por pessoas; para elas, código errado e inexistente são o mesmo caso (FR-009b), e a resposta única não revela o formato a quem tenta adivinhar. O corpo continua `ProblemDetails` com `type` e `traceId` | 400 com erro no campo: dois tratamentos para a mesma situação, distinguindo códigos bem formados. O 400 de `/{cobrancaId}` não muda |
