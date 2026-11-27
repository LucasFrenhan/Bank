# Quickstart: Sessão renovável com refresh token rotativo e logout no servidor

**Feature**: [spec.md](spec.md) | **Contratos**: [API 1.1.0](contracts/autenticacao-sessao-api.md),
[frontend](contracts/frontend-sessao.md) | **Modelo**: [data-model.md](data-model.md)

## Pré-requisitos

- Docker, SDK do .NET 10 e Node da spec 004.
- `docker compose up -d --build --wait` na raiz. A API aplica a migration `AdicionarSessoes`.
- Para ver renovação e avisos sem esperar, suba com prazos curtos no `.env`:
  `JWT_EXPIRACAO_SEGUNDOS=10`, `SESSAO_INATIVIDADE_SEGUNDOS=25`, `SESSAO_TOLERANCIA_REUSO_SEGUNDOS=5`, `SESSAO_LIMITE_ABSOLUTO_HORAS=1` (os mesmos valores do e2e, em `.env.e2e.example`).
- App em `http://localhost:3000`; `psql`: `docker compose exec postgres psql -U bank -d bank`.

## Cenário 1 — Testes automatizados

```bash
dotnet test --solution src/backend/Bank.slnx
cd src/frontend && npm run lint && npm run typecheck && npm test && npm run build && npm run e2e
```

**Esperado**: tudo verde, incluindo os testes anteriores e o `sessao.spec.ts` revisto, em `desktop` e
`mobile`.

## Cenário 2 — Cookie e armazenamento (FR-003, FR-005, SC-006)

1. Entre como PF. DevTools → Network → `login`: `Set-Cookie` com `bank_renovacao`, `HttpOnly`, `Secure`,
   `SameSite=Strict`, `Path=/api/v1/autenticacao`.
2. DevTools → Application: nenhum token em `sessionStorage` ou `localStorage`; o cookie aparece com
   `HttpOnly` marcado. No console, `document.cookie` não mostra `bank_renovacao`.
3. Network → uma chamada a `/api/v1/carteiras/minha`: sem o cookie na requisição (só `Authorization`).
4. `SELECT encode(hash,'hex'), rotacionada_em FROM credenciais_renovacao ORDER BY emitida_em DESC LIMIT 3;`
   → só hashes de 32 bytes; nenhum valor igual ao do cookie.

## Cenário 3 — Renovação sem perder a operação (US1)

1. Com token de 10 s: comece uma transferência e pare na revisão. Espere 15 s e confirme.
2. **Esperado**: em Network, um `401`, um `renovar` 200 e a repetição com o mesmo `Idempotency-Key`; o
   comprovante aparece uma única vez.
3. Recarregue a página → continua logado na mesma tela.

## Cenário 4 — Várias abas (FR-015, FR-016)

1. Abra uma segunda aba em `http://localhost:3000` → já entra no início.
2. Com token de 10 s, espere vencer e clique ao mesmo tempo nas duas abas → em Network, os `renovar` não se
   sobrepõem; nenhuma aba cai no login.
3. "Sair" em uma aba → a outra vai ao login sem o aviso de expiração.
4. Entre em uma aba que esteja no login → a outra, também no login, segue para o início.

## Cenário 5 — Sair revoga no servidor (US2)

1. Antes de sair, copie o valor do cookie (DevTools → Application → Cookies).
2. "Sair". Depois:

   ```bash
   curl -i -X POST http://localhost:3000/api/v1/autenticacao/renovar -H "Cookie: bank_renovacao=<valor>"
   ```

3. **Esperado**: `401` `urn:bank:problema:nao-autenticado` com `Set-Cookie` de remoção;
   `SELECT motivo_revogacao FROM sessoes ORDER BY iniciada_em DESC LIMIT 1;` → `Logout`.

## Cenário 6 — Reuso derruba a sessão (US3)

1. Entre e copie o cookie (valor A). Espere o token vencer e faça uma ação (o app rotaciona para B).
2. Espere mais que a tolerância (5 s com os valores do e2e) e rode o `curl` do cenário 5 com o valor A → `401`.
3. **Esperado**: a próxima ação no app leva ao login com "Sua sessão expirou. Entre novamente.";
   `motivo_revogacao = Reuso`; no Aspire Dashboard, log `Warning` de reuso só com `SessaoId` e `UsuarioId` e a
   métrica `bank.autenticacao.renovacoes{resultado="reuso"}`.
4. Repita com o valor A dentro da tolerância → `200` (tolerância), sessão continua ativa.

## Cenário 7 — Inatividade e limite absoluto (US4)

1. Com inatividade de 25 s, entre e não mexa: a 12,5 s do fim (metade do prazo, menor que 60 s), o aviso "Sua sessão vai expirar" aparece nas
   duas abas. "Continuar conectado" em uma fecha o aviso nas duas.
2. Deixe o aviso chegar a zero → login com aviso de sessão expirada.
3. Limite absoluto: em teste automatizado com relógio controlado (vide `research.md`, R15); manualmente, use
   `SESSAO_LIMITE_ABSOLUTO_HORAS` pequeno e confira o aviso "Sua sessão vai terminar" só com "Entendi".

## Cenário 8 — Telemetria sem segredos (FR-020)

No Aspire Dashboard, busque nos logs e traces o valor do cookie e o início do token (`eyJ`) após login,
renovação e saída → nenhuma ocorrência.

## Cenário 9 — Limpeza (FR-013)

Com o Worker rodando, sessões revogadas ou vencidas há mais de 7 dias somem:
`SELECT count(*) FROM sessoes WHERE coalesce(revogada_em, expira_em) < now() - interval '7 days';` → 0 após
o intervalo da limpeza. Métrica `bank.autenticacao.sessoes_removidas` no Worker.
