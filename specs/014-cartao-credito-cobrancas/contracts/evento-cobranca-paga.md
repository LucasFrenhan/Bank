# Contrato de evento: `cobranca.paga` — schema v1 (primeira publicação)

**Feature**: [../spec.md](../spec.md) (FR-021, FR-035) | **Base**: [specs/010 — evento-cobranca-paga.md](../../010-mensageria-rabbitmq/contracts/evento-cobranca-paga.md)

Esta spec passa a ser a referência do contrato. Produtor: API, pelo outbox, na transação do pagamento.
Consumidor: `notificacoes-cobrancas` (Worker). Routing key e `type` `cobranca.paga`; header `versao` `1`.

```json
{
  "eventoId": "019a8f77-4444-7890-abcd-ef1234567890",
  "tipoEvento": "CobrancaPaga",
  "versao": 1,
  "cobrancaId": "019a8f77-1111-7890-abcd-ef1234567890",
  "lojistaUsuarioId": "019a8f99-8888-7890-abcd-ef1234567890",
  "pagadorUsuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "valor": 299.90,
  "ultimosDigitosCartao": "7899",
  "ocorridoEm": "2026-11-24T16:35:00+00:00",
  "lojistaNome": "Cafeteria Grão Especial LTDA"
}
```

Os campos da spec 010 não mudam. Campo novo: `lojistaNome` (string, opcional), usado no texto da
notificação do pagador. O nome do pagador não é publicado.

`ocorridoEm` é o instante da autorização. O evento significa **cobrança autorizada**; o crédito ao
lojista ocorre depois (spec 014, FR-030 a FR-032).

## Notificações geradas (`notificacoes_auditoria`)

| Tipo | Destinatário | Texto |
| --- | --- | --- |
| `comprovante_cobranca` | pagador | "Você pagou R$ 299,90 para Cafeteria Grão Especial LTDA no cartão de crédito." (sem nome: "Você pagou R$ 299,90 no cartão de crédito.") |
| `cobranca_paga` | lojista | "A sua cobrança de R$ 299,90 foi paga." |

Sem dígitos do cartão, documento ou descrição da cobrança.

## Topologia (complemento da spec 010, versão 1.1.0)

| Fila | Argumentos |
| --- | --- |
| `notificacoes.cobrancas.espera.1000` / `.2000` / `.4000` | `x-message-ttl` = intervalo, `x-dead-letter-exchange=""`, `x-dead-letter-routing-key=notificacoes.cobrancas.fila` |

Fila principal, DLQ, novas tentativas, ACK e idempotência seguem a spec 010 sem mudança.

## Histórico

- **v1, revisão 2026-11-24**: `lojistaNome` opcional, primeiro produtor e consumidor (issue #22).
- **v1** (2026-11-21): preparação (spec 010).
