# Guia de desenvolvimento

Como executar, testar e evoluir o projeto Bank. As regras obrigatórias do projeto estão na
[constituição](../.specify/memory/constitution.md).

## Ambiente local

Infraestrutura de apoio ao desenvolvimento, definida em
[`specs/001-infra-local`](../specs/001-infra-local/spec.md).

**Pré-requisito:** Docker Desktop (Windows/macOS) ou Docker Engine (Linux) em execução,
com Docker Compose v2.

```bash
# (opcional) personalizar portas e credenciais — sem .env, valem os mesmos padrões
cp .env.example .env

# subir e aguardar até todos os serviços estarem saudáveis
docker compose up -d --wait

# ver o estado (todos devem estar "(healthy)")
docker compose ps

# parar mantendo os dados
docker compose down

# reset: parar e apagar todos os dados
docker compose down -v
```

| Serviço | Endereço | Credenciais padrão |
| --- | --- | --- |
| PostgreSQL | `localhost:5432`, banco `bank` | `bank` / `bank_dev_password` |
| RabbitMQ (AMQP) | `localhost:5672`, vhost `bank` | `bank` / `bank_dev_password` |
| RabbitMQ Management | <http://localhost:15672> | `bank` / `bank_dev_password` |
| Aspire Dashboard (traces, métricas e logs) | <http://localhost:18888> | sem login |
| OTLP gRPC / HTTP | `http://localhost:4317` / `http://localhost:4318` | sem autenticação |
| API (`/health`, `/scalar/v1`) | <http://localhost:8080> | sem autenticação |

- O `docker compose up -d --wait` também constrói e sobe a API (use `--build` após alterar
  o código). Para subir só a infraestrutura e rodar a API pelo SDK:
  `docker compose up -d --wait postgres rabbitmq aspire-dashboard`.
- As portas escutam apenas em `127.0.0.1` e podem ser alteradas no `.env`, por exemplo
  `POSTGRES_PORT=55432` se a 5432 já estiver ocupada.
- As credenciais são exclusivamente para desenvolvimento local.
- A telemetria do Aspire Dashboard fica só em memória e é perdida ao reiniciar o contêiner.
- Strings de conexão, endpoints e garantias do ambiente:
  [contrato do ambiente local](../specs/001-infra-local/contracts/ambiente-local.md).
- Roteiro completo de validação: [quickstart](../specs/001-infra-local/quickstart.md).

## Backend (API)

Solução .NET 10 em `src/backend/Bank.slnx`, definida em
[`specs/002-setup-api`](../specs/002-setup-api/spec.md): camadas `Bank.Api`,
`Bank.Application`, `Bank.Domain` e `Bank.Infrastructure`, além dos projetos de teste
unitário, de integração e de arquitetura.

**Pré-requisito:** SDK do .NET 10 (`dotnet --version` na raiz deve mostrar `10.0.4xx`; o
`global.json` fixa a versão).

```bash
# compilar e executar todos os testes
dotnet build src/backend/Bank.slnx
dotnet test --solution src/backend/Bank.slnx

# rodar a API pelo SDK (com o ambiente local no ar para receber a telemetria)
docker compose up -d --wait postgres rabbitmq aspire-dashboard
dotnet run --project src/backend/src/Bank.Api
```

| Recurso | SDK (`dotnet run`) | Compose |
| --- | --- | --- |
| Saúde | <http://localhost:5080/health> | <http://localhost:8080/health> |
| Documentação interativa (Scalar) | <http://localhost:5080/scalar/v1> | <http://localhost:8080/scalar/v1> |
| Documento OpenAPI | <http://localhost:5080/openapi/v1.json> | <http://localhost:8080/openapi/v1.json> |

- A documentação (Scalar e OpenAPI) só existe fora do ambiente `Production`.
- Toda resposta de erro usa `ProblemDetails` com `traceId`, que pode ser buscado no
  Aspire Dashboard (<http://localhost:18888>), onde a API aparece como `bank-api`.
- Outra porta pelo SDK: `dotnet run --project src/backend/src/Bank.Api --urls http://localhost:5090`.
- Garantias de erro, endpoints e telemetria:
  [contrato da API base](../specs/002-setup-api/contracts/api-base.md).

## Fluxo de desenvolvimento (SDD)

1. Crie a branch da spec a partir de `main` atualizada, com o mesmo nome da pasta da
   spec: `NNN-nome-curto` (ex.: `002-setup-api`).
2. Especifique: `/speckit-specify` (e `/speckit-clarify` se houver ambiguidades).
3. Planeje: `/speckit-plan` — o plano passa pelo *Constitution Check*.
4. Gere as tarefas: `/speckit-tasks` (opcional: `/speckit-analyze` para checar
   consistência entre spec, plano e tarefas).
5. Implemente: `/speckit-implement`.
6. Abra o PR para `main` referenciando a issue (`Closes #N`). O merge é feito
   por merge commit (sem squash), preservando o histórico da branch.

Mudanças sem spec (emendas da constituição, documentação, CI, manutenção) usam a
branch `<tipo>/<issue>-nome-curto`, por exemplo `docs/1-constituicao`.

### Commits

[Conventional Commits](https://www.conventionalcommits.org/):
`tipo(escopo): descrição`, com `tipo` em `feat`, `fix`, `docs`, `test`, `refactor`,
`build`, `ci` ou `chore`.
