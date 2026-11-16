# Contrato: Base da API

**Feature**: [../spec.md](../spec.md) | **Versão do contrato**: 1.0.0

Contrato HTTP estabelecido por esta spec e herdado por todas as specs de negócio. O
documento OpenAPI gerado em tempo de execução (`/openapi/v1.json`) é a fonte de verdade
dos endpoints; este arquivo fixa as garantias que não aparecem nele. Mudanças
incompatíveis exigem nova versão do contrato (Princípio V).

## Endpoints

| Método | Rota | Ambientes | Autenticação | Respostas |
| --- | --- | --- | --- | --- |
| GET | `/health` | todos | não | `200 Healthy`, `503 Unhealthy` |
| GET | `/openapi/v1.json` | todos exceto `Production` | não | `200 application/json` (OpenAPI) |
| GET | `/scalar/v1` | todos exceto `Production` | não | `200 text/html` (UI interativa) |

Endpoints internos, **fora do contrato** (não publicados no OpenAPI), existentes apenas no
ambiente `Testing`: `GET /__test/falha` e `POST /__test/eco` (research R7).

## Garantias de erro (todas as rotas)

| Situação | Status | Corpo |
| --- | --- | --- |
| Rota inexistente | 404 | `ProblemDetails` |
| Método não suportado na rota | 405 | `ProblemDetails` |
| Corpo JSON malformado / binding inválido | 400 | `ProblemDetails` |
| Falha não tratada | 500 | `ProblemDetails`, sem mensagem de exceção nem stack trace |

Toda resposta de erro tem `Content-Type: application/problem+json` e a extensão `traceId`
(32 caracteres hexadecimais), igual ao trace ID registrado na telemetria. Exemplo:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

## Telemetria

| Sinal | Conteúdo mínimo | Nome do serviço |
| --- | --- | --- |
| Traces | Requisições HTTP recebidas (exceto `/health`, `/openapi`, `/scalar`) e chamadas HTTP de saída | `bank-api` |
| Métricas | Meters do ASP.NET Core (requisições: contagem e duração), Kestrel, HttpClient e `System.Runtime` | `bank-api` |
| Logs | Estruturados, com `TraceId`/`SpanId` da requisição | `bank-api` |

Exportação: OTLP para `OTEL_EXPORTER_OTLP_ENDPOINT`. Falhas de exportação não afetam as
respostas.
