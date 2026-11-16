# Quickstart: Setup da API

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/api-base.md](contracts/api-base.md)

## Pré-requisitos

- SDK do .NET 10 (`dotnet --version` na raiz deve mostrar `10.0.4xx`, por causa do
  `global.json`).
- Ambiente local da spec 001 (Docker Desktop/Engine em execução).

## Cenário 1 — Build e testes (US5, FR-013, FR-014, FR-015)

```bash
dotnet build src/backend/Bank.slnx -c Release
dotnet test --solution src/backend/Bank.slnx -c Release
```

**Esperado**: build sem avisos (avisos são erros) e todos os testes unitários, de
integração e de arquitetura aprovados.

## Cenário 2 — API pelo SDK (US1, FR-001, FR-002)

```bash
docker compose up -d --wait aspire-dashboard
dotnet run --project src/backend/src/Bank.Api
curl -i http://localhost:5080/health
```

**Esperado**: `HTTP/1.1 200 OK` com corpo `Healthy`.

**Porta ocupada**: com outro processo na 5080, a inicialização falha citando o endereço
(`address already in use`); `dotnet run --project src/backend/src/Bank.Api --urls
http://localhost:5090` resolve sem editar arquivos versionados.

## Cenário 3 — Documentação interativa (US2, FR-003..005, SC-002)

- Abrir `http://localhost:5080/scalar/v1` → a UI lista `GET /health`; executar pela UI →
  `200 Healthy`.
- `curl -s http://localhost:5080/openapi/v1.json` → documento OpenAPI 3.x com `/health` e
  sem `/__test/*`.
- Produção:

  ```bash
  dotnet run --project src/backend/src/Bank.Api --no-launch-profile --environment Production --urls http://localhost:5081
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5081/scalar/v1        # 404
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5081/openapi/v1.json  # 404
  ```

## Cenário 4 — Erros padronizados (US4, FR-010, SC-004)

```bash
curl -s -i http://localhost:5080/nao-existe          # 404 application/problem+json com traceId
curl -s -i -X POST http://localhost:5080/health      # 405 application/problem+json com traceId
```

Os casos 500 (sem detalhes internos) e 400 (JSON malformado) são cobertos pelos testes de
integração no ambiente `Testing` (Cenário 1). Copiar o `traceId` de uma resposta e
procurá-lo em `http://localhost:18888` → *Traces* → o trace da requisição é encontrado.

## Cenário 5 — Telemetria (US3, FR-006..009, SC-003, SC-006)

```bash
for i in $(seq 1 5); do curl -s -o /dev/null http://localhost:5080/nao-existe; done
```

**Esperado** em `http://localhost:18888`, recurso `bank-api`, em até 10s:
- *Traces*: spans `GET` com rota e status 404;
- *Structured logs*: logs da aplicação vinculados aos traces;
- *Metrics*: `http.server.request.duration` e métricas `dotnet.*` do runtime.
- Nenhum trace para `/health`, `/openapi` ou `/scalar`.

Coletor fora do ar:

```bash
docker compose stop aspire-dashboard
for i in $(seq 1 20); do curl -s -o /dev/null -w "%{http_code} " http://localhost:5080/health; done
docker compose start aspire-dashboard
```

**Esperado**: vinte respostas `200` (SC-006).

## Cenário 6 — API no Compose (US1, FR-012)

```bash
docker compose up -d --wait --build
docker compose ps
curl -s http://localhost:8080/health
```

**Esperado**: `api` só inicia depois de `postgres` e `rabbitmq` ficarem `(healthy)` e de o
`aspire-dashboard` ter iniciado (o coletor não bloqueia a API); `api` também fica
`(healthy)`; com `API_PORT=8085` no `.env`, a API passa a responder na 8085; `/health` responde `Healthy` e
`http://localhost:8080/scalar/v1` abre a documentação; a telemetria do serviço `bank-api`
aparece no dashboard.

## Cenário 7 — Violação de camada (US5, SC-005)

Em uma branch descartável, fazer um tipo de `Bank.Api.Endpoints` usar um tipo de
`Bank.Infrastructure` (a API referencia a infraestrutura apenas para a composição, então
compila) e executar `dotnet test --solution src/backend/Bank.slnx`. Referências proibidas entre
projetos de camada (por exemplo, Domain → Infrastructure) já são impedidas pelo próprio
build, pois criariam referência circular.

**Esperado**: o teste de arquitetura correspondente falha indicando o tipo infrator; o CI
do PR também falha. Descartar a branch.

## Cenário 8 — Onboarding (SC-001)

A partir de um clone limpo, seguir apenas a seção "Backend (API)" do README e cronometrar
até `GET /health` responder `Healthy`.

**Esperado**: ≤ 10 minutos, sem contar downloads (SDK, pacotes NuGet e imagens).
