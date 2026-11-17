# Quickstart: Motor de transferências e extrato

**Feature**: [spec.md](spec.md) | **Contratos**: [API](contracts/transferencias-api.md), [evento](contracts/evento-transferencia-concluida.md)

## Pré-requisitos

Ambiente das specs 001–005 (`docs/desenvolvimento.md`); Docker em execução.

## Cenário 1 — Testes

```bash
dotnet test --solution src/backend/Bank.slnx
```

**Esperado**: todos aprovados, incluindo concorrência (5 × R$ 300 com saldo R$ 1.000 → 3
concluídas, saldo R$ 100), 50 transferências cruzadas sem impasse, idempotência em paralelo,
imutabilidade e outbox.

## Cenário 2 — Fluxo na API (Compose)

```bash
docker compose up -d --build --wait
```

1. Cadastrar uma PF e uma PJ (`POST /api/v1/autenticacao/registrar`) e entrar com a PF.
2. `POST /api/v1/transferencias/destinatarios/consultar` com o CNPJ da PJ → nome e tipo.
3. `POST /api/v1/transferencias` com `Idempotency-Key: $(uuidgen)` e `valor` 4.50 → 201.
4. Repetir o passo 3 com a mesma chave → 200 com o mesmo `transferenciaId`; saldo da PF R$ 5,50.
5. Mesma chave com `valor` 5.00 → 409 `idempotencia-divergente`.
6. `valor` 100.00 → 422 `saldo-insuficiente`; próprio CPF → 422 `autotransferencia`.
7. `GET /api/v1/transferencias/extrato` (PF) → `Saida` de 4,50 e `Entrada` "Saldo inicial de
   demonstração" de 10,00; com o token da PJ → `Entrada` de 4,50 com o CPF da PF mascarado.
8. Login da PJ e `POST /api/v1/transferencias` → 403.

## Cenário 3 — Banco

```sql
-- saldo = soma dos lançamentos, para toda carteira (deve voltar 0 linhas)
SELECT c.id FROM carteiras c
LEFT JOIN lancamentos l ON l.carteira_id = c.id
GROUP BY c.id, c.saldo
HAVING c.saldo <> COALESCE(SUM(CASE l.direcao WHEN 'Entrada' THEN l.valor ELSE -l.valor END), 0);

-- imutabilidade: deve falhar
UPDATE lancamentos SET valor = 1 WHERE id = (SELECT id FROM lancamentos LIMIT 1);

-- outbox: um evento por transferência
SELECT tipo, versao, payload FROM outbox_eventos ORDER BY ocorrido_em DESC LIMIT 5;
```

(`docker compose exec postgres psql -U bank -d bank`)

## Cenário 4 — Abertura das carteiras existentes

Com a base local da spec 003 (carteiras criadas antes desta spec), subir a API: cada carteira
com saldo passa a ter uma `Abertura`; subir de novo não duplica.

## Cenário 5 — Telemetria e documentação

- Scalar (`/scalar/v1`): os três endpoints, com `Bearer` e o cabeçalho `Idempotency-Key`.
- Aspire Dashboard → Metrics → `bank-api` → `Bank.Transferencias`: concluídas, recusadas por
  motivo, replays, valor e consultas de destinatário; nenhum documento, nome ou e-mail em logs e
  traces.
