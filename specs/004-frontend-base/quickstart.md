# Quickstart: Frontend base

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/frontend.md](contracts/frontend.md) | **Modelo**: [data-model.md](data-model.md)

## Pré-requisitos

- Ambiente das specs 001–003 (Docker em execução; SDK do .NET 10 para rodar a API pelo SDK).
- Node.js 26.13.0 (`src/frontend/.nvmrc`) — necessário só para desenvolver e rodar testes fora
  do Compose.

## Cenário 1 — Qualidade (FR-021, FR-022)

```bash
cd src/frontend
npm ci
npm run lint && npm run typecheck && npm test && npm run build
```

**Esperado**: sem erros de lint e de tipos; testes unitários e de componentes aprovados;
`dist/` gerado.

## Cenário 2 — Frontend pelo Compose (FR-020, SC-007)

```bash
docker compose up -d --build --wait
```

**Esperado**: serviço `frontend` saudável; <http://localhost:3000> abre a tela de login com o
aviso "Ambiente de demonstração".

## Cenário 3 — Desenvolvimento com API pelo SDK

```bash
docker compose up -d --wait postgres aspire-dashboard
dotnet run --project src/backend/src/Bank.Api   # chave JWT via user-secrets (docs/desenvolvimento.md)
cd src/frontend && npm run dev
```

**Esperado**: <http://localhost:5173> funciona com recarga automática; chamadas a `/api` chegam
à API na porta 5080.

## Cenário 4 — Fluxos principais (US1–US4) em desktop e mobile

No navegador (e com as ferramentas de dispositivo em 390 × 844):

1. Criar conta PF → entra automaticamente; início mostra **R$ 10,00**, "Transferir" e "Ver
   extrato" como "Em breve".
2. Sair; criar conta PJ com CNPJ `12.abc.345/01de-35` digitado em minúsculas → o campo mostra
   `12.ABC.345/01DE-35`; início mostra **R$ 0,00** e "Contas Pessoa Jurídica não enviam
   transferências", sem ação de transferir. (Se o CNPJ já existir na base local, usar outro
   válido ou `docker compose down -v`.)
3. Repetir o cadastro com o mesmo e-mail → "Este e-mail já tem conta" com link para o login.
4. Login com senha errada → "E-mail ou senha incorretos".
5. Abrir `/perfil` sem sessão → vai ao login e volta ao perfil após entrar.
6. Abrir uma segunda aba em `/` → exige login (sessão por aba).

## Cenário 5 — Testes ponta a ponta e acessibilidade (SC-002, SC-004, SC-006)

```bash
docker compose up -d --build --wait
cd src/frontend && npx playwright install chromium && npm run e2e
```

**Esperado**: fluxos aprovados em desktop e mobile; 0 violações WCAG 2.2 A/AA; nenhuma tela
com rolagem horizontal em 320, 390, 768, 1440 e 1920 px.

## Cenário 6 — Telemetria (SC-008)

Com o Compose no ar, fazer login em <http://localhost:3000> e abrir o Aspire Dashboard
(<http://localhost:18888>).

**Esperado**:

- **Traces**: o trace `bank-frontend: POST` do login tem 2 spans — `HTTP POST` (navegador) e
  `POST /api/v1/autenticacao/login` (`bank-api`).
- **Metrics**: a tela abre com "Resource (None)"; escolha o recurso. Em `bank-api`, o meter
  `Bank.Usuarios` tem `bank.usuarios.cadastros` (por `tipo_conta`) e `bank.autenticacao.logins`
  (por `resultado`); em `bank-frontend`, `bank.frontend.carregamento` e
  `bank.frontend.navegacoes` (por `url.path`). As métricas são exportadas a cada 5 s.
- Nenhum span ou log contém e-mail, senha, token ou documento.

## Cenário 7 — Perfil completo (após a issue #18)

Com a #18 integrada: o perfil mostra documento mascarado e "Cliente desde"; o início PJ mostra
o CNPJ e "Copiar" copia o valor sem máscara.
