---

description: "Lista de tarefas da feature 013-frontend-dados-bancarios"
---

# Tasks: Frontend — dados bancários da conta e nome One Bank

**Input**: Design documents from `specs/013-frontend-dados-bancarios/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/frontend.md](contracts/frontend.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (FR-014; constituição, Princípio VII): Vitest (unitários e componentes com
MSW) e Playwright contra a API real. Em cada história os testes são escritos primeiro e devem falhar
antes da implementação.

**Organization**: fundação (tipos da API, mock, formatação, nome do banco); US1 (cartão "Dados da
conta"); US2 (One Bank e "1B"); US3 (copiar); por fim, telemetria, documentação e validação.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Frontend em `src/frontend/` (abreviado `front/`); comandos `npm` rodam nele. Só `front/src/api/`
acessa a rede. Textos exatos da tela vêm de [contracts/frontend.md](contracts/frontend.md) e
[data-model.md](data-model.md).

---

## Phase 1: Setup

**Purpose**: ambiente no ar, para gerar os tipos e rodar o e2e

- [ ] T001 Subir o Compose na raiz (`docker compose up -d --build --wait`) e instalar dependências
  com `npm ci` em `src/frontend/` e o navegador com `npx playwright install chromium`; registrar o
  estado de partida com `npm run lint`, `npm run typecheck` e `npm test` verdes

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: tipo da conta bancária, mock, regras de apresentação e nome do banco, usados pelas
três histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

- [ ] T002 Regenerar `front/src/api/schema.d.ts` com `npm run api:tipos` e
  `BANK_API_URL=http://localhost:8080` (API do Compose); conferir no diff que só entram
  `contaBancaria` em `MinhaCarteiraResposta`, `ContaBancariaResposta` e `BancoResposta`, com todos os
  campos `string`; qualquer outra diferença é analisada antes de seguir
- [ ] T003 Em `front/src/api/cliente.ts`, exportar `export type ContaBancaria =
  Esquemas['ContaBancariaResposta'];` junto de `MinhaCarteira` (R1)
- [ ] T004 [P] Em `front/src/testes/handlers.ts`, criar `contaBancaria(alteracoes?)` com o exemplo do
  contrato (`{ banco: { codigo: '001', nome: 'One Bank' }, agencia: '0001', numero: '00000042',
  digito: '0', tipo: 'ContaCorrente' }`) e incluir `contaBancaria: contaBancaria()` em
  `carteira(tipoConta)`
- [ ] T005 [P] Escrever `front/src/formatacao/contaBancaria.test.ts` (deve falhar): `nomeTipoContaBancaria`
  (`ContaCorrente` → "Conta corrente", `ContaPoupanca` → "Conta poupança", qualquer outro → "Conta");
  `nomeBanco` → "001 – One Bank"; `contaComDigito` → "00000042-0", preservando zeros e dígito `0`;
  `lerDigitos('0001')` → "0 0 0 1"; `lerConta` → "0 0 0 0 0 0 4 2, dígito 0"; `textoDadosDaConta` →
  exatamente `Banco 001 – One Bank\nAgência 0001\nConta corrente 00000042-0`, sem linha final; com
  poupança, `Conta poupança …`; com tipo desconhecido, `Conta …` (FR-003 a FR-006)
- [ ] T006 Criar `front/src/formatacao/contaBancaria.ts` com as funções de T005, puras e só com texto
  (sem `Number`, `parseInt` nem `+` unário), até T005 passar (R2)
- [ ] T007 [P] Criar `front/src/marca.ts` com `export const NOME_BANCO = 'One Bank';` e comentário
  citando a spec 013 e que o nome exibido com o código do banco vem da API (R6, FR-002)

**Checkpoint**: `npm run typecheck` e `npm test` verdes; nenhuma tela mudou

---

## Phase 3: User Story 1 - Ver os dados bancários da própria conta (Priority: P1) 🎯 MVP

**Goal**: cartão "Dados da conta" no início de PF e PJ, com os valores da API, leitura acessível e
texto de apoio

**Independent Test**: entrar com PF, PJ e conta poupança e conferir, em desktop e mobile, que banco,
agência, conta-dígito e tipo são os entregues pela API para aquela carteira

### Tests for User Story 1 ⚠️

- [ ] T008 [P] [US1] Em `front/src/paginas/Inicio.test.tsx`, novo `describe` "Início — dados da conta
  (specs/013, US1)" (deve falhar): PF com região "Dados da conta", `h2`, Banco "001 – One Bank",
  Agência "0001", Conta "00000042-0", Tipo "Conta corrente" e "Para receber de clientes One Bank,
  informe o seu CPF."; PJ com os mesmos campos, "…o seu CNPJ.", depois de "Receber transferências" na
  ordem do documento, sem o número do CNPJ dentro da região; `ContaPoupanca` → "Conta poupança"; tipo
  desconhecido → "Conta"; nome do banco vindo da API (mock com outro nome); cada `dt` associado ao
  seu `dd`; texto para leitores "0 0 0 1" e "0 0 0 0 0 0 4 2, dígito 0" com o valor visível em
  `aria-hidden`; carregando ou erro da carteira → sem a região (US1.1 a US1.7)
- [ ] T009 [P] [US1] Em `front/e2e/apoio.ts`, criar `contaBancariaPelaApi(page, conta)` (usa
  `tokenPelaApi` e `GET /api/v1/carteiras/minha`, devolve `contaBancaria`); em
  `front/e2e/inicio.spec.ts`, novo `describe` "Início — dados da conta (spec 013)" com PF e PJ: cada
  valor igual ao da API, texto de apoio, `semViolacoesA11y`, `semRolagemHorizontal` e posição por
  `boundingBox` — desktop PF: à direita do saldo; desktop PJ: abaixo de "Receber transferências" e na
  mesma coluna; mobile: abaixo do saldo (PF) e do CNPJ (PJ) e acima das movimentações (FR-001a); nome de banco longo, com a resposta
  alterada por `page.route`, a 320 px, sem rolagem horizontal (Edge Cases)

### Implementation for User Story 1

- [ ] T010 [US1] Criar `front/src/paginas/DadosDaConta.tsx`: `DadosDaConta({ conta, documento }: {
  conta: ContaBancaria; documento: 'CPF' | 'CNPJ' })` com `Cartao rotulo="Dados da conta"`, `h2` em
  `.cabecalhoSecao`, `<dl>` com Banco, Agência, Conta e Tipo, nessa ordem, agência e conta com
  `aria-hidden` + `.apenas-leitor-de-tela` (R3), e o parágrafo de apoio com `NOME_BANCO` (FR-001,
  FR-004, FR-011)
- [ ] T011 [US1] Em `front/src/paginas/Autenticado.module.css`, estilos do cartão: grade de duas
  colunas `minmax(0, 1fr)` em todas as larguras, `overflow-wrap: anywhere`, algarismos tabulares,
  `user-select: none` no texto só para leitores, texto de apoio suave; coluna empilhada da PJ e
  saldo alinhado ao topo quando ela existe (R5)
- [ ] T012 [US1] Em `front/src/paginas/Inicio.tsx`, incluir `<DadosDaConta>` em `.resumo`: na PF
  (`documento="CPF"`) logo após o saldo; na PJ (`documento="CNPJ"`), dentro de uma coluna com
  "Receber transferências" primeiro; T008 verde

**Checkpoint**: US1 completa; T009 verde com a imagem do frontend reconstruída

---

## Phase 4: User Story 2 - Reconhecer o banco como One Bank (Priority: P2)

**Goal**: nome "One Bank" em toda a interface e ícone "1B" na marca, na aba e no atalho

**Independent Test**: percorrer todas as telas e o comprovante compartilhado sem encontrar "Bank" sem
"One" nem o ícone "B"

### Tests for User Story 2 ⚠️

- [ ] T013 [P] [US2] Criar `front/src/marca.test.ts` (deve falhar): com `import.meta.glob` (`?raw`,
  `eager`) de `./**/*.{ts,tsx}` (exceto `*.test.*` e `api/schema.d.ts`, gerado) e `../index.html`, nenhum arquivo contém `Bank`
  como palavra sem "One " antes (`/(?<!One )\bBank\b/`), e a lista de arquivos lidos não é vazia e
  inclui `index.html` (SC-003, R6)
- [ ] T014 [P] [US2] Atualizar os testes com os textos novos (devem falhar): `front/src/App.test.tsx`
  (título "Criar conta · One Bank"; marca com nome acessível exatamente "One Bank" e "1B" em
  `aria-hidden`; texto da página não encontrada), `front/src/api/erros.test.ts`,
  `front/src/paginas/Transferir.test.tsx`, `front/src/paginas/ConfirmarTransferencia.test.tsx`
  ("Nenhuma conta One Bank com esse documento."), `front/src/paginas/Comprovante.test.tsx` (resumo
  nativo e copiado começa com "Comprovante de transferência — One Bank") e
  `front/src/paginas/Inicio.test.tsx` ("Clientes com conta One Bank")
- [ ] T015 [P] [US2] Em `front/e2e/transferencia.spec.ts`, trocar a mensagem por "Nenhuma conta One
  Bank com esse documento."; em `front/e2e/inicio.spec.ts`, conferir o título "Início · One Bank"

### Implementation for User Story 2

- [ ] T016 [US2] Usar `NOME_BANCO` em `front/src/paginas/comum.ts` (título), `front/src/api/erros.ts`,
  `front/src/paginas/Comprovante.tsx`, `front/src/paginas/Inicio.tsx` e
  `front/src/paginas/NaoEncontrada.tsx`, e o texto direto em `front/index.html` (título e descrição);
  nomes técnicos (`bank.*`, `bank-frontend`, `BANK_API_URL`) ficam como estão (FR-008)
- [ ] T017 [US2] Em `front/src/design/componentes/Feedback.tsx`, `Marca` com "1B" em `aria-hidden` e
  `NOME_BANCO`; em `front/src/design/componentes/componentes.module.css`, reduzir a fonte de
  `.marcaIcone` para o "1B" caber em 28 px sem mudar cor, raio e tamanho (FR-009, FR-010)
- [ ] T018 [P] [US2] Redesenhar `front/public/favicon.svg` com contornos (`<path>`) de "1" e "B"
  brancos sobre o `rect` `#1d4ed8` atual (sem `<text>`), atualizando o comentário do arquivo; conferir
  por captura no Chromium, em 16 e 32 px, que "1" e "B" se distinguem (FR-009, R7)
- [ ] T019 [US2] Gerar `front/public/apple-touch-icon.png` (180×180, fundo `#1d4ed8` sem cantos
  arredondados nem transparência, como o atual) a partir do desenho de T018 com o Chromium do
  Playwright, por script temporário fora do repositório; conferir a imagem (R7)

**Checkpoint**: T013 a T015 verdes; nenhuma tela com o nome ou o ícone antigos

---

## Phase 5: User Story 3 - Copiar os dados bancários (Priority: P3)

**Goal**: copiar as três linhas com um acionamento, com aviso de sucesso ou falha

**Independent Test**: acionar "Copiar" por mouse, toque e teclado e conferir o texto colado e o aviso;
repetir com a cópia bloqueada

### Tests for User Story 3 ⚠️

- [ ] T020 [P] [US3] Em `front/src/paginas/Inicio.test.tsx` (deve falhar): "Copiar dados da conta"
  grava exatamente as três linhas e mostra "Dados da conta copiados" no `status` do cartão; não contém
  nome, documento nem saldo; `writeText` rejeitando e `navigator.clipboard` ausente → "Não foi
  possível copiar. Selecione os dados e copie manualmente." com a classe de erro (idem na falha do
  CNPJ, com o texto atual); copiar de novo limpa o aviso antes de
  mostrá-lo; na PJ, o aviso de um cartão não aparece no outro. Trocar, no teste do CNPJ, o
  `getByRole('status')` único pela busca do aviso "CNPJ copiado" (R4)
- [ ] T021 [P] [US3] Em `front/e2e/inicio.spec.ts`, com `grantPermissions(['clipboard-read',
  'clipboard-write'])`, copiar em PF (desktop e mobile) e comparar a área de transferência com o
  texto montado a partir da API; em `front/e2e/teclado.spec.ts`, chegar a "Copiar dados da conta" com
  `tabAte`, acionar com Enter e ver o aviso; seleção manual do cartão com
  `getSelection().toString()` sem o texto só para leitores (US3.2, R3)

### Implementation for User Story 3

- [ ] T022 [US3] Criar `front/src/paginas/useCopia.ts`: `useCopia()` devolve `{ aviso, copiar(texto, {
  sucesso, falha }) }`, com `aviso: { texto, falhou }`; limpa o aviso antes de copiar e trata rejeição e `navigator.clipboard` ausente
  como falha (R4)
- [ ] T023 [US3] Em `front/src/paginas/Inicio.tsx`, `CnpjParaReceber` passa a usar `useCopia`, com os
  mesmos textos e a cor de erro na falha (FR-007)
- [ ] T024 [US3] Em `front/src/paginas/DadosDaConta.tsx`, botão "Copiar" (`variante="secundario"`,
  `aria-label="Copiar dados da conta"`) que copia `textoDadosDaConta(conta)` e `<p role="status"
  aria-live="polite">` próprio, com os textos do contrato; em
  `front/src/paginas/Autenticado.module.css`, estilos e variante de erro do aviso (FR-006, FR-007)

**Checkpoint**: T020 e T021 verdes; as três histórias funcionam juntas

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: telemetria, documentação e validação final

- [ ] T025 [P] Em `front/e2e/telemetria.spec.ts`, novo caso: entrar com PF, copiar os dados da conta,
  esperar envios OTLP e conferir que `numero` (delimitado: `(?<!\d)numero(?!\d)`), `numero-digito` e os
  nomes `agencia`/`contaBancaria` não aparecem em nenhum corpo (FR-013, SC-005, R8)
- [ ] T026 [P] Em `docs/design/prototipo.md`, registrar o nome "One Bank", o ícone "1B" e o cartão
  "Dados da conta" (ausentes das telas originais) e trocar a mensagem por "Nenhuma conta One Bank com
  esse documento." (FR-015)
- [ ] T027 [P] Em `docs/desenvolvimento.md`, seção curta "Dados da conta e nome One Bank (frontend)" com
  links para a spec 013, o contrato v1.3.0 e o quickstart, e a conta poupança de teste já documentada
- [ ] T028 Rodar `npm run lint`, `npm run typecheck`, `npm test` e `npm run build` em `src/frontend/`,
  reconstruir o frontend no Compose e rodar `npm run e2e` (desktop e mobile), tudo verde
- [ ] T029 Executar o roteiro de `specs/013-frontend-dados-bancarios/quickstart.md`, inclusive a conta
  poupança e os ícones, conferindo capturas em desktop e mobile; registrar o que não puder ser
  verificado aqui (leitor de tela e atalho no celular)
- [ ] T030 Conferir spec, contrato e código lado a lado e atualizar os documentos de
  `specs/013-frontend-dados-bancarios/` se algo divergiu (Princípio I)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → histórias
- **US1 (3)** e **US2 (4)** dependem só da fundação; as duas alteram `Inicio.tsx`,
  `Inicio.test.tsx` e `e2e/inicio.spec.ts` (T009, T015 e T021), então essas tarefas não rodam ao
  mesmo tempo
- **US3 (5)** depende da US1 (o botão fica no cartão)
- **Polish (6)** depois das histórias; T025 depende da US3

### Within Each User Story

- Testes primeiro, falhando; depois componente, estilos e integração
- T006 depende de T003 e T005; T008 depende de T004; T010 depende de T006 e T007; T012 depende de
  T010 e T011; T016 e T017 dependem de T007; T019 depende de T018; T023 e T024 dependem de T022; T024 também depende de T010

### Parallel Opportunities

- Fundação: T004, T005 e T007
- US1: T008 e T009; US2: T013, T014, T015 e T018; US3: T020 e T021
- Polish: T025, T026 e T027

---

## Parallel Example: User Story 2

```text
T013 marca.test.ts          T014 testes com os textos novos
T015 e2e com os textos novos T018 favicon.svg
```

---

## Implementation Strategy

### MVP First

1. Fases 1 e 2
2. US1 → o titular vê os dados da própria conta
3. **PARAR e VALIDAR** em desktop e mobile (quickstart, passos 1 a 5 e 11)

### Incremental Delivery

1. US2 → um só nome e um só ícone
2. US3 → copiar
3. Polish → telemetria, documentação e quickstart

### Commits

Conventional Commits por fase, escopo `frontend-dados-bancarios` (por exemplo
`feat(frontend-dados-bancarios): ...`); PR com `Closes #51`.
