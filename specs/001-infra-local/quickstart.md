# Quickstart: Ambiente de Infraestrutura Local

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/ambiente-local.md](contracts/ambiente-local.md)

Roteiro de validação ponta a ponta. Cada cenário referencia a história e os critérios da
spec que comprova.

## Pré-requisitos

- Docker Desktop (Windows/macOS) ou Docker Engine (Linux) em execução, com Compose v2.
- Portas livres no host: 5432, 5672, 15672, 18888, 4317 e 4318 (ou alteradas no `.env`).

## Cenário 1 — Subida com um comando (US1, FR-001..003, SC-001, SC-002)

```bash
cp .env.example .env          # opcional: o ambiente sobe com os padrões mesmo sem .env
docker compose up -d --wait
docker compose ps
```

**Esperado**: o comando retorna código 0 em até 60s (com as imagens já baixadas) e os três
serviços aparecem como `(healthy)`.

```bash
docker compose exec postgres psql -U bank -d bank -c "select version();"
```

**Esperado**: retorna a versão do PostgreSQL 18; o banco `bank` existe.

Abrir `http://localhost:15672`, entrar com `bank` / `bank_dev_password`.
**Esperado**: a UI de gerenciamento abre e o vhost `bank` está listado.

## Cenário 2 — Saúde dos serviços (US2, FR-005, SC-003)

```bash
docker compose ps --format "table {{.Service}}\t{{.Status}}"
```

**Esperado**: 100% dos serviços com `(healthy)`.

```bash
docker compose exec rabbitmq rabbitmqctl stop_app
# repetir até o STATUS mudar (12 falhas × 5s ≈ 60–70s)
docker compose ps rabbitmq
docker compose exec rabbitmq rabbitmqctl start_app
```

**Esperado**: o RabbitMQ passa a `(unhealthy)` após o app parar e volta a `(healthy)`
depois do `start_app`.

## Cenário 3 — Telemetria (US3, FR-004, SC-004)

Os payloads usam o horário atual (`NOW`/`END`), para os itens caírem na janela de tempo
exibida pela interface. Cada `curl` deve responder `{}` (HTTP 200).

Enviar um trace de teste via OTLP/HTTP:

```bash
NOW=$(date +%s)000000000; END=$(( $(date +%s) + 1 ))000000000
curl -s -X POST http://localhost:4318/v1/traces \
  -H "Content-Type: application/json" \
  -d '{"resourceSpans":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"quickstart"}}]},"scopeSpans":[{"spans":[{"traceId":"5b8efff798038103d269b633813fc60c","spanId":"eee19b7ec3c1b174","name":"teste-quickstart","kind":1,"startTimeUnixNano":"'"$NOW"'","endTimeUnixNano":"'"$END"'"}]}]}]}'
```

**Esperado**: em até 10s, o trace `teste-quickstart` do serviço `quickstart` aparece em
`http://localhost:18888` → *Traces*.

Enviar um log correlacionado ao trace:

```bash
curl -s -X POST http://localhost:4318/v1/logs \
  -H "Content-Type: application/json" \
  -d '{"resourceLogs":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"quickstart"}}]},"scopeLogs":[{"logRecords":[{"timeUnixNano":"'"$NOW"'","severityNumber":9,"severityText":"Information","body":{"stringValue":"log de teste do quickstart"},"traceId":"5b8efff798038103d269b633813fc60c","spanId":"eee19b7ec3c1b174"}]}]}]}'
```

**Esperado**: o log aparece em *Structured logs* e, ao abri-lo, está vinculado ao trace
`teste-quickstart`.

Enviar uma métrica (contador):

```bash
curl -s -X POST http://localhost:4318/v1/metrics \
  -H "Content-Type: application/json" \
  -d '{"resourceMetrics":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"quickstart"}}]},"scopeMetrics":[{"scope":{"name":"quickstart"},"metrics":[{"name":"quickstart.requisicoes","unit":"1","sum":{"aggregationTemporality":2,"isMonotonic":true,"dataPoints":[{"asInt":"1","startTimeUnixNano":"'"$NOW"'","timeUnixNano":"'"$NOW"'"}]}}]}]}]}'
```

**Esperado**: a métrica `quickstart.requisicoes` aparece em *Metrics* para o recurso
`quickstart`.

A validação definitiva com uma aplicação real (traces, métricas e logs correlacionados)
acontece na spec da API (issue #3).

## Cenário 4 — Persistência e reset (US4, FR-007, FR-008, SC-005)

```bash
docker compose exec postgres psql -U bank -d bank -c "create table smoke(id int); insert into smoke values (1);"
curl -s -u bank:bank_dev_password -X PUT http://localhost:15672/api/queues/bank/smoke \
  -H "Content-Type: application/json" -d '{"durable":true}'
docker compose down
docker compose up -d --wait
docker compose exec postgres psql -U bank -d bank -c "select * from smoke;"
docker compose exec rabbitmq rabbitmqctl list_queues -p bank
```

**Esperado**: a tabela `smoke` (com 1 linha) e a fila `smoke` existem após o reinício.

```bash
docker compose down -v
docker compose up -d --wait
docker compose exec postgres psql -U bank -d bank -c "select * from smoke;"
```

**Esperado**: erro `relation "smoke" does not exist`; a fila `smoke` também não existe.

## Cenário 5 — Casos de borda

- **Sem `.env`**: apagar o `.env` e executar `docker compose up -d --wait` → sobe com os
  padrões.
- **Porta ocupada**: com outro processo na 5432, a subida falha citando o serviço
  `postgres` e a porta; definir `POSTGRES_PORT=55432` no `.env` resolve sem editar
  arquivos versionados.
- **Subida repetida**: executar `docker compose up -d --wait` com o ambiente no ar → nenhum
  contêiner é recriado e nenhum dado é perdido.
- **Dashboard fora do ar**: `docker compose stop aspire-dashboard` → PostgreSQL e RabbitMQ
  continuam `(healthy)`.
- **Imagem indisponível**: definir temporariamente, num arquivo de override local, uma tag
  inexistente para um serviço (por exemplo `postgres:0.0-inexistente`) e subir → a falha
  cita o serviço e a imagem que não puderam ser obtidos.
- **Onboarding (SC-001)**: a partir de um clone limpo, seguir apenas a seção "Ambiente
  local" do guia `docs/desenvolvimento.md` e cronometrar até todos os serviços estarem `(healthy)` (≤ 10 min, sem
  contar download de imagens).
