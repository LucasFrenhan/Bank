# Implementation Plan: Frontend base — acesso, cadastro e carteira

**Branch**: `004-frontend-base` | **Date**: 2026-11-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-frontend-base/spec.md` (issue #17; depende
parcialmente da issue #18)

## Summary

Criar o frontend em `src/frontend/` com React 19 + TypeScript 5.9 sobre Vite 8, seguindo o
protótipo (`docs/design/prototipo.md`): tokens do design system como variáveis CSS,
componentes próprios, layouts desktop (≥ 1024 px, menu lateral) e mobile (barra inferior). O
cliente HTTP é tipado a partir do OpenAPI da API (`openapi-typescript` + `openapi-fetch`),
com middlewares de token, sessão expirada (401) e tradução de `ProblemDetails` nas mensagens
do protótipo. A sessão fica em `sessionStorage` (por aba, expira em 3600 s). Telas: login,
cadastro PF/PJ (máscaras de CPF e CNPJ alfanumérico), início PF, início PJ, perfil e "não
encontrada". A API e o coletor são acessados pela mesma origem (proxy do Vite no
desenvolvimento; nginx no Compose), sem CORS. Telemetria OpenTelemetry do navegador
(`bank-frontend`) com `traceparent` até a API. Qualidade: Vitest + Testing Library + MSW,
Playwright + axe contra a API real do Compose em desktop e mobile, e jobs de CI. Os dados do
titular (documento, "Cliente desde", "Copiar CNPJ") entram numa fase bloqueada pela issue #18.

## Technical Context

**Language/Version**: TypeScript 5.9.3 sobre Node.js 26.13.0 (LTS); React 19.3.1

**Primary Dependencies**: Vite 8.3.5, `react-router` 8.5.1, `openapi-fetch` 0.17.0 /
`openapi-typescript` 7.13.0, OpenTelemetry JS 2.12.0 / 0.223.0, fontes Inter e JetBrains Mono
self-hosted — justificativas em [research.md R2 e R7](research.md)

**Storage**: `sessionStorage` (somente a sessão); nenhum dado de negócio persistido no navegador

**Testing**: Vitest 5.1.0 + jsdom + Testing Library + MSW 3.0.3 (unitários/componentes);
Playwright 1.64.1 + `@axe-core/playwright` 4.14.0 (ponta a ponta contra o Compose);
ESLint 9.39.5 + `typescript-eslint` + `jsx-a11y`; `tsc --noEmit`

**Target Platform**: navegadores atuais (Chrome, Edge, Firefox, Safari) desktop e mobile;
servido por nginx 1.30.6 (Alpine) no Compose

**Project Type**: web application (frontend SPA consumindo a API existente)

**Performance Goals**: saldo visível em ≤ 2 s após o login (SC-005); cadastro até a tela
inicial em < 2 min (SC-001)

**Constraints**: WCAG 2.2 AA; sem rolagem horizontal de 320 a 1920 px; nenhum dado sensível
em telemetria, URL ou log; telemetria desligada por padrão em produção; nenhum CDN de
terceiros; sem mudanças na API desta spec (exceto as da issue #18, em spec própria)

**Scale/Scope**: 6 telas (×2 layouts), 4 endpoints consumidos, 1 serviço novo no Compose, 2
jobs novos de CI

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 4 clarificações, issue #17, branch `004-frontend-base` = pasta; dependência #18 registrada | ✅ |
| II. Isolamento de camadas | Backend inalterado. No frontend, telas não chamam `fetch` diretamente: só o módulo `api/` fala com a API (regra de lint `no-restricted-imports`/`no-restricted-globals` nas pastas de telas) | ✅ |
| III. Erros padronizados | O frontend consome `ProblemDetails` pelo `type` (nunca pelo `detail`), com tabela única de tradução e `traceId` exibido em erros inesperados | ✅ |
| IV. Integridade financeira | Só exibe saldo vindo da API, formatado de `decimal` sem aritmética no cliente | N/A |
| V. Contratos explícitos | Tipos gerados do OpenAPI; [contracts/frontend.md](contracts/frontend.md) v1.0.0 (rotas, endpoints, ambiente, telemetria) | ✅ |
| VI. Observabilidade | OpenTelemetry no navegador (traces, logs, métricas) via OTLP, `traceparent` até a API, sem dados sensíveis (R7) | ✅ |
| VII. Qualidade verificável | Testes unitários das regras de apresentação; ponta a ponta contra a API real em contêiner (Compose), caminho feliz e erros; CI em todo PR. 401/403 de endpoint protegido continuam cobertos no backend (spec 003); o frontend testa a reação a eles | ✅ |
| Restrições: React + TypeScript em `src/frontend/` | Sim | ✅ |
| Restrições: toda tela com versão desktop e mobile especificada e validada | Spec US5/FR-002; Playwright em 390 × 844 e 1440 × 900 + larguras 320/768/1920 | ✅ |
| Restrições: Docker Compose com healthchecks | Serviço `frontend` com healthcheck `/saude`, dependendo da `api` saudável | ✅ |
| Restrições: dependência justificada | Tabelas R2, R3 e R7, com alternativas rejeitadas | ✅ |
| Restrições: segredos não versionados | Nenhum segredo no frontend; o token só existe em `sessionStorage` | ✅ |
| Convenções: linguagem ubíqua em português | Pastas e componentes em português (`paginas/`, `sessao/`, `CampoDocumento`) | ✅ |

**Resultado pré-pesquisa**: aprovado.
**Re-check pós-design (Phase 1)**: aprovado; versões abaixo do "latest" e proxy do coletor
registrados em Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/004-frontend-base/
├── plan.md                 # Este arquivo
├── research.md             # Phase 0: decisões R1–R10
├── data-model.md           # Phase 1: sessão, visões, formulários, máscaras, tradução de erros
├── quickstart.md           # Phase 1: roteiro de validação
├── contracts/
│   └── frontend.md         # Phase 1: rotas, endpoints consumidos, ambiente, telemetria
├── checklists/
│   └── requirements.md
└── tasks.md                # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
/
├── docker-compose.yml                    # + serviço frontend (R9)
├── .env.example                          # + FRONTEND_PORT
├── .github/workflows/
│   ├── ci.yml                            # + jobs frontend e frontend-e2e
│   └── infra-local.yml                   # paths + src/frontend/**
├── docs/desenvolvimento.md               # + seção Frontend
└── src/frontend/
    ├── .nvmrc                            # 26.13.0
    ├── package.json / package-lock.json
    ├── tsconfig.json, tsconfig.app.json, tsconfig.node.json
    ├── vite.config.ts                    # proxy /api e /otlp; config do Vitest
    ├── eslint.config.js
    ├── playwright.config.ts              # projetos desktop (1440×900) e mobile (390×844)
    ├── index.html
    ├── Dockerfile, .dockerignore
    ├── nginx/default.conf                # estáticos, SPA fallback, /api, /otlp, /saude, cabeçalhos
    ├── public/saude                      # healthcheck
    ├── src/
    │   ├── main.tsx                      # bootstrap: telemetria (opcional), fontes, rotas
    │   ├── App.tsx                       # rotas e RotaProtegida
    │   ├── api/
    │   │   ├── schema.d.ts               # gerado (npm run api:tipos)
    │   │   ├── cliente.ts                # openapi-fetch + middlewares (token, 401)
    │   │   └── erros.ts                  # ProblemDetails → MensagemErro (data-model)
    │   ├── sessao/                       # armazenamento, claims, contexto, expiração, RotaProtegida
    │   ├── telemetria/                   # SDK OTel do navegador (carregado sob demanda)
    │   ├── design/
    │   │   ├── tokens.css, global.css
    │   │   └── componentes/              # Botao, CampoTexto, CampoDocumento, CampoSenha, Aviso,
    │   │                                 # Carregando, EstadoVazio, Cartao, Layout, Navegacao
    │   ├── formatacao/                   # mascaras (CPF/CNPJ), moeda, data, documento mascarado
    │   └── paginas/                      # Entrar, CriarConta, Inicio (PF/PJ), Perfil, NaoEncontrada
    │       └── **/*.test.tsx             # testes de componentes ao lado das telas
    └── e2e/                              # Playwright: cadastro, login, inicio, perfil, sessao,
                                          # acessibilidade, responsividade
```

**Structure Decision**: aplicação única em `src/frontend/` (constituição). O isolamento
equivalente ao Princípio II é feito por pastas: `paginas/` e `design/` não acessam a rede;
apenas `api/` conversa com a API, e `sessao/` é a única dona do token. Testes unitários ficam
ao lado do código; ponta a ponta em `e2e/`.

## Fases de entrega

1. **Base**: projeto, design system, cliente, sessão, rotas, telemetria, Compose e CI.
2. **US1–US3 + US5**: cadastro, login/saída, início PF/PJ, responsividade e acessibilidade.
3. **US4 parcial**: perfil com nome, e-mail, tipo, permissões e "Sair".
4. **Bloqueada pela #18**: documento mascarado e "Cliente desde" no perfil; CNPJ com "Copiar"
   no início PJ (tipos regenerados com `npm run api:tipos`).

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| Versões abaixo do "latest" (TypeScript 5.9, ESLint 9, OpenTelemetry 2.x) | `typescript-eslint` e `openapi-typescript` não aceitam TS 7; `jsx-a11y` não aceita ESLint 10; OTel 3.0 é major de 4 semanas e o `sdk-trace-web` não a acompanha | Usar o "latest" quebraria a instalação (peer dependencies) ou misturaria linhas incompatíveis do OTel |
| nginx encaminhando `/otlp` para o coletor | Telemetria do navegador sem CORS no coletor e na mesma origem (R4) | Liberar CORS no Aspire Dashboard exigiria configuração de segurança extra e outra origem a manter |
| Fase final bloqueada pela issue #18 | O perfil e o início PJ do protótipo precisam de documento e data de cadastro | Implementar o endpoint aqui misturaria backend e frontend na mesma spec (decisão do usuário: task separada) |
| Testes ponta a ponta subindo o Compose no CI | Constituição: dependências reais; validar desktop/mobile e acessibilidade contra a API real | Simular a API nos e2e não provaria a integração nem os tipos gerados |
