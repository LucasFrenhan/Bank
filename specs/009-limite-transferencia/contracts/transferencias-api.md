# Contrato HTTP: Transferências e extrato — v1.2.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/008 — transferencias-api.md v1.1.0](../../008-consulta-transferencia/contracts/transferencias-api.md)

Lista só o que muda em relação à v1.1.0. Todas as mudanças são aditivas (MINOR): um erro novo em
`POST /api/v1/transferencias` e um endpoint novo.

## `POST /api/v1/transferencias` — novo erro 422 `limite-excedido`

Ordem das verificações (FR-008): validação (400) → idempotência (200 replay / 409) →
destinatário (404) → autotransferência (422) → **limite por operação** → **limite diário** →
saldo (422). Um replay idempotente devolve o comprovante original sem verificar limites.

```http
HTTP/1.1 422 Unprocessable Entity
Content-Type: application/problem+json
```

```json
{
  "type": "urn:bank:problema:limite-excedido",
  "title": "Limite de transferência excedido",
  "status": 422,
  "detail": "O valor passa do limite diário de transferências.",
  "tipoLimite": "Diario",
  "limite": 5000.00,
  "valorDisponivel": 500.00,
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

| Extensão | Tipo | Significado |
| --- | --- | --- |
| `tipoLimite` | `"PorOperacao"` \| `"Diario"` | Limite que recusou. Novos valores podem surgir em versões futuras (MINOR); clientes devem tratar valores desconhecidos como limite genérico |
| `limite` | number | Valor do limite aplicado, em BRL |
| `valorDisponivel` | number | Máximo que poderia ser enviado agora por esse limite: o próprio limite (`PorOperacao`) ou `max(0, diário − transferido hoje)` (`Diario`) |

`detail` por tipo: "O valor passa do limite por transferência." / "O valor passa do limite
diário de transferências." Consumidores decidem por `type` e pelas extensões, nunca por `detail`.

## `GET /api/v1/transferencias/limites` — novo

Limites da PF autenticada e uso no dia de Brasília (FR-012). Informativo: o envio verifica de
novo, sob bloqueio.

Autorização: `Bearer`, mesmas políticas de `POST /api/v1/transferencias` (somente PF).

### 200 OK

```json
{
  "porOperacao": 1000.00,
  "diario": 5000.00,
  "transferidoHoje": 4500.00,
  "disponivelHoje": 500.00,
  "dia": "2026-11-18"
}
```

| Campo | Tipo | Observação |
| --- | --- | --- |
| `porOperacao` | number \| null | `null` = sem limite por operação |
| `diario` | number \| null | `null` = sem limite diário |
| `transferidoHoje` | number | Soma das transferências concluídas enviadas no dia |
| `disponivelHoje` | number \| null | `max(0, diario − transferidoHoje)`; `null` se `diario` for `null` |
| `dia` | string (`date`) | Data de Brasília (`America/Sao_Paulo`) do cálculo |

### Erros

| Status | `type` | Quando |
| --- | --- | --- |
| 401 | — | Sem token ou token inválido |
| 403 | `urn:bank:problema:acesso-negado` | Conta PJ (sem `RealizarTransferencia`) |

## OpenAPI

- `Transferir`: a resposta 422 passa a citar `limite-excedido` na descrição.
- Nova operação `ConsultarLimites` (`GET /api/v1/transferencias/limites`), com `Bearer` e as
  respostas 200, 401 e 403; esquema `LimitesResposta`.

## Histórico

- **1.3.0** (2026-11-24): versão atual em [specs/014 — transferencias-api.md](../../014-cartao-credito-cobrancas/contracts/transferencias-api.md) — novos tipos de lançamento no extrato (issue #22).
- **1.2.0** (2026-11-18): erro `limite-excedido` no envio; `GET /limites` — issue #33.
- **1.1.0** (2026-11-18): consulta por identificador; documento do pagador mascarado — issue #36.
- **1.0.0** (2026-11-17): versão inicial (issues #8 e #13).
