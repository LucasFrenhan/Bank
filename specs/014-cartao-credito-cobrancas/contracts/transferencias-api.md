# Contrato HTTP: Transferências e extrato — v1.3.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/009 — transferencias-api.md v1.2.0](../../009-limite-transferencia/contracts/transferencias-api.md)

Lista só o que muda em relação à v1.2.0. A mudança é aditiva (MINOR): novos valores de `tipo` no extrato.

## `GET /api/v1/transferencias/extrato`

| `tipo` | `direcao` | `descricao` | `transferenciaId` / `contraparte` |
| --- | --- | --- | --- |
| `PagamentoFatura` | `Saida` | "Pagamento da fatura 12/2026" | nulos |
| `LiquidacaoRecebivel` | `Entrada` | "Recebimento: {descrição da cobrança}" | nulos |
| `AntecipacaoRecebivel` | `Entrada` | "Antecipação: {descrição da cobrança}" | nulos |

`valor` é o que entrou ou saiu da carteira: com juros no pagamento de fatura vencida e líquido da taxa na
antecipação. A descrição é truncada em 200 caracteres. Clientes devem tratar `tipo` desconhecido como
lançamento genérico.

## Frontend

`nomeContraparte`, o detalhe do início e o do extrato mobile mostram "Pagamento de fatura", "Recebimento
de cobrança" e "Antecipação de recebível"; tipo desconhecido, "Lançamento". "Saldo inicial" fica só para
`Abertura`.

## Histórico

- **1.3.0** (2026-11-24): novos tipos de lançamento no extrato — issue #22.
- **1.2.0** (2026-11-18): erro `limite-excedido` e `GET /limites` — issue #33.
