# Implementation Plan: Frontend — dados bancários da conta e nome One Bank

**Branch**: `013-frontend-dados-bancarios` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/013-frontend-dados-bancarios/spec.md` (issue #51)

## Summary

Mostrar na tela inicial (PF e PJ) um cartão "Dados da conta" com banco, agência, conta-dígito e tipo
vindos de `contaBancaria` em `GET /api/v1/carteiras/minha` 1.1.0 (já consultada pela tela), com
leitura dígito a dígito, texto de apoio sobre CPF/CNPJ e ação de copiar. Trocar "Bank" por "One Bank"
na interface (constante única e teste que impede a volta do nome antigo) e o ícone por "1B" (marca,
favicon e atalho iOS). Sem mudanças na API nem dependências novas.

## Technical Context

**Language/Version**: TypeScript 5.9.3 sobre Node.js 26.13.0; React 19.3.1 (inalterados)

**Primary Dependencies**: as das specs 004 e 007; **nenhuma dependência nova** — cópia pela API nativa
do navegador

**Storage**: N/A — dados só em memória; nada novo no `sessionStorage`

**Testing**: Vitest 5.1.0 + Testing Library + MSW 3.0.3; Playwright 1.64.1 + `@axe-core/playwright`
4.14.0 contra a API real do Compose (research R9)

**Target Platform**: navegadores atuais desktop e mobile; nginx no Compose (inalterado)

**Project Type**: web application (frontend SPA consumindo a API existente)

**Performance Goals**: nenhuma requisição adicional; o início mantém ≤ 2 s após o login (e2e atual)

**Constraints**: WCAG 2.2 AA; sem rolagem horizontal de 320 a 1920 px; valores sempre como texto;
agência, número e dígito fora da telemetria; contrato e transferência inalterados

**Scale/Scope**: 1 cartão novo em 1 tela (PF e PJ, ×2 layouts), 1 hook, 1 módulo de formatação, 8
pontos de texto e 3 ícones

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com clarificações, issue #51, branch `013-frontend-dados-bancarios` = pasta | ✅ |
| II. Isolamento de camadas | Backend inalterado; só `src/api/` acessa a rede, e a tela usa `useCarteira` | ✅ |
| III. Erros padronizados | Só muda o texto de `destinatario-nao-encontrado`, ainda traduzido pelo `type` | ✅ |
| IV. Integridade financeira | Nenhuma operação financeira; transferência inalterada | ✅ |
| V. Contratos explícitos | Tipos regenerados do OpenAPI 1.1.0 (aditivo); [contracts/frontend.md](contracts/frontend.md) v1.3.0 | ✅ |
| VI. Observabilidade | Número da conta é dado sensível e fica fora da telemetria, verificado em e2e (R8); nenhum caso de uso que peça métrica nova | ✅ |
| VII. Qualidade verificável | Testes unitários, de componentes e e2e contra a API real, desktop e mobile, no CI (R9) | ✅ |
| Restrições: toda tela com desktop e mobile especificada e validada | FR-001a e FR-012; e2e nos projetos `desktop`/`mobile` e larguras 320–1920 | ✅ |
| Restrições: dependência justificada | Nenhuma dependência nova | ✅ |
| Restrições: segredos não versionados | Nada novo | ✅ |
| Convenções: português e linguagem ubíqua | `DadosDaConta`, `useCopia`, `contaBancaria`, `NOME_BANCO` | ✅ |

**Resultado pré-pesquisa**: aprovado.
**Re-check pós-design (Phase 1)**: aprovado, sem violações.

## Project Structure

### Documentation (this feature)

```text
specs/013-frontend-dados-bancarios/
├── plan.md                 # Este arquivo
├── research.md             # Phase 0: decisões R1–R9
├── data-model.md           # Phase 1: conta bancária e visão derivada
├── quickstart.md           # Phase 1: roteiro de validação
├── contracts/
│   └── frontend.md         # Phase 1: contrato do frontend v1.3.0 (delta da v1.2.0)
├── checklists/
│   └── requirements.md
└── tasks.md                # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
/
├── docs/
│   ├── desenvolvimento.md                  # + dados da conta no frontend
│   └── design/prototipo.md                 # nome, ícone e cartão novos (FR-015)
└── src/frontend/
    ├── index.html                          # título e descrição "One Bank"
    ├── public/favicon.svg, apple-touch-icon.png   # "1B" (R7)
    ├── src/
    │   ├── marca.ts                        # NOME_BANCO (R6)
    │   ├── marca.test.ts                   # nome antigo ausente (SC-003)
    │   ├── api/
    │   │   ├── schema.d.ts                 # regenerado (npm run api:tipos)
    │   │   ├── cliente.ts                  # + tipo ContaBancaria
    │   │   └── erros.ts                    # mensagem com NOME_BANCO
    │   ├── formatacao/
    │   │   └── contaBancaria.ts, .test.ts  # tipo, conta-dígito, leitura, texto de cópia (R2)
    │   ├── design/componentes/
    │   │   ├── Feedback.tsx                # Marca: "1B" + One Bank
    │   │   └── componentes.module.css      # fonte do ícone
    │   ├── paginas/
    │   │   ├── DadosDaConta.tsx            # cartão (R3, R5)
    │   │   ├── useCopia.ts                 # cópia com aviso (R4)
    │   │   ├── TextoAvisoCopia.tsx         # região de aviso, com estilo de erro na falha (R4)
    │   │   ├── Inicio.tsx                  # cartão em PF e PJ; CNPJ usa useCopia
    │   │   ├── Autenticado.module.css      # grade do cartão e coluna da PJ
    │   │   ├── comum.ts, Comprovante.tsx, NaoEncontrada.tsx   # NOME_BANCO
    │   │   └── *.test.tsx                  # Inicio, Comprovante, Transferir, ConfirmarTransferencia
    │   ├── App.test.tsx, api/erros.test.ts # textos novos
    │   └── testes/handlers.ts              # + contaBancaria no mock
    └── e2e/
        ├── inicio.spec.ts                  # valores da API, posição, cópia, axe
        ├── teclado.spec.ts                 # "Copiar" pelo teclado
        ├── telemetria.spec.ts              # número da conta fora dos sinais
        └── transferencia.spec.ts           # texto novo
```

**Structure Decision**: mesma aplicação das specs 004 e 007. A regra de apresentação fica em
`formatacao/`, a cópia num hook e o cartão em arquivo próprio; `Inicio.tsx` só posiciona.

## Fases de entrega

1. **Base**: tipos regenerados, tipo `ContaBancaria`, mock com `contaBancaria`, `contaBancaria.ts`,
   `src/marca.ts`.
2. **US1**: cartão em PF e PJ, leitura acessível, texto de apoio, layout.
3. **US2**: `NOME_BANCO`, textos, título, ícone "1B", teste do nome antigo.
4. **US3**: `useCopia`, ação de copiar, CNPJ migrado.
5. **Transversais**: e2e (valores, cópia, teclado, telemetria), documentação, roteiro do quickstart.

O texto de apoio da US1 usa `NOME_BANCO`; por isso `src/marca.ts` entra na Base.

## Complexity Tracking

Nenhuma violação a justificar.
