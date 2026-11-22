# Contrato de evento: `cobranca.paga` — schema v1 (preparação, Fase 4)

**Feature**: [../spec.md](../spec.md) (FR-012) | Transporte: [topologia-e-mensagens.md](topologia-e-mensagens.md)

Reservado para a spec de cobranças: nesta spec **nada publica nem consome** este evento; só a fila
`notificacoes.cobrancas.fila` e sua DLQ são declaradas. A spec de cobranças pode ajustar este
contrato antes da primeira publicação sem quebra de compatibilidade (ainda não há produtor).

Routing key `cobranca.paga`; `type` = `cobranca.paga`; header `versao` = `1`.

```json
{
  "eventoId": "019a8f77-4444-7890-abcd-ef1234567890",
  "tipoEvento": "CobrancaPaga",
  "versao": 1,
  "cobrancaId": "019a8f77-1111-7890-abcd-ef1234567890",
  "lojistaUsuarioId": "019a8f99-8888-7890-abcd-ef1234567890",
  "pagadorUsuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "valor": 299.90,
  "ultimosDigitosCartao": "4242",
  "ocorridoEm": "2026-11-17T15:10:00+00:00"
}
```

| Campo | Tipo | Regra |
| --- | --- | --- |
| `eventoId` | uuid | UUIDv7, único; = `message-id` |
| `tipoEvento` | string | `"CobrancaPaga"` |
| `versao` | inteiro | `1` |
| `cobrancaId` | uuid | Cobrança paga |
| `lojistaUsuarioId` | uuid | Titular PJ que recebe |
| `pagadorUsuarioId` | uuid | Titular que pagou |
| `valor` | número | BRL, 2 casas, > 0 |
| `ultimosDigitosCartao` | string | Exatamente 4 dígitos; nunca o número completo (Princípio VI) |
| `ocorridoEm` | string (date-time) | UTC |
