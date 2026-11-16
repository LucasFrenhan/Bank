# Data Model: Setup da API

**Feature**: [spec.md](spec.md) | **Date**: 2026-11-16

Esta feature não tem entidades de negócio nem persistência. Os "dados" são a configuração
da API e o formato padronizado de erro.

## Configuração da API

| Chave | Padrão (SDK) | Padrão (Compose) | Regra |
| --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Development` | `Production` desliga OpenAPI e Scalar (FR-005); `Testing` habilita os endpoints de teste (research R7) |
| `ASPNETCORE_URLS` / porta | `http://localhost:5080` | `http://+:8080`, host `127.0.0.1:${API_PORT:-8080}` | Alterável por `--urls` ou `.env` (FR-011) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://localhost:4317` | `http://aspire-dashboard:18889` | Variável padrão do OpenTelemetry (FR-006, FR-011) |
| `OTEL_SERVICE_NAME` | — | — | Não definida: o nome `bank-api` é fixado no código para ser estável (FR-006); a variável continua podendo sobrescrevê-lo |

Arquivos: `src/backend/src/Bank.Api/Properties/launchSettings.json` (SDK),
`docker-compose.yml` + `.env.example` (`API_PORT=8080`) para o Compose. Nenhum segredo é
necessário nesta spec.

## Resposta de erro padronizada (`ProblemDetails`)

| Campo | Tipo | Obrigatório | Regra |
| --- | --- | --- | --- |
| `type` | string (URI) | sim | Padrão do framework por status (RFC 9110); tipos de negócio virão com as specs de negócio |
| `title` | string | sim | Texto curto do status |
| `status` | int | sim | Igual ao status HTTP |
| `detail` | string | não | MUST NOT conter mensagem de exceção, stack trace ou dados internos em 5xx |
| `traceId` | string (32 hex) | sim | `Activity.Current.TraceId`; fallback `HttpContext.TraceIdentifier` (research R6) |
| `errors` | objeto | só em 400 de validação | `ValidationProblemDetails` (Princípio III) — usado a partir das specs de negócio |

Content-Type: `application/problem+json`.

## Resposta de saúde

`GET /health` → `200 text/plain` com `Healthy` (formato padrão do ASP.NET Core Health
Checks). `503` com `Unhealthy` quando algum health check registrado falhar (nenhum nesta
spec além da própria aplicação).
