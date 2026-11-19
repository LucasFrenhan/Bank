# Quickstart: Limite de transferência

**Feature**: [spec.md](spec.md) | **Contratos**: [transferencias-api.md](contracts/transferencias-api.md), [frontend.md](contracts/frontend.md)

## Pré-requisitos

- Ambiente da spec 006 (Docker; SDK do .NET 10; Node do frontend).

## Cenário 1 — Testes automatizados do backend

```bash
cd src/backend
dotnet build
dotnet test
```

**Esperado**: unitários de `LimitesTransferencia`, `CalendarioBrasilia`, opções, `RealizarTransferencia`
e `ConsultarLimites`; integração `LimiteTransferenciaTests` e `LimiteDiarioTests` (422 com extensões, valor exato,
replay com limite esgotado, virada do dia de Brasília, `GET /limites` 200/401/403) e
`ConcorrenciaLimiteTests` (5 × R$ 300 com diário de R$ 1.000 → 3 concluídas); OpenAPI e
telemetria sem dados pessoais. Os testes passam tanto no Windows quanto no runner Linux do CI
(resolução do fuso, research R4).

## Cenário 2 — Manual pela API do Compose, com limites baixos

Sobe a API com limites que o saldo de demonstração (R$ 10,00) alcança:

```bash
LIMITE_PF_POR_OPERACAO=4.00 LIMITE_PF_DIARIO=6.00 docker compose up -d --build --wait
```

(ou as mesmas variáveis no `.env`; sem elas, valem os padrões R$ 1.000,00 / R$ 5.000,00).

1. Cadastre e entre com uma PF **A** e uma PJ **B**.
2. `GET /api/v1/transferencias/limites` com o token de A → 200
   `{ porOperacao: 4.00, diario: 6.00, transferidoHoje: 0, disponivelHoje: 6.00, dia }`.
3. `POST /api/v1/transferencias` de R$ 4,01 para B → 422 `limite-excedido`,
   `tipoLimite=PorOperacao`, `limite=4.00`, `valorDisponivel=4.00`; saldo de A continua R$ 10,00.
4. Transfira R$ 4,00 (201) e depois R$ 2,01 → 422 `tipoLimite=Diario`, `valorDisponivel=2.00`.
5. Reenvie o passo 4 (mesma `Idempotency-Key`, R$ 4,00) → 200 com o comprovante original.
6. Transfira R$ 2,00 → 201; `GET /limites` → `transferidoHoje: 6.00`, `disponivelHoje: 0.00`.
7. Com o token de B, `GET /limites` → 403 `acesso-negado`.

## Cenário 3 — Tela Transferir

Com o ambiente do cenário 2 e uma PF nova, em desktop e mobile (`http://localhost:3000`):

1. Transferir → a dica mostra "Disponível: R$ 10,00 · Limite agora: R$ 4,00 · mínimo R$ 0,01".
2. Valor R$ 5,00 → "Continuar" não avança; aviso "Limite excedido — O limite por transferência é
   R$ 4,00. Ajuste o valor para continuar."
3. Conclua R$ 4,00 e R$ 2,00; volte a Transferir → "Limite agora: R$ 0,00"; tentar R$ 0,01 mostra
   "Você já usou todo o limite diário de R$ 6,00. Ele renova à meia-noite (horário de Brasília)."

## Cenário 4 — Frontend automatizado

```bash
cd src/frontend
npm run lint && npm run typecheck && npm test && npm run e2e
```

**Esperado**: testes de `traduzirErro`, `useLimites`, dica e avisos em Transferir e Confirmação;
e2e de Transferir com a dica do limite.
