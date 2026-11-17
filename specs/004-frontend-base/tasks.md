---

description: "Lista de tarefas da feature 004-frontend-base"
---

# Tasks: Frontend base — acesso, cadastro e carteira

**Input**: Design documents from `specs/004-frontend-base/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/frontend.md](contracts/frontend.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (FR-022 e constituição, Princípio VII): unitários/componentes com
Vitest + Testing Library + MSW e ponta a ponta com Playwright + axe contra a API real do
Compose. Em cada história, os testes são escritos primeiro e devem falhar antes da
implementação.

**Organization**: tarefas agrupadas por história de usuário (US1–US5), mais uma fase
bloqueada pela issue #18.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Frontend em `src/frontend/` (abreviado abaixo como `front/`). Arquivos de raiz
(`docker-compose.yml`, `.env.example`, `.github/`, `docs/`) usam o caminho completo. Comandos
`npm` rodam em `src/frontend/` com Node 26.13.0.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: projeto, ferramentas e configuração de build/teste

- [X] T001 Criar `front/.nvmrc` (`26.13.0`) e `front/package.json` (`"private": true`,
  `"type": "module"`, `"engines": { "node": ">=26.13.0 <27" }`) com as versões **exatas** de
  research R2, R3 e R7 (React 19.3.1, `@types/react`/`@types/react-dom` 19.3.0, TypeScript
  5.9.3, Vite 8.3.5, `@vitejs/plugin-react` 6.2.1, `react-router` 8.5.1, `openapi-fetch`
  0.17.0, `openapi-typescript` 7.13.0, fontes 5.3.0, Vitest 5.1.0, jsdom 30.1.3, Testing
  Library 16.3.3 / 14.6.8 / 7.0.1, MSW 3.0.3, Playwright 1.64.1, `@axe-core/playwright`
  4.14.0, ESLint e `@eslint/js` 9.39.5, `typescript-eslint` 8.72.1,
  `eslint-plugin-react-hooks` 7.1.1, `eslint-plugin-jsx-a11y` 6.10.2, `globals` 17.14.0,
  OpenTelemetry 1.9.1 / 2.12.0 / 0.223.0 / 0.68.0 / 1.44.0) e os scripts do contrato (`dev`,
  `build`, `lint`, `typecheck`, `test`, `e2e`, `api:tipos`); executar `npm install` para gerar
  `front/package-lock.json` (versionado)
- [X] T002 [P] Criar `front/tsconfig.json`, `front/tsconfig.app.json` (`strict`,
  `noUncheckedIndexedAccess`, `jsx: react-jsx`, `moduleResolution: bundler`) e
  `front/tsconfig.node.json` (configs do Vite/Playwright)
- [X] T003 [P] Criar `front/vite.config.ts`: plugin React; proxy `/api` →
  `process.env.BANK_API_URL ?? 'http://localhost:5080'` e `/otlp` →
  `process.env.BANK_OTLP_URL ?? 'http://localhost:4318'` (removendo o prefixo `/otlp`); bloco
  `test` do Vitest (`environment: 'jsdom'`, `setupFiles: ['src/testes/setup.ts']`,
  `include: ['src/**/*.test.{ts,tsx}']`)
- [X] T004 [P] Criar `front/eslint.config.js` (flat config): `@eslint/js` recommended,
  `typescript-eslint` recommendedTypeChecked, `react-hooks`, `jsx-a11y` recommended; e a regra
  de isolamento do plano: em `src/paginas/**` e `src/design/**`, `no-restricted-globals`
  (`fetch`) e `no-restricted-imports` de `openapi-fetch` (somente `src/api/**` fala com a rede)
- [X] T005 [P] Criar `front/index.html` (`lang="pt-BR"`, `<meta name="viewport">`, título
  "Bank") e `front/src/main.tsx`/`front/src/App.tsx` mínimos (página em branco com o nome)
- [X] T006 [P] Criar `front/playwright.config.ts`: `baseURL` de `E2E_BASE_URL` (padrão
  `http://localhost:3000`); projetos `desktop` (1440 × 900, Chromium) e `mobile` (390 × 844,
  `isMobile`, `hasTouch`, Chromium); `testDir: 'e2e'`; trace em falha
- [X] T007 Executar `npm run lint`, `npm run typecheck`, `npm test -- --passWithNoTests` e
  `npm run build` e confirmar tudo sem erros

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: design system, formatação, cliente da API, sessão, rotas, telemetria, ambiente
e CI — usados por todas as histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Testes (escrever primeiro, devem falhar)

- [X] T008 [P] Criar `front/src/testes/setup.ts` (jest-dom, servidor MSW com
  `onUnhandledRequest: 'error'`, limpeza de `sessionStorage` entre testes) e
  `front/src/testes/handlers.ts` (respostas padrão dos endpoints da spec 003 e um
  `ProblemDetails` por `type` do contrato)
- [X] T009 [P] Criar `front/src/formatacao/mascaras.test.ts`: CPF `52998224725` →
  `529.982.247-25` (parcial durante a digitação; só dígitos); CNPJ `12abc34501de35` →
  `12.ABC.345/01DE-35`; letras nas posições 13–14 rejeitadas; caracteres fora de
  `[0-9A-Za-z]` ignorados; colar valor mascarado ou com espaços normaliza; documento mascarado
  do perfil: CPF `•••.982.247-••`, CNPJ `••.•••.345/01DE-••`
- [X] T010 [P] Criar `front/src/formatacao/moeda.test.ts` e `data.test.ts`: `10` →
  `R$ 10,00`, `1234.56` → `R$ 1.234,56`, `0` → `R$ 0,00`; data UTC formatada no fuso do
  navegador no padrão `17 nov 2026, 14:32`
- [X] T011 [P] Criar `front/src/api/erros.test.ts` com um caso por linha da tabela
  "Tradução de erros" de [data-model.md](data-model.md#tradução-de-erros): `validacao` com
  campos conhecidos e desconhecidos, `email-ja-cadastrado`, `documento-ja-cadastrado`,
  `credenciais-invalidas`, `nao-autenticado`, `acesso-negado`, `carteira-nao-encontrada`,
  `usuario-nao-encontrado`, rede indisponível, 500 com `traceId`, corpo não-JSON; o `detail`
  nunca é usado como mensagem principal (SC-003)
- [X] T012 [P] Criar `front/src/sessao/sessao.test.tsx`: salvar/ler `bank.sessao`
  (`{ token, expiraEm }`); claims decodificadas (`sub`, `email`, `tipo_conta`,
  `carteira_id`); token sem claims ou ilegível descartado; sessão vencida descartada ao
  iniciar; temporizador encerra a sessão em `expiraEm`; `sair` remove a chave; middleware
  adiciona `Authorization: Bearer` e, em 401, limpa a sessão e navega para
  `/entrar?expirada=1` guardando a rota de retorno
- [X] T013 [P] Criar `front/src/App.test.tsx`: sem sessão, `/` e `/perfil` redirecionam para
  `/entrar` com retorno; com sessão, `/entrar` e `/criar-conta` redirecionam para `/`; rota
  desconhecida mostra "Página não encontrada" com link para o início
- [X] T014 [P] Criar `front/src/telemetria/telemetria.test.ts`: desligada por padrão
  (`VITE_TELEMETRIA_HABILITADA` ausente → nenhum provedor criado); quando ligada, `service.name`
  `bank-frontend`; processador de spans remove atributos com corpo/cabeçalhos e o exportador
  não recebe senha, token, documento nem e-mail (FR-019c); falha no exportador não lança erro

### Implementação

- [X] T015 [P] Criar `front/src/design/tokens.css` e `global.css` com os tokens do
  [protótipo](../../docs/design/prototipo.md): cores (`#1D4ED8`, `#0F172A`, `#475569`,
  `#F5F7FA`, `#E2E8F0`, `#047857`, `#B42318`, `#B54708`), escala tipográfica desktop/mobile
  (Display 40/48 e 32/40 … Caption 12/16, Mono 13/20 e 12/16), raios 10/16 px, espaçamento em
  múltiplos de 4 px, `font-variant-numeric: tabular-nums` para valores, foco visível
  (`:focus-visible`) com contraste ≥ 3:1, breakpoint desktop `≥ 1024px`; importar
  `@fontsource-variable/inter` e `@fontsource/jetbrains-mono` em `main.tsx`
- [X] T016 [P] Criar os componentes base em `front/src/design/componentes/` (CSS Modules):
  `Botao` (48 px desktop / 52 px mobile; estado de progresso com `aria-busy` e desabilitado),
  `CampoTexto` (rótulo associado, mensagem de erro ligada por `aria-describedby`,
  `aria-invalid`), `CampoSenha` (mostrar/ocultar com botão acessível), `Aviso` (variantes
  erro/atenção/informação; `role="alert"` para erros), `Carregando` (`aria-live="polite"`),
  `EstadoVazio`, `Cartao`; alvos de toque ≥ 44 px
- [X] T017 [P] Implementar `front/src/formatacao/mascaras.ts`, `moeda.ts` e `data.ts` (faz
  T009–T010 passar)
- [X] T018 Executar a API (Compose ou SDK) e `npm run api:tipos` para gerar
  `front/src/api/schema.d.ts` a partir de `/openapi/v1.json`; versionar o arquivo gerado
- [X] T019 Implementar `front/src/api/erros.ts` (`traduzirErro` → `{ mensagem, campos?,
  traceId?, acao? }`) e `front/src/api/cliente.ts` (`createClient<paths>` com `baseUrl: ''`,
  middleware de token/401 recebido da sessão, funções `registrar`, `entrar`,
  `obterMinhaCarteira` que devolvem resultado tipado `{ ok, dados } | { ok: false, erro }`)
  (faz T011 passar)
- [X] T020 Implementar `front/src/sessao/` (`armazenamento.ts`, `claims.ts`,
  `ContextoSessao.tsx` com `entrar`, `sair`, `sessao`, temporizador de expiração, e
  `RotaProtegida.tsx`) (faz T012 passar)
- [X] T021 Implementar `front/src/design/componentes/Layout.tsx` e `Navegacao.tsx`: menu
  lateral (desktop) / barra inferior (mobile) com os itens por perfil do
  [contrato](contracts/frontend.md#navegação-por-perfil) — "Em breve" visível, não navegável
  (`aria-disabled`), item ativo com `aria-current="page"`; link "Pular para o conteúdo"
- [X] T022 Implementar `front/src/App.tsx` (rotas `/entrar`, `/criar-conta`, `/`, `/perfil`,
  `*`) e `front/src/paginas/NaoEncontrada.tsx`; atualizar `front/src/main.tsx` (provedor de
  sessão, roteador, título da página por rota) (faz T013 passar)
- [X] T023 Implementar `front/src/telemetria/telemetria.ts` (R7): carregamento sob demanda
  quando `import.meta.env.VITE_TELEMETRIA_HABILITADA === 'true'`; `WebTracerProvider` com
  `ZoneContextManager`, instrumentações document-load e fetch (propagação de `traceparent` na
  mesma origem, sem corpo/cabeçalhos), span por navegação de rota, `LoggerProvider` para
  `window.onerror`/`unhandledrejection` e falhas de API (só `type`, status, `traceId`),
  `MeterProvider` com histograma de carregamento por rota; exportadores OTLP/HTTP em
  `VITE_OTLP_URL` (`/otlp`); erros de exportação engolidos (faz T014 passar)
- [X] T024 [P] Criar `front/Dockerfile` (estágio `node:26.13.0-alpine`: `npm ci`, `npm run
  build` com `ARG VITE_TELEMETRIA_HABILITADA=false`, `ARG VITE_OTLP_URL=/otlp`; estágio
  `nginx:1.30.6-alpine` com `dist/`), `front/.dockerignore` (`node_modules`, `dist`,
  `e2e`, relatórios) e `front/nginx/default.conf`: estáticos com cache para `assets/`,
  fallback SPA para `index.html`, `location /api/` → `http://api:8080`, `location /otlp/` →
  `http://aspire-dashboard:18890/` (removendo o prefixo), `location = /saude` → 200, cabeçalhos
  `Content-Security-Policy: default-src 'self'; img-src 'self' data:; style-src 'self'
  'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'`, `X-Content-Type-Options:
  nosniff`, `Referrer-Policy: no-referrer`
- [X] T025 Adicionar o serviço `frontend` ao `docker-compose.yml` (build `./src/frontend` com
  `VITE_TELEMETRIA_HABILITADA: "true"`; porta `127.0.0.1:${FRONTEND_PORT:-3000}:80`;
  `depends_on: api: condition: service_healthy`; healthcheck `wget -q -O /dev/null
  http://localhost/saude`), citar `specs/004-frontend-base/contracts/frontend.md` no cabeçalho
  e acrescentar `FRONTEND_PORT=3000` ao `.env.example`
- [X] T026 [P] Atualizar `.github/workflows/ci.yml` com o job `frontend` (`actions/setup-node`
  com `node-version-file: src/frontend/.nvmrc` e cache npm; `npm ci`, `lint`, `typecheck`,
  `test`, `build`) e o job `frontend-e2e` (`docker compose up -d --build --wait`; `npx
  playwright install --with-deps chromium`; `npm run e2e`; em falha, `docker compose logs` e
  upload do relatório do Playwright); incluir `src/frontend/**` nos paths de
  `.github/workflows/infra-local.yml`
- [X] T027 [P] Criar `front/e2e/apoio.ts` com geradores de CPF e CNPJ alfanumérico válidos e
  e-mails únicos (mesma regra dos testes de integração do backend) e helpers `criarConta`,
  `entrar`, `semRolagemHorizontal(page, larguras)` e `semViolacoesA11y(page)` (axe com tags
  `wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`)
- [X] T028 Executar `npm test`, `npm run build` e `docker compose up -d --build --wait`;
  confirmar o serviço `frontend` saudável e a página "não encontrada" servida em
  `http://localhost:3000/qualquer`

**Checkpoint**: base pronta; histórias podem começar

---

## Phase 3: User Story 1 - Criar conta pelo navegador (Priority: P1) 🎯 MVP

**Goal**: cadastro PF/PJ com máscaras, erros por campo, mensagens de conflito e entrada
automática

**Independent Test**: criar conta PF e PJ (CNPJ alfanumérico) em desktop e mobile e chegar ao
início; provocar 400 e 409 e ver as mensagens do protótipo (quickstart cenário 4, passos 1–3)

### Testes (escrever primeiro, devem falhar)

- [X] T029 [P] [US1] Criar `front/src/paginas/CriarConta.test.tsx`: alternar PF/PJ muda
  rótulos ("Nome completo"/"Razão social", "CPF"/"CNPJ"), máscara e o texto do saldo inicial
  ("R$ 10,00"/"R$ 0,00") e limpa o documento; aviso "O tipo de conta não pode ser alterado
  depois do cadastro"; CNPJ digitado em minúsculas aparece em maiúsculas; obrigatoriedade e
  limite de 150 caracteres no nome validados localmente; indicador da política de senha;
  400 da API → cada erro abaixo do campo e foco no primeiro campo com erro, campo desconhecido
  → mensagem geral; 409 e-mail/documento → mensagem específica com link "Ir para o login";
  valores digitados preservados após erro; duplo clique envia uma única requisição; sucesso →
  login automático e navegação para `/`
- [X] T030 [P] [US1] Criar `front/e2e/cadastro.spec.ts` (desktop e mobile, API real): cadastro
  PF com CPF mascarado → início com "R$ 10,00"; cadastro PJ com CNPJ alfanumérico minúsculo →
  campo exibe maiúsculas e início com "R$ 0,00"; e-mail repetido → "Este e-mail já tem conta";
  CNPJ com DV inválido (`12ABC34501DE36`) → erro no campo; axe sem violações na tela de
  cadastro (inclusive com erros exibidos)

### Implementação

- [X] T031 [US1] Implementar `front/src/design/componentes/CampoDocumento.tsx` (máscara por
  tipo; `inputMode="numeric"` para CPF, `inputMode="text"` + `autoCapitalize="characters"`
  para CNPJ; `autoComplete="off"`)
- [X] T032 [US1] Implementar `front/src/paginas/CriarConta.tsx` (+ CSS Module) conforme as
  capturas `desktop-02-cadastro.png` e `mobile-02-cadastro.png`: seletor de tipo como grupo de
  rádio acessível, campos com `autoComplete` adequado (`name`/`organization`, `email`,
  `new-password`), envio pelo `cliente.registrar`, tradução de erros, login automático com
  `sessao.entrar` (faz T029 passar)
- [X] T033 [US1] Executar T029–T030 e o quickstart cenário 4 (passos 1–3)

**Checkpoint**: cadastro funcional em desktop e mobile (MVP)

---

## Phase 4: User Story 2 - Entrar e sair da conta (Priority: P1)

**Goal**: login, sessão por aba com expiração, retorno à tela pedida e saída

**Independent Test**: login correto e incorreto, rota protegida sem sessão, sessão expirada e
"Sair" (quickstart cenário 4, passos 4–6)

### Testes (escrever primeiro, devem falhar)

- [X] T034 [P] [US2] Criar `front/src/paginas/Entrar.test.tsx`: e-mail e senha obrigatórios;
  sucesso → `/` ou a rota de retorno; 401 `credenciais-invalidas` → "E-mail ou senha
  incorretos" sem limpar o e-mail; `?expirada=1` → "Sua sessão expirou. Entre novamente.";
  aviso "Ambiente de demonstração"; sem link "Esqueci a senha"; link "Criar conta"; rede
  indisponível → mensagem de conexão; duplo clique em "Entrar" envia uma única requisição
- [X] T035 [P] [US2] Criar `front/e2e/sessao.spec.ts` (desktop e mobile): login com senha
  errada → mensagem única; `/perfil` sem sessão → login → volta ao perfil; recarregar a página
  mantém a sessão; nova aba exige login; sessão com `expiraEm` no passado (ajustando o
  `sessionStorage`) → próxima ação leva ao login com o aviso; "Sair" → telas protegidas
  inacessíveis; o token não aparece na URL nem em nenhuma mensagem do console do navegador
  capturada durante o fluxo (FR-012); axe sem violações na tela de login

### Implementação

- [X] T036 [US2] Implementar `front/src/paginas/Entrar.tsx` (+ CSS Module) conforme
  `desktop-01-login.png` e `mobile-01-login.png` (painel institucional só no desktop), com
  `autoComplete` `email` e `current-password` (faz T034 passar)
- [X] T037 [US2] Adicionar a ação "Sair" ao `Layout`/`Navegacao` (desktop e mobile) usando
  `sessao.sair` e executar T034–T035

**Checkpoint**: cadastro + login/saída funcionais

---

## Phase 5: User Story 3 - Ver a carteira na tela inicial (Priority: P1)

**Goal**: início PF e PJ com saldo atual, estados de carregamento/erro/vazio e regras da RN-06

**Independent Test**: PF recém-criada vê R$ 10,00 com "Transferir"/"Ver extrato" "Em breve";
PJ vê R$ 0,00 sem transferir; recarregar mostra o saldo atual

### Testes (escrever primeiro, devem falhar)

- [X] T038 [P] [US3] Criar `front/src/paginas/Inicio.test.tsx`: PF → "Olá, {primeiro nome}",
  "Pessoa Física", saldo formatado, "Atualizado em …", "Transferir" e "Ver extrato" como "Em
  breve" (não acionáveis), estado vazio "Suas movimentações aparecerão aqui" no lugar de
  entradas/saídas/movimentações; PJ → razão social, "Pessoa Jurídica", saldo, "Contas Pessoa
  Jurídica não enviam transferências", nenhum elemento "Transferir"; carregando sem saldo
  falso; 500 → "Algo deu errado" com `traceId` e "Tentar de novo" (refaz a chamada); 404
  `carteira-nao-encontrada` → login; saldo exibido vem da resposta (sem cache entre montagens)
- [X] T039 [P] [US3] Criar `front/e2e/inicio.spec.ts` (desktop e mobile, API real): PF e PJ
  conforme o cenário acima; ao recarregar, o valor exibido coincide com o de `GET
  /api/v1/carteiras/minha` (não há endpoint de movimentação nesta spec); saldo visível em
  ≤ 2 s após o login (SC-005); axe sem violações

### Implementação

- [X] T040 [US3] Implementar `front/src/paginas/Inicio.tsx`, `InicioPf.tsx` e `InicioPj.tsx`
  (+ CSS Modules) conforme `desktop-03/04` e `mobile-03/04`, com `cliente.obterMinhaCarteira`,
  `Carregando`, `EstadoVazio` e `Aviso` (faz T038 passar)
- [X] T041 [US3] Executar T038–T039

**Checkpoint**: cadastro, sessão e carteira funcionais

---

## Phase 6: User Story 4 - Consultar o perfil (Priority: P2) — parte independente da #18

**Goal**: perfil com nome, e-mail, tipo de conta, permissões da RN-06 e "Sair"

**Independent Test**: abrir o perfil com contas PF e PJ e conferir dados disponíveis,
permissões e "Sair"

### Testes (escrever primeiro, devem falhar)

- [X] T042 [P] [US4] Criar `front/src/paginas/Perfil.test.tsx`: PF → nome (da carteira),
  e-mail (da sessão), "Pessoa Física · não pode ser alterado", permissões "Enviar
  transferências para PF e PJ", "Receber transferências", "Pagar cobranças de cartão de
  crédito"; PJ → "Receber transferências", "Gerar cobranças de cartão de crédito"; "Sair"
  encerra a sessão; nenhum documento ou data fictícios exibidos
- [X] T043 [P] [US4] Criar `front/e2e/perfil.spec.ts` (desktop e mobile): perfil PF e PJ;
  "Sair" a partir do perfil; axe sem violações

### Implementação

- [X] T044 [US4] Implementar `front/src/paginas/Perfil.tsx` (+ CSS Module) conforme
  `desktop-09-perfil.png` e `mobile-09-perfil.png`, com a lista de permissões por perfil num
  único mapa `front/src/sessao/permissoes.ts` (espelho da RN-06 para exibição) (faz T042
  passar)
- [X] T045 [US4] Executar T042–T043

---

## Phase 7: User Story 5 - Mesma experiência no computador e no celular (Priority: P2)

**Goal**: responsividade e acessibilidade verificadas em todas as telas

**Independent Test**: percorrer todas as telas em 320, 390, 768, 1440 e 1920 px e só pelo
teclado

### Testes

- [X] T046 [P] [US5] Criar `front/e2e/responsividade.spec.ts`: para `/entrar`,
  `/criar-conta`, `/` (PF e PJ), `/perfil` e uma rota inexistente, nas larguras 320, 390, 768,
  1440 e 1920 px: sem rolagem horizontal; nome de 150 caracteres sem cortar a tela; alvos
  interativos ≥ 44 × 44 px; navegação lateral no desktop e barra inferior no mobile (SC-002,
  SC-006)
- [X] T047 [P] [US5] Criar `front/e2e/teclado.spec.ts`: cadastro, login e "Sair" concluídos só
  com teclado; foco visível em cada parada; "Pular para o conteúdo" funcional; erros
  anunciados (`role="alert"`) e foco no primeiro campo inválido (FR-001a, SC-006)

### Implementação

- [X] T048 [US5] Corrigir o que T046–T047 apontarem nos componentes e telas e executar a suíte
  e2e completa

**Checkpoint**: todas as histórias independentes da #18 concluídas

---

## Phase 8: Bloqueada pela issue #18 — dados do titular

**Pré-requisito**: `GET /api/v1/usuarios/me` integrado na `main` (issue #18).

- [X] T049 [US4] Executar `npm run api:tipos` com a API atualizada e adicionar
  `obterTitular` a `front/src/api/cliente.ts` (e o caso `usuario-nao-encontrado` em
  `erros.ts`, se ainda não coberto)
- [X] T050 [P] [US4] Estender `front/src/paginas/Perfil.test.tsx` e `e2e/perfil.spec.ts`:
  documento mascarado (CPF `•••.982.247-••`; CNPJ `••.•••.345/01DE-••`) e "Cliente desde
  {data}"
- [X] T051 [P] [US4] Estender `front/src/paginas/Inicio.test.tsx` e `e2e/inicio.spec.ts`:
  início PJ mostra o CNPJ com máscara e "Copiar" grava o valor sem máscara na área de
  transferência e confirma "CNPJ copiado" (anunciado por leitor de tela)
- [X] T052 [US4] Implementar os dados do titular em `Perfil.tsx` e o bloco "Receber
  transferências" com "Copiar" em `InicioPj.tsx` (FR-017, FR-017a) e executar T050–T051

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T053 [P] Criar `front/e2e/telemetria.spec.ts` (SC-008): com o Compose, capturar as
  requisições a `/otlp/v1/*` durante cadastro e login e verificar que nenhum corpo contém
  e-mail, senha, token ou documento; verificar que a chamada de login enviou `traceparent`
- [X] T054 [P] Atualizar `docs/desenvolvimento.md` com a seção Frontend: pré-requisito Node
  26.13.0, `npm ci`, `npm run dev` (proxy para a API pelo SDK), `npm test`, `npm run e2e`,
  `npm run api:tipos`, endereço do Compose (`http://localhost:3000`) e link para o contrato
- [X] T055 [P] Atualizar o `README.md`: parágrafo de **Status** (já há cadastro, login,
  carteira e o frontend base) e o comentário do `docker-compose.yml` na árvore do repositório
  (incluir o `frontend`)
- [ ] T056 Executar todos os cenários do [quickstart.md](quickstart.md) (1–6; 7 após a #18),
  incluindo a conferência visual do trace `bank-frontend` → `bank-api` no Aspire Dashboard e
  a validação manual em Firefox e Safari/WebKit; cronometrar a subida do frontend com a API a
  partir de um clone limpo seguindo só a documentação (SC-007, ≤ 10 min sem downloads);
  registrar resultados e divergências neste arquivo e na spec/plano/contrato (constituição,
  Princípio I); anotar na descrição do PR que, após o merge, os jobs `frontend` e
  `frontend-e2e` devem ser marcados como checks obrigatórios da `main` (FR-021)

### Registro da validação (2026-11-17)

- **Qualidade**: lint e tipos sem erros; **85 testes** de unidade/componentes aprovados; build
  com a telemetria em chunk separado (não entra no bundle inicial).
- **Ponta a ponta contra o Compose** (`http://localhost:3000`, API real): **33 aprovados** em
  desktop (1440 × 900) e mobile (390 × 844), incluindo axe WCAG 2.2 A/AA sem violações,
  larguras 320–1920 px sem rolagem horizontal, fluxo só pelo teclado, 401 → login com aviso,
  sessão por aba, token fora da URL e do console, saldo em ≤ 2 s após o login (SC-005) e
  telemetria sem dados sensíveis com `traceparent` na chamada à API (SC-008).
- **Compose**: serviço `frontend` saudável; `/saude` 200; fallback de SPA; CSP,
  `X-Content-Type-Options` e `Referrer-Policy` presentes; `/api` via proxy responde 401 sem
  token.
- **Pendente (T056)**: conferência visual do trace `bank-frontend` → `bank-api` no Aspire
  Dashboard; validação manual em Firefox e Safari/WebKit; cronometragem a partir de um clone
  limpo (SC-007); cadastro + login em < 2 min com pessoa real (SC-001).
- **Fase 8** (T049–T052, 2026-11-17, após o merge da issue #18 / PR #23): tipos regenerados de
  `/openapi/v1.json`; perfil com documento mascarado e "Cliente desde" e início PJ com
  "Copiar CNPJ" lendo `GET /api/v1/usuarios/me`. **88 testes** de componentes e **35 ponta a
  ponta** aprovados no Compose (inclui a área de transferência real no Chromium e axe sem
  violações). SC-005 da spec 005 verificado.

### Ajustes feitos na implementação

- Healthcheck do `frontend` em `http://127.0.0.1/saude` (T025): `localhost` resolve para IPv6
  na imagem Alpine.
- Mensagem de documento duplicado genérica ("Este documento já tem conta…"), sem depender do
  tipo de conta (data-model atualizado).
- Motivo de encerramento `?conta=1` (conta não encontrada) além de `?expirada=1` (contrato
  atualizado).
- Ver também "Descobertas da implementação" em [research.md](research.md).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → histórias
- **US1 (3)** e **US2 (4)**: dependem só da Foundational (a sessão e o cliente já incluem o
  login usado no cadastro); o e2e de US1 usa a tela de login apenas indiretamente
- **US3 (5)**: depende da Foundational; os e2e criam contas pela API ou pela tela de cadastro
- **US4 (6)**: depende da US3 (reaproveita o carregamento da carteira para o nome)
- **US5 (7)**: depois de US1–US4 (verifica todas as telas)
- **Fase 8**: bloqueada pela issue #18; pode ser entregue em PR posterior
- **Polish (9)**: depois das fases desejadas

### Within Each Phase

- Testes antes da implementação, falhando primeiro
- `formatacao` → `api` → `sessao` → `App`/rotas → telas
- T018 (tipos gerados) antes de T019 (cliente)

### Parallel Opportunities

- Setup: T002–T006 em paralelo depois de T001
- Foundational: T008–T014 em paralelo; T015–T017, T024, T026, T027 em paralelo
- US1 e US2 podem andar em paralelo após a Foundational (telas diferentes)
- US5: T046 e T047 em paralelo
- Polish: T053–T055 em paralelo

---

## Parallel Example: Foundational

```text
T009 mascaras.test.ts   T010 moeda/data.test.ts   T011 erros.test.ts   T012 sessao.test.tsx
T015 tokens.css         T016 componentes base     T024 Dockerfile/nginx T026 CI
```

## Parallel Example: User Story 1 + User Story 2

```text
T029 CriarConta.test.tsx   T030 e2e/cadastro.spec.ts
T034 Entrar.test.tsx       T035 e2e/sessao.spec.ts
```

---

## Implementation Strategy

### MVP First

1. Phases 1 e 2
2. US1 (cadastro) + US2 (login) → primeira entrega navegável
3. **PARAR e VALIDAR** em desktop e mobile

### Incremental Delivery

1. US3 → carteira na tela inicial
2. US4 (parte independente) → perfil
3. US5 → responsividade e acessibilidade verificadas
4. Polish → telemetria, documentação, quickstart
5. Fase 8 → após a issue #18 (pode ir em PR separado)

### Commits

Conventional Commits por fase, escopo `frontend-base` (por exemplo
`feat(frontend-base): ...`, `test(frontend-base): ...`); PR com `Closes #17` apenas quando a
Fase 8 estiver concluída — antes disso, `Refs #17`.
