# Bank

Sistema bancário desenvolvido com **Spec-Driven Development (SDD)** usando o
[Spec Kit](https://github.com/github/spec-kit): cada funcionalidade nasce como uma
especificação aprovada em `specs/` e só então é planejada, quebrada em tarefas e
implementada.

> **Status:** fundação do projeto (milestone *Fundação, Arquitetura e Contratos —
> SDD Core*). Instruções de execução serão adicionadas conforme as issues de
> infraestrutura (#2) e de setup da API (#3) forem concluídas.

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
├── .specify/              # Spec Kit: constituição, templates e scripts
└── .claude/               # comandos do Spec Kit para o Claude Code
```

## Fluxo de desenvolvimento (SDD)

1. Crie a branch da spec a partir de `main` atualizada, com o mesmo nome da pasta da
   spec: `NNN-nome-curto` (ex.: `003-setup-api`).
2. Especifique: `/speckit-specify` (e `/speckit-clarify` se houver ambiguidades).
3. Planeje: `/speckit-plan` — o plano passa pelo *Constitution Check*.
4. Gere as tarefas: `/speckit-tasks` (opcional: `/speckit-analyze` para checar
   consistência entre spec, plano e tarefas).
5. Implemente: `/speckit-implement`.
6. Abra o PR para `main` referenciando a issue (`Closes #N`). O merge é por squash.

Mudanças sem spec (emendas da constituição, documentação, CI, manutenção) usam a
branch `<tipo>/<issue>-nome-curto`, por exemplo `docs/1-constituicao`.

### Commits

[Conventional Commits](https://www.conventionalcommits.org/):
`tipo(escopo): descrição`, com `tipo` em `feat`, `fix`, `docs`, `test`, `refactor`,
`build`, `ci` ou `chore`.
