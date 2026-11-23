# Quickstart: Dados bancários da carteira

**Feature**: [spec.md](spec.md) | **Contrato**: [carteira-minha.md](contracts/carteira-minha.md) |
**Modelo**: [data-model.md](data-model.md)

## Pré-requisitos

- Docker (em execução: os testes de integração usam Testcontainers) e SDK do .NET 10.
- Ambiente das specs 001–011 (`docker compose up -d --build --wait`); a API aplica a migration
  `AdicionarDadosBancariosCarteira` na partida.
- Banco: `docker compose exec postgres psql -U bank -d bank` (credenciais do `.env`).

## Cenário 1 — Testes automatizados

```bash
cd src/backend
dotnet build
dotnet test
```

**Esperado**: unitários de `ContaBancaria` (vetores do contrato, detecção de erro de um dígito),
`Carteira`/`Usuario` e `RegistrarUsuario`; integração cobrindo cadastro, concorrência, backfill,
restrições, scripts e o contrato ([research R10](research.md)); arquitetura sem violações; toda
a suíte anterior verde.

## Cenário 2 — Contas existentes ganham dados (US2)

1. **Antes** de subir esta versão (na `main` anterior), tenha ao menos duas contas cadastradas
   com saldo e uma transferência entre elas.
2. Suba esta versão (`docker compose up -d --build --wait`) e consulte:

   ```sql
   SELECT codigo_banco, agencia, numero_conta, digito_conta, tipo_conta_bancaria, saldo
   FROM carteiras ORDER BY numero_conta;
   ```

   **Esperado**: todas com `001`, `0001`, números distintos a partir de `00000001`, dígito
   conforme o contrato, `conta_corrente` e os mesmos saldos de antes; extratos iguais.

## Cenário 3 — Cadastro gera conta corrente (US1) e consulta (US3)

1. Cadastre uma PF pela API (`POST /api/v1/autenticacao/registrar`), entre e chame
   `GET /api/v1/carteiras/minha`.
2. **Esperado**: `contaBancaria` com banco `001`/`One Bank`, agência `0001`, número igual ao
   maior número anterior + 1 (ou mais, se houve cadastros com falha), dígito válido e tipo
   `ContaCorrente`; demais campos iguais aos de antes.

## Cenário 4 — Scripts de outros bancos e poupança (US4)

```bash
docker compose exec -T postgres psql -U bank -d bank -v ON_ERROR_STOP=1 < scripts/sql/dados-bancarios/registrar-contas-outros-bancos.sql
docker compose exec -T postgres psql -U bank -d bank -v ON_ERROR_STOP=1 < scripts/sql/dados-bancarios/criar-conta-poupanca.sql
```

1. `SELECT codigo_banco, count(*) FROM contas_externas GROUP BY 1 ORDER BY 1;` → bancos 002 a
   005, cada um com contas.
2. Entre com o titular da poupança (e-mail e senha no cabeçalho do script) e chame
   `GET /api/v1/carteiras/minha` → `contaBancaria.tipo = "ContaPoupanca"`, banco `001`.
3. Execute os dois scripts de novo → as contagens não mudam (SC-005).

## Cenário 5 — Restrições (Edge Cases)

```sql
-- banco fora dos cinco: rejeitado pela FK
UPDATE contas_externas SET codigo_banco = '999' WHERE ctid IN (SELECT ctid FROM contas_externas LIMIT 1);
-- dígito errado: rejeitado por ck_carteiras_digito_conta
UPDATE carteiras SET digito_conta = ((digito_conta::int + 1) % 10)::text WHERE ctid IN (SELECT ctid FROM carteiras LIMIT 1);
```

**Esperado**: as duas instruções falham, sem alterar dados.
