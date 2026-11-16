# Bank

Sistema bancário desenvolvido com **Spec-Driven Development (SDD)** usando o
[Spec Kit](https://github.com/github/spec-kit): cada funcionalidade nasce como uma
especificação aprovada em `specs/` e só então é planejada, quebrada em tarefas e
implementada.

> **Status:** fundação do projeto (milestone *Fundação, Arquitetura e Contratos —
> SDD Core*). O ambiente local e a API base já estão disponíveis; ainda não há
> funcionalidades de negócio.

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
├── docs/                  # documentação do projeto (guias e decisões)
├── specs/                 # documentação viva: uma pasta por spec (NNN-nome-curto/)
├── src/
│   ├── backend/           # solução .NET 10 (Api, Application, Domain, Infrastructure, testes)
│   └── frontend/          # aplicação React
├── docker/                # imagens auxiliares do ambiente local
├── docker-compose.yml     # ambiente local (PostgreSQL, RabbitMQ, Aspire Dashboard, API)
├── global.json            # versão do SDK do .NET e runner de testes
├── .env.example           # configuração padrão do ambiente local
├── .specify/              # Spec Kit: constituição, templates e scripts
└── .claude/               # comandos do Spec Kit para o Claude Code
```

## Documentação

- [Guia de desenvolvimento](docs/desenvolvimento.md): ambiente local, backend (API) e
  fluxo de desenvolvimento SDD.
