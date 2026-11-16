# Data Model: Ambiente de Infraestrutura Local

**Feature**: [spec.md](spec.md) | **Date**: 2026-11-16

Esta feature não cria entidades de negócio nem schema de banco. O "modelo" é a
configuração do ambiente: serviços, variáveis de configuração local e volumes. Os valores
concretos (portas, URLs, credenciais) estão no contrato
[contracts/ambiente-local.md](contracts/ambiente-local.md).

## Serviço de infraestrutura

| Campo | Descrição | Regras |
| --- | --- | --- |
| nome | Identificador do serviço no Compose (`postgres`, `rabbitmq`, `aspire-dashboard`) | Único no projeto `bank` |
| imagem | Imagem com tag de versão completa | Tags flutuantes proibidas (FR-012) |
| hostname | Nome do host no contêiner | Obrigatório e fixo para o RabbitMQ (R5) |
| portas | Mapeamentos `127.0.0.1:<porta-host>:<porta-contêiner>` | Porta do host configurável por variável (FR-009) |
| healthcheck | Comando, intervalo, timeout, retries e start_period | Obrigatório em todo serviço (FR-005) |
| volumes | Volumes nomeados montados | Obrigatório para `postgres` e `rabbitmq` (FR-007) |
| depends_on | Dependências com `condition: service_healthy` | Nenhuma nesta spec (R4) |

### Estados (healthcheck)

```text
(criado) → starting ──ok──→ healthy ──falha × retries──→ unhealthy
                │                         ↑                  │
                └──falha × retries──→ unhealthy ──ok─────────┘
```

`docker compose up --wait` retorna sucesso somente quando todos estão `healthy`.

## Configuração local

| Variável | Padrão | Usada por |
| --- | --- | --- |
| `POSTGRES_USER` | `bank` | postgres |
| `POSTGRES_PASSWORD` | `bank_dev_password` | postgres |
| `POSTGRES_DB` | `bank` | postgres |
| `POSTGRES_PORT` | `5432` | postgres (porta do host) |
| `RABBITMQ_USER` | `bank` | rabbitmq |
| `RABBITMQ_PASSWORD` | `bank_dev_password` | rabbitmq |
| `RABBITMQ_VHOST` | `bank` | rabbitmq |
| `RABBITMQ_AMQP_PORT` | `5672` | rabbitmq (porta do host) |
| `RABBITMQ_MANAGEMENT_PORT` | `15672` | rabbitmq (porta do host) |
| `ASPIRE_DASHBOARD_PORT` | `18888` | aspire-dashboard (UI, porta do host) |
| `OTLP_GRPC_PORT` | `4317` | aspire-dashboard (porta do host) |
| `OTLP_HTTP_PORT` | `4318` | aspire-dashboard (porta do host) |

Regras:

- `.env.example` (versionado) contém todas as variáveis acima com os valores padrão.
- `.env` (não versionado) é opcional; quando ausente, o Compose usa os mesmos padrões.
- Os valores padrão são exclusivamente para desenvolvimento local (FR-010).

## Volume de dados

| Volume | Montado em | Conteúdo |
| --- | --- | --- |
| `bank_postgres-data` | `postgres:/var/lib/postgresql` | Cluster do PostgreSQL 18 (R5) |
| `bank_rabbitmq-data` | `rabbitmq:/var/lib/rabbitmq` | Definições e mensagens persistentes do RabbitMQ |

- Sobrevivem a `docker compose stop`/`down` e a recriações de contêiner (FR-007).
- Removidos somente por `docker compose down -v` (FR-008).
- A telemetria do Aspire Dashboard não tem volume: fica em memória (R1).
