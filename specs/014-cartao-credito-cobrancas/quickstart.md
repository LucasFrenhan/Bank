# Quickstart: Cartão de crédito, cobranças, faturas e recebimento do lojista

**Feature**: [spec.md](spec.md) | **Contratos**: [API](contracts/cartoes-cobrancas-api.md),
[evento](contracts/evento-cobranca-paga.md), [extrato](contracts/transferencias-api.md) |
**Modelo**: [data-model.md](data-model.md)

## Pré-requisitos

- Docker em execução e SDK do .NET 10; Node da spec 004 para o frontend.
- Compose: `docker compose up -d --build --wait` (a chave `CARTOES_CHAVE_DERIVACAO` tem padrão só de
  desenvolvimento).
- Fora do Compose: `dotnet user-secrets set "Cartoes:ChaveDerivacao" "$(openssl rand -base64 48)"
  --project src/backend/src/Bank.Api`.
- Scalar em `http://localhost:8080/scalar/v1`; `psql`: `docker compose exec postgres psql -U bank -d bank`.

## Cenário 1 — Testes automatizados

```bash
cd src/backend && dotnet build && dotnet test
cd ../frontend && npm run lint && npm run typecheck && npm test
```

**Esperado**: tudo verde, incluindo os testes das specs anteriores (SC-010).

## Cenário 2 — Cartão (US1)

1. Cadastre e entre com uma PF. `POST /api/v1/cartoes` com `{"bandeira":"001"}` → 201, limite de
   R$ 5.000,00. Repita com `002` → 409 `cartao-ja-existente`.
2. `GET /cartoes/meu/dados` duas vezes → os mesmos número, validade e CVV; `Cache-Control: no-store`.
3. `SELECT * FROM cartoes;` → só os 4 últimos dígitos; nem número nem CVV.

## Cenário 3 — Cobrança e pagamento (US2, US3)

1. Com uma PJ, `POST /api/v1/cobrancas` de R$ 299,90.
2. Com a PF, `POST /cobrancas/{id}/pagar` com os dados do cartão e `Idempotency-Key` → 200, `Paga`. Repita
   com a mesma chave → mesmo corpo. Com outra chave → 409 `cobranca-nao-pendente`.
3. `GET /cartoes/meu` → disponível R$ 4.700,10 e R$ 299,90 a lançar. Saldos das carteiras iguais.
4. Emita R$ 4.700,11 e pague → 422 `limite-cartao-insuficiente`; a cobrança segue `Pendente`, com
   `ultimaRecusa` visível só para a PJ.
5. Pague com CVV errado → 422 `cartao-nao-autorizado`. Com número de final alterado (Luhn inválido) → 400.

## Cenário 4 — Virada do dia, faturas e recebíveis (US4–US6)

O relógio não é ajustável no Compose; as datas são conferidas pelos testes de integração com relógio
controlado:

```bash
cd src/backend
dotnet test --project tests/Bank.IntegrationTests --filter-class "*ProcessamentoDiarioTests" --filter-class "*FaturasTests" --filter-class "*RecebiveisTests"
```

**Esperado**: ciclo do dia 07, dia não útil, liberação, vencimento ajustado, juros de R$ 50,00 em 10 dias
sobre R$ 1.000,00, bloqueio por atraso, liquidação em 30 dias úteis e processamento repetido, atrasado e
concorrente sem duplicar.

No Compose, sem esperar a virada:

1. `GET /faturas/{competência atual}` → `Aberta`, sem a compra de hoje.
2. Com a PJ, `GET /recebiveis` → data prevista no 30º dia útil. `POST /recebiveis/{id}/antecipar` →
   taxa R$ 6,00, líquido R$ 293,90; saldo e extrato atualizados. Nova chave → 409.
3. No dia útil seguinte, a compra aparece na fatura até 1 minuto depois da 00:00.

## Cenário 5 — Notificações e telemetria (US7, FR-037)

1. `SELECT tipo, mensagem FROM notificacoes_auditoria ORDER BY criado_em DESC LIMIT 2;` →
   `comprovante_cobranca` e `cobranca_paga`, sem dígitos de cartão.
2. Aspire Dashboard (`http://localhost:18888`): métricas `bank.cobrancas.autorizacoes` e
   `bank.processamento.*`; trace do pagamento até a notificação. Nenhum número de cartão ou CVV.
3. `GET http://localhost:15672` → filas de espera de cobranças criadas e DLQ vazia.

## Cenário 6 — Telas existentes (FR-038)

Com a PJ que antecipou, abra o início e o extrato no desktop e no celular → "Antecipação de recebível",
sem link de comprovante.
