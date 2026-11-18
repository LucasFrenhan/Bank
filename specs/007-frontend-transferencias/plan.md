# Implementation Plan: Frontend de transferências, comprovante e extrato

**Branch**: `007-frontend-transferencias` | **Date**: 2026-11-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/007-frontend-transferencias/spec.md` (issue #32;
depende parcialmente da issue #36)

## Summary

Acrescentar ao frontend da spec 004 as telas Transferir (Dados → Confirmação), Comprovante e
Extrato do protótipo, consumindo o contrato v1.0.0 da spec 006, e ativar "Transferir", "Ver
extrato" e as listas reais de movimentações no início PF/PJ. O cliente da API ganha as chamadas
de consulta de destinatário, transferência (com `Idempotency-Key` gerada no navegador e
reaproveitada em reenvios, distinguindo 201 de 200) e extrato (filtro `direcao`, paginação). A
transferência em andamento e os comprovantes da aba ficam em `sessionStorage`, no escopo da
sessão. Valores são tratados em centavos inteiros. O comprovante tem rota própria e, quando a
issue #36 entregar `GET /transferencias/{id}`, pode ser reaberto pelo extrato e em outra sessão.
Sem dependências npm novas. Testes: Vitest + MSW para cada resposta do contrato; Playwright +
axe contra o Compose em desktop e mobile.

## Technical Context

**Language/Version**: TypeScript 5.9.3 sobre Node.js 26.13.0; React 19.3.1 (inalterados, spec 004)

**Primary Dependencies**: as da spec 004 (`react-router` 8.5.1, `openapi-fetch` 0.17.0 /
`openapi-typescript` 7.13.0, OpenTelemetry JS); **nenhuma dependência nova** — UUID, compartilhar
e área de transferência usam APIs nativas do navegador (research R2, R10)

**Storage**: `sessionStorage` — sessão (spec 004), transferência em andamento e comprovantes da
aba (research R4)

**Testing**: Vitest 5.1.0 + Testing Library + MSW 3.0.3; Playwright 1.64.1 + `@axe-core/playwright`
4.14.0 contra a API real do Compose (research R11)

**Target Platform**: navegadores atuais desktop e mobile; nginx no Compose (inalterado)

**Project Type**: web application (frontend SPA consumindo a API existente)

**Performance Goals**: nome do titular em ≤ 2 s após o documento completo; primeira página do
extrato em ≤ 2 s; transferência completa em < 1 min (SC-001, SC-004)

**Constraints**: WCAG 2.2 AA; sem rolagem horizontal de 320 a 1920 px; aritmética monetária em
centavos inteiros; nenhum dado pessoal, valor, descrição ou chave de idempotência em telemetria,
URL ou log; sem mudanças na API nesta spec (o endpoint da #36 é spec própria)

**Scale/Scope**: 4 telas novas + 2 alteradas (×2 layouts), 4 endpoints novos consumidos (1
bloqueado pela #36), 5 tipos de erro novos

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com clarificação registrada, issue #32, branch `007-frontend-transferencias` = pasta; dependência do backend isolada na issue #36 | ✅ |
| II. Isolamento de camadas | Backend inalterado. Só `src/api/` acessa a rede (regra de lint da spec 004); telas usam hooks (`useExtrato`, `useTransferencia`) | ✅ |
| III. Erros padronizados | Erros novos traduzidos pelo `type` (R9), `traceId` exibido em inesperados; nenhum `detail` na tela | ✅ |
| IV. Integridade financeira | Chave de idempotência gerada no cliente e reaproveitada em reenvios (R2); valores em centavos inteiros, sem arredondar (R3); envio bloqueado contra duplo clique; o saldo final é decidido pela API | ✅ |
| V. Contratos explícitos | Tipos regenerados do OpenAPI; [contracts/frontend.md](contracts/frontend.md) v1.1.0; consome o contrato v1.0.0 da spec 006 sem alterá-lo | ✅ |
| VI. Observabilidade | Telas e chamadas novas na telemetria `bank-frontend`; rota registrada pelo padrão (sem ID); proibidos valor, descrição, nomes, documento e chave | ✅ |
| VII. Qualidade verificável | Testes de componentes para cada resposta do contrato; e2e contra a API real em contêiner, caminho feliz e cada erro, desktop e mobile, no CI | ✅ |
| Restrições: toda tela com desktop e mobile especificada e validada | Spec US6; e2e nos projetos `desktop`/`mobile` + 320/768/1920 | ✅ |
| Restrições: dependência justificada | Nenhuma dependência nova | ✅ |
| Restrições: segredos não versionados | Nada novo | ✅ |
| Convenções: português e linguagem ubíqua | `Transferir`, `Confirmar`, `Comprovante`, `Extrato`, `useTransferencia`, `interpretarValor` | ✅ |

**Resultado pré-pesquisa**: aprovado.
**Re-check pós-design (Phase 1)**: aprovado; a fase bloqueada pela #36 está registrada em
Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/007-frontend-transferencias/
├── plan.md                 # Este arquivo
├── research.md             # Phase 0: decisões R1–R11
├── data-model.md           # Phase 1: transferência em andamento, comprovante, extrato, valores, erros
├── quickstart.md           # Phase 1: roteiro de validação
├── contracts/
│   └── frontend.md         # Phase 1: contrato do frontend v1.1.0 (delta da v1.0.0)
├── checklists/
│   └── requirements.md
└── tasks.md                # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
/
├── docs/desenvolvimento.md                 # + telas novas e preparo de saldo (FR-031)
└── src/frontend/
    ├── src/
    │   ├── App.tsx                         # + rotas /transferir, /transferir/confirmar, /transferencias/:id, /extrato
    │   ├── api/
    │   │   ├── schema.d.ts                 # regenerado (npm run api:tipos)
    │   │   ├── cliente.ts                  # + consultarDestinatario, transferir (201/200), obterExtrato, obterTransferencia (#36)
    │   │   └── erros.ts                    # + códigos da spec 006 e da #36; contexto 'transferencia' (R9)
    │   ├── sessao/armazenamento.ts         # removerSessao() também apaga bank.transferencia e bank.comprovantes
    │   ├── telemetria/ponte.ts             # navegação registrada pelo padrão da rota
    │   ├── formatacao/
    │   │   ├── valor.ts                    # interpretarValor, centavos ↔ número (R3)
    │   │   ├── mascaras.ts                 # + detecção CPF/CNPJ e DV de CPF (R6)
    │   │   └── data.ts                     # + data relativa e agrupamento por dia
    │   ├── transferencia/
    │   │   ├── armazenamento.ts            # transferência em andamento e cache de comprovantes (R4)
    │   │   ├── chave.ts                    # UUID e assinatura (R2)
    │   │   └── useTransferencia.ts         # estado das etapas e envio
    │   ├── design/componentes/
    │   │   ├── Layout.tsx                  # navegação ativa por perfil (R8)
    │   │   ├── Etapas.tsx                  # indicador Dados → Confirmar → Comprovante
    │   │   ├── CampoValor.tsx              # "R$" + teclado decimal
    │   │   ├── Feedback.tsx                # + variantes atenção/informativo/neutro do protótipo
    │   │   └── ListaLancamentos.tsx        # itens com ícone, sinal e cor (extrato mobile e início)
    │   └── paginas/
    │       ├── Transferir.tsx              # etapa Dados
    │       ├── ConfirmarTransferencia.tsx  # etapa Confirmação e envio
    │       ├── Comprovante.tsx             # comprovante (pagador/recebedor), compartilhar
    │       ├── Extrato.tsx                 # tabela (desktop) e lista agrupada (mobile)
    │       ├── useExtrato.ts, useLarguraDesktop.ts
    │       ├── Inicio.tsx                  # ações ativas, últimas movimentações/recebimentos
    │       └── **/*.test.tsx
    └── e2e/
        ├── apoio.ts                        # + transferir pela API, acumular saldo, criar lançamentos
        ├── transferencia.spec.ts           # fluxo completo, recusas, reenvio, PJ
        ├── extrato.spec.ts                 # filtros, paginação desktop/mobile, início PF/PJ
        ├── comprovante.spec.ts             # reabertura (após #36)
        └── responsividade/teclado/acessibilidade  # telas novas incluídas
```

**Structure Decision**: mesma aplicação da spec 004. A lógica da transferência (estado, chave,
armazenamento) fica em `src/transferencia/`, testável sem tela; `paginas/` só compõe a interface;
`api/` continua o único acesso à rede.

## Fases de entrega

1. **Base**: tipos regenerados, chamadas novas no cliente, tradução de erros, `interpretarValor`,
   formatação de datas, armazenamento da transferência, limpeza na saída.
2. **US1–US3**: Transferir, Confirmação, Comprovante (cache da aba), recusas e reenvio.
3. **US4–US5**: Extrato (desktop/mobile), início PF/PJ e navegação ativa.
4. **US6 e transversais**: responsividade, acessibilidade, teclado, telemetria, documentação, e2e.
5. **Bloqueada pela #36**: reabrir o comprovante pelo extrato, pelo endereço em outra sessão e
   como recebedor (`obterTransferencia`, tipos regenerados, `comprovante.spec.ts`).

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| Fase final bloqueada pela issue #36 | Reabrir o comprovante a qualquer momento exige consulta por identificador, que a spec 006 não oferece | Implementar o endpoint aqui misturaria backend e frontend na mesma spec (decisão do usuário: issue separada) |
| Transferência em andamento em `sessionStorage` | A chave de idempotência precisa sobreviver a recarregar a confirmação (US3) | Estado em memória perderia a chave e permitiria cobrança duplicada |
| e2e acumulando saldo por transferências entre contas de apoio | O saldo de demonstração é R$ 10,00 e o e2e não acessa a base | SQL direto no Postgres acoplaria o e2e ao schema; endpoint de teste exigiria mudança no backend |
