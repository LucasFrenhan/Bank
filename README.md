# Bank

Sistema bancário desenvolvido com **Spec-Driven Development (SDD)** usando o
[Spec Kit](https://github.com/github/spec-kit): cada funcionalidade nasce como uma
especificação aprovada em `specs/` e só então é planejada, quebrada em tarefas e
implementada.

> **Status:** fundação do projeto (milestone *Fundação, Arquitetura e Contratos —
> SDD Core*). O [ambiente local](#ambiente-local) já está disponível; as instruções da
> API serão adicionadas com a issue de setup da API (#3).

## Constituição

As regras obrigatórias do projeto — arquitetura, padrão de erros, integridade
financeira, testes e branching — estão na constituição:
[`.specify/memory/constitution.md`](.specify/memory/constitution.md)
(ponto de entrada: [`CONSTITUTION.md`](CONSTITUTION.md)).

Todo pull request é revisado quanto à conformidade com ela.

## Stack

| Camada | Tecnologia |
| --- | --- |
| Backend | .NET 10, C#, Minimal APIs |
| Persistência | PostgreSQL + Entity Framework Core (Npgsql) |
| Autenticação | ASP.NET Core Identity + JWT Bearer (claims e policies) |
| Mensageria | RabbitMQ (`RabbitMQ.Client` nativo) |
| Documentação da API | OpenAPI nativo do .NET + Scalar (`/scalar/v1`) |
| Observabilidade | OpenTelemetry (traces, métricas e logs) via OTLP |
| Frontend | React + TypeScript (desktop e mobile) |
| Ambiente local | Docker Compose |
| Testes | Testes unitários, de integração (Testcontainers) e de arquitetura |

## Estrutura do repositório

```text
/
├── CONSTITUTION.md        # ponteiro para a constituição
├── specs/                 # documentação viva: uma pasta por spec (NNN-nome-curto/)
├── src/
│   ├── backend/           # solução .NET 10 (Api, Application, Domain, Infrastructure, testes)
│   └── frontend/          # aplicação React
├── docker/                # imagens auxiliares do ambiente local
├── docker-compose.yml     # ambiente local (PostgreSQL, RabbitMQ, Aspire Dashboard)
├── .env.example           # configuração padrão do ambiente local
├── .specify/              # Spec Kit: constituição, templates e scripts
└── .claude/               # comandos do Spec Kit para o Claude Code
```

## Ambiente local

Infraestrutura de apoio ao desenvolvimento, definida em
[`specs/001-infra-local`](specs/001-infra-local/spec.md).

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

- As portas escutam apenas em `127.0.0.1` e podem ser alteradas no `.env`, por exemplo
  `POSTGRES_PORT=55432` se a 5432 já estiver ocupada.
- As credenciais são exclusivamente para desenvolvimento local.
- A telemetria do Aspire Dashboard fica só em memória e é perdida ao reiniciar o contêiner.
- Strings de conexão, endpoints e garantias do ambiente:
  [contrato do ambiente local](specs/001-infra-local/contracts/ambiente-local.md).
- Roteiro completo de validação: [quickstart](specs/001-infra-local/quickstart.md).

## Fluxo de desenvolvimento (SDD)

1. Crie a branch da spec a partir de `main` atualizada, com o mesmo nome da pasta da
   spec: `NNN-nome-curto` (ex.: `003-setup-api`).
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
