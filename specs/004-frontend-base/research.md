# Research: Frontend base — acesso, cadastro e carteira

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-17

Versões consultadas no registro do npm, no Docker Hub e no índice de releases do Node em
2026-11-17. Regra herdada das specs 001–003: versão exata fixada, publicada há pelo menos 2
semanas (até 2026-11-03), evitando majors recém-lançadas.

## R1. Runtime e ferramentas

- **Decision**: Node.js **26.13.0** (LTS "Lithium", publicada em 2026-10-28), fixada em
  `src/frontend/.nvmrc`, em `engines` do `package.json` e na imagem `node:26.13.0-alpine` do
  build. npm (o que acompanha o Node) com `package-lock.json` versionado e `npm ci` no CI e no
  Dockerfile.
- **Rationale**: LTS ativa; um único gerenciador de pacotes, sem ferramenta extra.
- **Observação**: o Node não está instalado na máquina de desenvolvimento atual; é
  pré-requisito do quickstart (ou o frontend pode ser executado só pelo Compose).
- **Alternatives considered**: Node 24 (LTS em manutenção); pnpm/yarn (ferramenta adicional
  sem ganho para um único pacote).

## R2. Stack da aplicação

| Pacote | Versão | Uso / justificativa |
| --- | --- | --- |
| `react`, `react-dom` | 19.3.1 | Fixado pela constituição (React). |
| `@types/react`, `@types/react-dom` | 19.3.0 | Tipos do React. |
| `typescript` | 5.9.3 | Fixado pela constituição (TypeScript). A 7.x não é aceita por `typescript-eslint` (`<6.1`) nem por `openapi-typescript` (`^5`). |
| `vite` | 8.3.5 | Servidor de desenvolvimento com proxy e build de produção. A 8.3.8 saiu em 2026-11-11. |
| `@vitejs/plugin-react` | 6.2.1 | Integração do React com o Vite. |
| `react-router` | 8.5.1 | Rotas, rotas protegidas e retorno à tela pedida após o login (FR-010). |
| `openapi-typescript` | 7.13.0 | Gera os tipos TypeScript a partir do OpenAPI da API (contratos explícitos — Princípio V). |
| `openapi-fetch` | 0.17.0 | Cliente HTTP tipado pelos tipos gerados, com middlewares (token, 401, erros). |
| `@fontsource-variable/inter`, `@fontsource/jetbrains-mono` | 5.3.0 | Fontes do protótipo servidas pela própria aplicação (sem CDN de terceiros — privacidade). |

**Não adotados**: biblioteca de estado global (Redux/Zustand) — o único estado global é a
sessão, resolvido com contexto do React; biblioteca de dados (TanStack Query) — três chamadas
simples, sem cache compartilhado; biblioteca de máscaras — CPF e CNPJ alfanumérico têm regras
próprias e curtas, cobertas por testes; framework de UI/CSS (Tailwind, MUI) — os tokens do
protótipo viram variáveis CSS e componentes próprios, sem dependência de tema de terceiros.

## R3. Testes e qualidade

| Pacote | Versão | Uso |
| --- | --- | --- |
| `vitest` | 5.1.0 | Testes unitários e de componentes (FR-022). |
| `jsdom` | 30.1.3 | DOM dos testes de componentes. A 31.x saiu em 2026-11-13. |
| `@testing-library/react` | 16.3.3 | Testes de componentes pelo comportamento visível. |
| `@testing-library/user-event` | 14.6.8 | Interação realista (digitação, teclado). |
| `@testing-library/jest-dom` | 7.0.1 | Asserções de DOM (acessibilidade, visibilidade). |
| `msw` | 3.0.3 | Simula respostas da API (incluindo `ProblemDetails`) nos testes de componentes. |
| `@playwright/test` | 1.64.1 | Testes ponta a ponta contra a API real (Compose), em viewports desktop e mobile. |
| `@axe-core/playwright` | 4.14.0 | Verificação automática WCAG 2.2 A/AA em cada tela e viewport (FR-001a, SC-006). |
| `eslint`, `@eslint/js` | 9.39.5 | Análise estática. A 10.x não é aceita por `eslint-plugin-jsx-a11y`. |
| `typescript-eslint` | 8.72.1 | Regras para TypeScript. |
| `eslint-plugin-react-hooks` | 7.1.1 | Regras de hooks do React. |
| `eslint-plugin-jsx-a11y` | 6.10.2 | Acessibilidade estática no JSX (FR-001a). |
| `globals` | 17.14.0 | Globais do navegador para o ESLint. |

- **Decision**: duas camadas de teste, espelhando o backend:
  1. **Unitários/componentes** (Vitest + Testing Library + MSW): máscaras, formatação,
     tradução de erros, sessão (expiração, 401), rotas protegidas e cada tela com respostas
     simuladas da API.
  2. **Ponta a ponta** (Playwright + axe): fluxos de cadastro, login, início PF/PJ, perfil e
     saída contra a **API real** do Compose (constituição: dependências reais, sem simulação),
     em viewports 390 × 844 e 1440 × 900, mais a verificação sem rolagem horizontal em 320,
     768 e 1920 px (SC-002).
- **Navegador**: Chromium do Playwright com emulação de dispositivo para mobile. WebKit e
  Firefox ficam como verificação manual (spec, Assumptions).
- **TypeScript estrito** (`strict`, `noUncheckedIndexedAccess`) e `tsc --noEmit` no CI.

## R4. Acesso à API (proxy × CORS)

- **Decision**: **mesma origem por proxy**, sem CORS na API.
  - Desenvolvimento: o servidor do Vite encaminha `/api` para a API (`http://localhost:5080`
    por padrão, configurável por `BANK_API_URL`) e `/otlp` para o coletor
    (`http://localhost:4318`).
  - Compose: o frontend é servido por **nginx**, que entrega os arquivos estáticos e
    encaminha `/api/` → `api:8080` e `/otlp/` → `aspire-dashboard:18890` (OTLP/HTTP).
- **Rationale**: nenhuma mudança de segurança na API (CORS abriria a API a outras origens);
  o token nunca cruza origens; o `traceparent` é propagado por padrão em requisições de mesma
  origem; o coletor não precisa de CORS.
- **Alternatives considered**: CORS na API e no coletor (duas configurações de segurança a
  manter e testar); servir o frontend pela própria API (acopla deploys).

## R5. Sessão (FR-009 a FR-013)

- **Decision**: `sessionStorage` (chave `bank.sessao`) com `{ token, expiraEm }`.
  - `expiraEm` = momento do login + `expiraEmSegundos` da resposta; ao carregar a aplicação,
    sessão vencida é descartada.
  - As claims (`sub`, `email`, `tipo_conta`, `carteira_id`) são lidas decodificando o payload
    do JWT **apenas para exibição e roteamento** (PF × PJ); a autorização continua na API.
  - Um temporizador encerra a sessão no instante de `expiraEm` (US2, cenário 4).
  - Middleware do cliente: injeta `Authorization: Bearer`; resposta 401 em rota protegida →
    limpa a sessão e navega para `/entrar` com o aviso de sessão expirada e a rota de retorno.
  - "Sair" remove a chave e volta para `/entrar`.
- **Segurança**: o token nunca vai para a URL, para logs nem para a telemetria (FR-012,
  FR-019c); Content Security Policy no nginx restringindo scripts à própria origem reduz o
  risco de XSS ler o `sessionStorage`.
- **Alternatives considered**: memória pura (recarregar desloga — rejeitado no clarify);
  `localStorage` (compartilhado e persistente — rejeitado no clarify); cookie `HttpOnly`
  (exige mudança na API — spec futura).

## R6. Tradução de erros (FR-018, FR-019)

- **Decision**: módulo único `traduzirErro(resposta)` que recebe o `ProblemDetails` (ou a
  falha de rede) e devolve `{ mensagem, campos?, traceId?, acao? }`, guiado pela tabela de
  [data-model.md](data-model.md#tradução-de-erros). Campos de `errors` são mapeados para os
  campos do formulário; os desconhecidos viram mensagem geral. O `detail` da API nunca é
  exibido como mensagem principal.
- Testado com um caso por `type` do contrato da spec 003, mais rede indisponível, 500 e corpo
  não-JSON (SC-003).

## R7. Telemetria do navegador (FR-019a a FR-019d)

- **Decision**: OpenTelemetry JS na **linha 2.x estável**, coerente entre os pacotes:

  | Pacote | Versão |
  | --- | --- |
  | `@opentelemetry/api` | 1.9.1 (o `core` 2.12 exige `<1.10`) |
  | `@opentelemetry/sdk-trace-web`, `@opentelemetry/sdk-trace-base`, `@opentelemetry/resources`, `@opentelemetry/sdk-metrics`, `@opentelemetry/context-zone`, `@opentelemetry/core` | 2.12.0 |
  | `@opentelemetry/instrumentation`, `@opentelemetry/instrumentation-fetch`, `@opentelemetry/exporter-trace-otlp-http`, `@opentelemetry/exporter-metrics-otlp-http`, `@opentelemetry/exporter-logs-otlp-http`, `@opentelemetry/sdk-logs`, `@opentelemetry/api-logs` | 0.223.0 |
  | `@opentelemetry/instrumentation-document-load` | 0.68.0 |
  | `@opentelemetry/semantic-conventions` | 1.44.0 |

  A linha 3.0 (2026-10-20) é major recente e o `sdk-trace-web` ainda não a acompanha —
  evitada, como o Scalar 3.0 na spec 002.
- **Sinais**: traces de carregamento de documento, de cada chamada `fetch` (com
  `traceparent` para a API, mesma origem) e de navegação entre rotas (span por troca de
  rota); logs de erros não tratados (`window.onerror`, `unhandledrejection`) e de falhas de
  chamada (apenas `type` e status, nunca corpo); métrica de tempo de carregamento por rota.
  `service.name` = `bank-frontend`.
- **Dados sensíveis**: spans de `fetch` sem corpo nem cabeçalhos; URLs só com caminho (as
  rotas não carregam dados pessoais); logs só com `type`, status e `traceId`; teste
  automatizado inspeciona o que seria exportado (SC-008).
- **Configuração**: `VITE_TELEMETRIA_HABILITADA` (padrão `false`; `true` no
  desenvolvimento e no Compose) e `VITE_OTLP_URL` (padrão `/otlp`). Inicialização carregada
  sob demanda: com a telemetria desligada, o SDK não entra no bundle inicial. Falhas de envio
  são ignoradas (FR-019d).

## R8. Design system e responsividade

- **Decision**: tokens do protótipo como variáveis CSS em `src/design/tokens.css` (cores,
  tipografia, raios, espaçamentos de 4 px); CSS Modules por componente (nativo do Vite, sem
  dependência). Componentes base: `Botao`, `CampoTexto`, `CampoDocumento`, `CampoSenha`,
  `Aviso`, `Carregando`, `EstadoVazio`, `Cartao`, `Layout` (navegação lateral no desktop,
  barra inferior no mobile).
- **Breakpoint**: `≥ 1024 px` desktop; abaixo, mobile; layouts fluidos de 320 a 1920 px.
- **Formatação**: `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })` com
  `font-variant-numeric: tabular-nums`; datas com `Intl.DateTimeFormat('pt-BR')` no fuso do
  navegador.

## R9. Ambiente (FR-020) e CI (FR-021)

- **Dockerfile** (`src/frontend/Dockerfile`): `node:26.13.0-alpine` (`npm ci` + `npm run
  build`, com `VITE_*` como build args) → `nginx:1.30.6-alpine` (linha estável do nginx)
  servindo `dist/` com a configuração de R4, fallback de SPA para `index.html`, cabeçalhos de
  segurança (CSP, `X-Content-Type-Options`, `Referrer-Policy`) e healthcheck `wget` em
  `/saude` (arquivo estático).
- **Compose**: serviço `frontend` em `127.0.0.1:${FRONTEND_PORT:-3000}`, dependendo da `api`
  saudável; `.env.example` ganha `FRONTEND_PORT`.
- **CI** (`.github/workflows/ci.yml`, sem filtro de paths, como o `build-test`):
  - job `frontend`: `npm ci`, `npm run lint`, `npm run typecheck`, `npm test`, `npm run build`;
  - job `frontend-e2e`: `docker compose up -d --build --wait`, `npx playwright install
    --with-deps chromium`, `npm run e2e` contra `http://localhost:3000`, logs do Compose em
    falha.
  - O `infra-local` passa a incluir `src/frontend/**` no filtro de paths.
- **Tipos da API**: `npm run api:tipos` gera `src/api/schema.d.ts` a partir de
  `/openapi/v1.json` da API em execução; o arquivo gerado é versionado (o build não depende
  da API no ar). Divergência entre tipos e contrato aparece nos testes ponta a ponta.

## R10. Dependência da issue #18 (`GET /api/v1/usuarios/me`)

- **Decision**: o cliente consome o endpoint pelo contrato proposto na issue #18. As tarefas
  que dependem dele (documento e "Cliente desde" no perfil, CNPJ com "Copiar" no início PJ)
  ficam numa fase própria, **bloqueada até o merge da #18**; o restante da spec é entregável
  sem ela. Enquanto isso, perfil e início PJ exibem apenas os dados já disponíveis — nenhum
  dado fictício.

## Descobertas da implementação

- **`openapi-fetch` e o `fetch` global**: o cliente captura `globalThis.fetch` ao ser criado, o
  que ignoraria a instrumentação do OpenTelemetry (carregada depois) e o MSW nos testes. O
  cliente passa `fetch: (req) => globalThis.fetch(req)`, resolvendo o `fetch` a cada chamada.
- **Token lido do armazenamento**: as telas chamam a API em efeitos que rodam antes dos
  efeitos do provedor de sessão (filhos antes do pai); por isso o middleware lê o token
  direto de `sessionStorage`, e o provedor só registra a reação a 401.
- **Redirecionamento centralizado**: encerrar a sessão e navegar no mesmo instante concorria
  com o `<Navigate>` da rota protegida. `RotaProtegida` passou a ser a única a redirecionar
  para o login (com `?expirada=1`/`?conta=1` e a rota de retorno), e `RotaPublica` devolve
  para `state.retorno` após o login.
- **MSW 3**: a opção `onUnhandledRequest` virou `onUnhandledFrame`.
- **`playwright-core` duplicado**: `@axe-core/playwright` resolvia `playwright-core` 1.65.1;
  `overrides` no `package.json` fixa 1.64.1, a mesma do `@playwright/test`.
- **Healthcheck do nginx**: na imagem Alpine `localhost` resolve para IPv6 e o nginx escuta
  em IPv4; o healthcheck usa `http://127.0.0.1/saude`.
- **Métrica de carregamento**: a telemetria é carregada sob demanda e costuma iniciar depois do
  evento `load`; registrar só no `load` perdia a medição (nenhuma métrica do `bank-frontend` no
  dashboard, notado na validação manual). Agora mede na hora quando a página já carregou, conta
  as navegações (`bank.frontend.navegacoes`) e força o envio ao esconder a aba. O teste ponta a
  ponta passou a exigir que traces e métricas sejam aceitos pelo coletor (200).
- **Avisos de manutenção**: o npm marca `@opentelemetry/sdk-trace-web`/`sdk-trace-base` 2.x
  como substituídos por `@opentelemetry/sdk-trace` (linha 3.x) e o ESLint 9 como fora de
  suporte. Ambos continuam funcionais; a migração para OTel 3.x e ESLint 10 fica para quando
  `sdk-trace-web` e `eslint-plugin-jsx-a11y` acompanharem (Complexity Tracking do plano).
