# Contrato: Ambiente Local

**Feature**: [../spec.md](../spec.md) | **Versão do contrato**: 1.0.0

Este é o contrato que o ambiente local oferece às aplicações do projeto (API, workers) e
às pessoas desenvolvedoras. Specs futuras dependem destes nomes, portas e endpoints;
alterá-los de forma incompatível exige nova versão do contrato (Princípio V).

Valores abaixo são os padrões; portas e credenciais podem ser sobrescritas pelo `.env`
(ver [data-model.md](../data-model.md#configuração-local)). Todas as portas escutam
somente em `127.0.0.1`.

## Serviços

| Serviço | Host (fora do Compose) | Host (dentro da rede do Compose) | Portas | Credenciais padrão |
| --- | --- | --- | --- | --- |
| PostgreSQL | `localhost` | `postgres` | `5432` | `bank` / `bank_dev_password`, banco `bank` |
| RabbitMQ (AMQP) | `localhost` | `rabbitmq` | `5672` | `bank` / `bank_dev_password`, vhost `bank` |
| RabbitMQ (Management UI) | `http://localhost:15672` | `http://rabbitmq:15672` | `15672` | `bank` / `bank_dev_password` |
| Aspire Dashboard (UI) | `http://localhost:18888` | `http://aspire-dashboard:18888` | `18888` | sem autenticação |
| OTLP/gRPC | `http://localhost:4317` | `http://aspire-dashboard:18889` | `4317` | sem autenticação |
| OTLP/HTTP | `http://localhost:4318` | `http://aspire-dashboard:18890` | `4318` | sem autenticação |

## Strings de conexão de referência

```text
# PostgreSQL (Npgsql)
Host=localhost;Port=5432;Database=bank;Username=bank;Password=bank_dev_password

# RabbitMQ (URI AMQP; vhost "bank")
amqp://bank:bank_dev_password@localhost:5672/bank

# OpenTelemetry (variáveis padrão dos SDKs)
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

## Comandos

| Ação | Comando | Resultado esperado |
| --- | --- | --- |
| Subir e aguardar saúde | `docker compose up -d --wait` | Código de saída 0 somente quando todos os serviços estão `healthy` |
| Ver estado | `docker compose ps` | Coluna `STATUS` com `(healthy)` em todos os serviços |
| Parar (mantém dados) | `docker compose down` | Contêineres removidos; volumes preservados |
| Reset completo | `docker compose down -v` | Contêineres e volumes do projeto removidos |

## Garantias

- Todo serviço possui healthcheck; serviços futuros podem depender deles com
  `condition: service_healthy`.
- Dados do PostgreSQL e do RabbitMQ persistem entre reinícios até o reset.
- A telemetria no Aspire Dashboard **não** persiste entre reinícios do contêiner.
