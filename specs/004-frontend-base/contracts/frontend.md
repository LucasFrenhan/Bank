# Contrato do Frontend — v1.0.0

**Feature**: [../spec.md](../spec.md) | **API consumida**: [specs/003 — usuarios-carteiras-api.md](../../003-usuarios-carteiras/contracts/usuarios-carteiras-api.md) + `GET /api/v1/usuarios/me` (issue #18)

## Rotas

| Rota | Acesso | Tela | Observações |
| --- | --- | --- | --- |
| `/entrar` | Pública (com sessão → `/` ou `state.retorno`) | Login | `?expirada=1` mostra o aviso de sessão expirada; `?conta=1`, o de conta não encontrada; `state.retorno` define o destino após entrar |
| `/criar-conta` | Pública (com sessão → `/`) | Cadastro PF/PJ | Após sucesso, login automático → `/` |
| `/` | Protegida | Início PF ou PJ (pela claim `tipo_conta`) | |
| `/perfil` | Protegida | Perfil | |
| `*` | Pública | Página não encontrada | Link para `/` |

Sem sessão, rotas protegidas redirecionam para `/entrar` guardando a rota pedida. Não existem
rotas de transferência, extrato ou cobranças nesta versão; os itens de menu correspondentes são
"Em breve" (não navegáveis).

## Navegação por perfil

| Perfil | Itens (desktop: menu lateral; mobile: barra inferior) |
| --- | --- |
| PF | Início · Transferir (Em breve) · Extrato (Em breve) · Perfil |
| PJ | Início · Recebimentos (Em breve) · Cobranças (Em breve) · Perfil |

## Endpoints consumidos

| Chamada | Tela | Autenticação | Respostas tratadas |
| --- | --- | --- | --- |
| `POST /api/v1/autenticacao/registrar` | Cadastro | — | 201, 400, 409, rede, 5xx |
| `POST /api/v1/autenticacao/login` | Login; cadastro (login automático) | — | 200, 400, 401, rede, 5xx |
| `GET /api/v1/carteiras/minha` | Início | Bearer | 200, 401, 403, 404, rede, 5xx |
| `GET /api/v1/usuarios/me` (issue #18) | Perfil; início PJ (CNPJ) | Bearer | 200, 401, 403, 404, rede, 5xx |

Tipos de requisição e resposta gerados de `/openapi/v1.json` (`src/frontend/src/api/schema.d.ts`).
Mensagens de erro: [data-model — tradução de erros](../data-model.md#tradução-de-erros).

## Ambiente

| Modo | Endereço | `/api` encaminha para | `/otlp` encaminha para |
| --- | --- | --- | --- |
| Desenvolvimento (`npm run dev`) | `http://localhost:5173` | `BANK_API_URL` (padrão `http://localhost:5080`) | `BANK_OTLP_URL` (padrão `http://localhost:4318`) |
| Compose (`frontend`) | `http://localhost:${FRONTEND_PORT:-3000}` | `http://api:8080` | `http://aspire-dashboard:18890` |

- Healthcheck do serviço: `GET /saude` → 200.
- Cabeçalhos do nginx: `Content-Security-Policy` (`default-src 'self'`; sem scripts de
  terceiros), `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`.

## Telemetria

- `service.name`: `bank-frontend`; exportação OTLP/HTTP para `VITE_OTLP_URL` (`/otlp/v1/traces`,
  `/otlp/v1/metrics`, `/otlp/v1/logs`).
- Traces: carregamento do documento, navegação entre rotas, chamadas `fetch` com `traceparent`
  propagado para a API (mesma origem).
- Logs: erros não tratados e falhas de chamada (somente `type`, status e `traceId`).
- Métricas: `bank.frontend.carregamento` (histograma, ms, por rota de entrada) e
  `bank.frontend.navegacoes` (contador, por rota), exportadas a cada 5 s e ao esconder a aba.
- Proibido em qualquer sinal: valores de formulário, senha, token, documento, e-mail.

## Scripts (`src/frontend/package.json`)

| Script | Função |
| --- | --- |
| `dev` | Servidor de desenvolvimento com proxy |
| `build` | Verificação de tipos + build de produção em `dist/` |
| `lint` / `typecheck` | ESLint / `tsc --noEmit` |
| `test` | Vitest (unitários e componentes) |
| `e2e` | Playwright + axe contra `E2E_BASE_URL` (padrão `http://localhost:3000`) |
| `api:tipos` | Gera `src/api/schema.d.ts` a partir de `${BANK_API_URL}/openapi/v1.json` |

## Histórico

- **1.0.0** (2026-11-17): versão inicial (issue #17).
