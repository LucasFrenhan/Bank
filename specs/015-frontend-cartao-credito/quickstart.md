# Quickstart: Frontend de cartão de crédito, cobranças, faturas e recebíveis

**Feature**: [spec.md](spec.md) | **Contratos**: [frontend](contracts/frontend.md),
[API 1.1.0](contracts/cartoes-cobrancas-api.md)

## Pré-requisitos

- Docker, SDK do .NET 10 e Node da spec 004.
- `docker compose up -d --build --wait` na raiz. A API aplica a migration `AdicionarCodigoCobranca`.
- App em `http://localhost:3000`; `psql`: `docker compose exec postgres psql -U bank -d bank`.

## Cenário 1 — Testes automatizados

```bash
dotnet test --solution src/backend/Bank.slnx
cd src/frontend && npm run lint && npm run typecheck && npm test && npm run build && npm run e2e
```

**Esperado**: tudo verde, em `desktop` e `mobile`, incluindo os testes anteriores (SC-008).

## Cenário 2 — Código das cobranças antigas (FR-009a)

Antes de subir esta versão, tenha cobranças emitidas. Depois:
`SELECT count(*), count(DISTINCT codigo) FROM cobrancas;` → números iguais; os demais dados não mudam.

## Cenário 3 — Cartão (US1)

1. PF nova → "Cartão": sem escolher bandeira, erro; com Visa, cartão emitido com R$ 5.000,00.
2. Olho: riscado → aberto, dados visíveis; troque de aba e volte → ocultos. Espere 1 minuto com os dados
   visíveis → ocultos.
3. DevTools → Application: nenhum número ou CVV em `sessionStorage` e `localStorage`.

## Cenário 4 — Cobrança e pagamento (US2, US3)

1. PJ → "Cobranças" → "Nova cobrança" de R$ 5,00 → detalhe com código e link. Deixe a tela aberta.
2. Em outra janela, sem sessão, abra o link → login como PF → volta à cobrança. "Usar meu cartão" → CVV vazio
   com foco → pagar → comprovante. A tela da PJ muda para "Paga" em até 10 segundos.
3. Nova cobrança: CVV errado → "Cartão não autorizado" e CVV limpo. De R$ 6.000,00 → "Limite insuficiente".
4. No celular, digite o código em minúsculas e sem hífen na tela do cartão → abre a cobrança.

## Cenário 5 — Faturas (US4, US5)

O e2e `faturas.spec.ts` prepara faturas disponível e vencida por SQL e cobre o aviso do início, os juros e o
pagamento. Manualmente, confira a fatura aberta do ciclo atual, sem a compra de hoje.

## Cenário 6 — Recebíveis (US6)

PJ → "Cobranças" → "Recebíveis" → "Antecipar" → confira taxa e líquido → confirmar → "Antecipado"; saldo e
extrato atualizados.

## Cenário 7 — Acessibilidade e larguras (US7)

Percorra as telas só pelo teclado e em 320, 390, 768, 1024, 1440 e 1920 px. Com NVDA, confira a leitura do
cartão exibido.
