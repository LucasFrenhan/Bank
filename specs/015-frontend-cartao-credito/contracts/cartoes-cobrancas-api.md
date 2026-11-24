# Contrato HTTP: Cartões, cobranças, faturas e recebíveis — v1.1.0

**Feature**: [../spec.md](../spec.md) (FR-009a, FR-009b) | **Base**: [specs/014 — cartoes-cobrancas-api.md v1.0.0](../../014-cartao-credito-cobrancas/contracts/cartoes-cobrancas-api.md)

Lista só o que muda em relação à v1.0.0. A mudança é aditiva (MINOR).

## Campo `codigo`

`CobrancaResposta` (emissão, consulta, lista e cancelamento) ganha `codigo`, logo após `cobrancaId`: texto
`XXXX-XXXX`, sempre presente, único e imutável, com caracteres de `23456789ABCDEFGHJKMNPQRSTVWXYZ`.

```json
{ "cobrancaId": "019a8f77-1111-7890-abcd-ef1234567890", "codigo": "7K3F-9QXM" }
```

## `GET /api/v1/cobrancas/codigo/{codigo}`

**Permissão**: autenticado. `codigo` aceita maiúsculas ou minúsculas, com ou sem hífen e espaços. **200**: a
mesma resposta e as mesmas regras de visibilidade de `GET /cobrancas/{cobrancaId}`. **Erros**: 401, 403, 404
`cobranca-nao-encontrada` (malformado ou inexistente; sem 400, ver [plano](../plan.md#complexity-tracking)).

Pagar e cancelar continuam pelo `cobrancaId`.

## Histórico

- **1.1.0** (2026-11-24): código curto da cobrança e consulta por ele (issue #54).
- **1.0.0** (2026-11-24): versão inicial (issue #22).
