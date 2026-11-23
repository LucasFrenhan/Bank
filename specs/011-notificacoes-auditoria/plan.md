# Implementation Plan: Notificações de transferência persistidas (auditoria)

**Branch**: `011-notificacoes-auditoria` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/011-notificacoes-auditoria/spec.md` (issue #21,
complementar à #20 / spec 010)

## Summary

O consumidor de notificações de transferência do `Bank.Worker` (spec 010) passa a gravar, além do
log e da métrica, duas linhas em `notificacoes_auditoria` por evento `transferencia.concluida`:
comprovante de envio para o pagador e aviso de crédito para o recebedor. A notificação vira
entidade de domínio (`NotificacaoAuditoria`, id tipado, fábricas que montam o texto com valor em
formato brasileiro e o nome da outra parte quando o evento traz). O caso de uso
`NotificarTransferenciaConcluida` grava pela abstração nova `INotificacoesAuditoria`, usando o
`BankDbContext` do escopo do consumidor, de modo que a gravação entra na transação já aberta para
`eventos_processados` e o ACK continua depois do commit. Idempotência em três camadas
(`eventos_processados`, checagem no caso de uso, índice único `(evento_id, tipo)`). O consumidor
base, a topologia, o contrato v1 e a política de novas tentativas/DLQ não mudam.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: as das specs 002–010; **nenhuma dependência nova**

**Storage**: PostgreSQL 18, tabela nova `notificacoes_auditoria` (migration
`AdicionarNotificacoesAuditoria`)

**Testing**: xUnit v3; `WebApplicationFactory` da API e do Worker; Testcontainers (PostgreSQL e
RabbitMQ); ArchUnitNET (testes existentes)

**Target Platform**: contêineres Linux (Compose, sem serviço novo) e SDK local (Windows e runner
Linux do CI); `InvariantGlobalization=true` (R5)

**Project Type**: web-service + worker (backend apenas; sem mudança no frontend nem na API HTTP)

**Performance Goals**: notificações gravadas em até 5 s após a transferência (SC-002; mesma meta da
010); custo por evento: 1 consulta por índice + 1 `INSERT` de 2 linhas na transação existente

**Constraints**: atomicidade com `eventos_processados`; no máximo 1 notificação por
`(evento, tipo)`; registros imutáveis; nomes e texto fora de logs, spans e métricas; contrato do
evento sem mudança

**Scale/Scope**: 1 tabela, 1 entidade de domínio (+ id e 2 enums), 1 abstração, 1 repositório, 1
migration, ajustes em 1 caso de uso, 1 comando e 1 leitor; 1 contrato documental

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 4 clarificações; issue #21; branch `011-notificacoes-auditoria` = pasta; spec 010 atualizada (FR-011) | ✅ |
| II. Endpoints só transporte; dependências | Nenhum endpoint novo; texto e destinatários no Domain (`NotificacaoAuditoria`), orquestração na Application, EF na Infrastructure (`NotificacoesAuditoriaRepositorio`) por `INotificacoesAuditoria` (R1, R2) | ✅ |
| III. ProblemDetails | Sem resposta HTTP nova | ✅ |
| IV. Atomicidade | Notificações e `eventos_processados` na mesma transação; ACK após o commit (R2) | ✅ |
| IV. Idempotência | `eventos_processados` + checagem no caso de uso + índice único `(evento_id, tipo)` (R3) | ✅ |
| IV. Concorrência definida no plano | Entregas simultâneas continuam serializadas pelo conflito da PK de `eventos_processados` (010, R7); o índice único é a última barreira | ✅ |
| IV. Registros imutáveis | Notificações só com `INSERT`; entidade sem métodos de alteração; FK `RESTRICT` (FR-007) | ✅ |
| V. Contratos versionados | `transferencia.concluida` v1 sem mudança (os nomes já estão no contrato); registro de notificações documentado em `contracts/notificacoes-auditoria.md` 1.0.0 | ✅ |
| VI. Observabilidade, sem dados sensíveis | Log 5100 e `bank.notificacoes.enviadas` mantidos, sem nomes/texto (R6); teste de dados sensíveis ampliado | ✅ |
| VII. Unitários, integração com contêineres | Domain, caso de uso (incl. idempotência) e leitor; integração ponta a ponta, duplicatas, rollback, DLQ, integridade (R8) | ✅ |
| Restrições: schema por migrations | `AdicionarNotificacoesAuditoria` | ✅ |
| Restrições: SQL puro em `.sql` | Nenhum SQL puro novo (EF/LINQ); `CHECK` declarado no mapeamento (fora do escopo da regra) | ✅ |
| Restrições: identificadores tipados | `NotificacaoAuditoriaId`; `EventoId`/`UsuarioId` na entidade e na abstração; conversor na Infrastructure | ✅ |
| Restrições: dependência nova justificada | Nenhuma | ✅ |
| Convenções: português | `NotificacaoAuditoria`, `TipoNotificacao`, `CanalNotificacao`, `INotificacoesAuditoria`, `notificacoes_auditoria` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado, sem itens em Complexity
Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/011-notificacoes-auditoria/
├── plan.md
├── research.md                        # R1–R8
├── data-model.md
├── quickstart.md
├── contracts/
│   └── notificacoes-auditoria.md      # registro das notificações (1.0.0)
├── checklists/requirements.md
└── tasks.md                           # /speckit-tasks
```

### Source Code (repository root)

```text
specs/010-mensageria-rabbitmq/(spec.md, data-model.md)   # notificação agora persistida → spec 011 (FR-011)

src/backend/
├── src/
│   ├── Bank.Domain/Notificacoes/                  # novo
│   │   ├── NotificacaoAuditoria.cs                # entidade + fábricas + texto (R1, R5)
│   │   ├── NotificacaoAuditoriaId.cs
│   │   ├── TipoNotificacao.cs
│   │   └── CanalNotificacao.cs
│   ├── Bank.Application/
│   │   ├── Abstracoes/INotificadorTransferencia.cs   # NotificacaoTransferencia + nomes (R4)
│   │   ├── Abstracoes/INotificacoesAuditoria.cs      # novo (R2)
│   │   └── Notificacoes/NotificarTransferenciaConcluida.cs  # grava, idempotente, log/métrica (R3, R6)
│   ├── Bank.Infrastructure/
│   │   ├── DependencyInjection.cs                 # AddPersistencia + repositório
│   │   ├── Mensageria/LeitorTransferenciaConcluida.cs   # lê pagadorNome/recebedorNome (R4)
│   │   └── Persistencia/
│   │       ├── BankDbContext.cs                   # DbSet + conversor do id
│   │       ├── NotificacoesAuditoriaRepositorio.cs   # novo
│   │       ├── Configuracoes/NotificacaoAuditoriaConfiguracao.cs   # novo (R7)
│   │       └── Migrations/*_AdicionarNotificacoesAuditoria.cs
│   └── Bank.Worker/                               # sem mudança (já registra AddNotificacoes e AddPersistencia)
└── tests/
    ├── Bank.UnitTests/
    │   ├── Domain/NotificacaoAuditoriaTests.cs    # novo
    │   ├── Application/NotificarTransferenciaConcluidaTests.cs   # ajustado
    │   └── Infrastructure/LeitorTransferenciaConcluidaTests.cs   # ajustado
    └── Bank.IntegrationTests/
        ├── UsuariosDeTeste.cs                     # novo: par de usuários reais para os payloads (FK)
        ├── PublicacaoOutboxTests.cs               # + linhas em notificacoes_auditoria
        ├── ConsumoIdempotenteTests.cs             # + contagem de linhas; Payload com usuários reais
        ├── NovaTentativaEDlqTests.cs              # Payload com usuários reais
        ├── RastreamentoMensageriaTests.cs         # Payload com usuários reais
        ├── DadosSensiveisMensageriaTests.cs       # + nomes/texto fora da telemetria
        └── NotificacoesAuditoriaTests.cs          # novo: rollback, DLQ, reprocessamento, integridade
```

**Structure Decision**: a mudança fica no efeito do consumo, que a spec 010 isolou em
`INotificadorTransferencia` justamente para isso. A entidade nova segue o padrão do Domain
(pasta por conceito, id tipado), a persistência segue o padrão dos repositórios existentes
(`UsuarioRepositorio`, `TransferenciaRepositorio`) e os testes ficam nos projetos atuais.

## Complexity Tracking

Sem violações a justificar.
