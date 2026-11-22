# Implementation Plan: Mensageria RabbitMQ (topologia, contratos, DLQ e rastreamento)

**Branch**: `010-mensageria-rabbitmq` | **Date**: 2026-11-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/010-mensageria-rabbitmq/spec.md` (issue #20)

## Summary

Tira do outbox os eventos `transferencia.concluida` que a spec 006 já grava e os leva até um
consumidor de notificações, com o cliente nativo `RabbitMQ.Client` 7. Um processo novo,
`Bank.Worker`, hospeda o despachante do outbox e o consumidor; a API continua sem falar com o
broker. O despachante reserva pendentes com `FOR UPDATE SKIP LOCKED`, publica com `mandatory` e
publisher confirms e só então marca `publicado_em`. O consumidor grava `eventos_processados` na
mesma transação do efeito (log estruturado da notificação) e dá ACK após o commit. Falhas vão para
filas de espera com TTL de 1 s, 2 s e 4 s e, na 4ª execução, para a DLQ com headers de
diagnóstico, no modelo do Order-Management-System. O contexto W3C da requisição é guardado no
outbox na gravação, para que o span Producer e o Consumer fiquem no mesmo trace do `POST`, mesmo
com a publicação atrasada. O evento v1 ganha nomes, descrição, `tipoEvento` e `versao` (aditivo).

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: as das specs 002–009, mais `RabbitMQ.Client` 7.2.2 e `OpenTelemetry.Api`
1.21.0 (Infrastructure) e `Testcontainers.RabbitMq` 4.16.0 (testes) — justificativas em R2

**Storage**: PostgreSQL 18 — tabela nova `eventos_processados`; colunas `trace_parent` e
`trace_state` em `outbox_eventos` (migration `AdicionarMensageria`)

**Testing**: xUnit v3; `WebApplicationFactory` da API e do Worker; Testcontainers (PostgreSQL e
RabbitMQ); exportadores OpenTelemetry em memória; ArchUnitNET

**Target Platform**: contêineres Linux (Compose: novo serviço `worker`) e SDK local (Windows e
runner Linux do CI)

**Project Type**: web-service + worker (backend apenas; sem mudança no frontend)

**Performance Goals**: notificação em até 5 s após a transferência (SC-001; despacho a cada 1 s);
nenhum impacto no p95 < 1 s da transferência (SC-007; a API não toca o broker)

**Constraints**: nenhum evento perdido (outbox + confirms); nenhuma notificação duplicada
(idempotência transacional); nenhuma mensagem em loop (4 execuções no máximo por reprocessamento);
nenhum dado pessoal em telemetria; contrato do evento só aditivo

**Scale/Scope**: 1 projeto novo (`Bank.Worker`), 2 serviços de fundo, 1 tabela, 2 colunas, 7 filas,
2 exchanges, 1 caso de uso novo (Application), 3 contratos, 1 serviço novo no Compose

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 6 clarificações; issue #20; branch `010-mensageria-rabbitmq` = pasta | ✅ |
| II. Endpoints só transporte; dependências | API sem mudança de endpoint; Worker é raiz de composição como a API; regra da notificação em Application (`NotificarTransferenciaConcluida`); broker e EF na Infrastructure; teste de arquitetura inclui o Worker e proíbe `RabbitMQ.Client`/`OpenTelemetry` no Domain e na Application (R8, R12) | ✅ |
| III. ProblemDetails | Sem resposta HTTP nova (o `/health` do Worker não tem erro de negócio) | ✅ |
| IV. Atomicidade | Transferência + evento continuam num único commit; publicação nunca desfaz a operação (RN-01) | ✅ |
| IV. Eventos consistentes com a transação (outbox) | Outbox existente + despachante com confirms; `publicado_em` só após confirmação (R4) | ✅ |
| IV. Idempotência | Consumidor: `eventos_processados` na transação do efeito, `ON CONFLICT DO NOTHING` (R7) | ✅ |
| IV. Concorrência definida no plano | Despachante: `FOR UPDATE SKIP LOCKED`; consumidor: conflito da PK serializa entregas simultâneas (R4, R7) | ✅ |
| IV. Lançamentos imutáveis | Nenhuma escrita em lançamentos; no outbox só `publicado_em` muda (como já previsto na spec 006, R9) | ✅ |
| V. Contratos versionados; eventos no passado, com versão | `transferencia.concluida` v1 aditivo, header `versao`; `cobranca.paga` v1; topologia versionada 1.0.0; contratos em `contracts/` e histórico do contrato da 006 atualizado | ✅ |
| VI. OpenTelemetry, logs estruturados, sem dados sensíveis | Worker exporta OTLP (`bank-worker`); spans Producer/Consumer com W3C; métricas da FR-022; logs com `EventId` e sem nome, descrição, payload ou credencial (R10) | ✅ |
| VII. Unitários, integração com contêineres, contratos de mensageria | Testcontainers RabbitMQ + PostgreSQL; testes de serialização e conformidade dos contratos (R12) | ✅ |
| Restrições: RabbitMQ.Client nativo, sem MassTransit/Wolverine | `RabbitMQ.Client` 7.2.2 direto | ✅ |
| Restrições: schema por migrations | `AdicionarMensageria` | ✅ |
| Restrições: SQL puro em `.sql` | `ReservarEventosPendentes.sql`, `RegistrarEventoProcessado.sql` em `Sql/Consultas/` | ✅ |
| Restrições: identificadores tipados | `EventoProcessado.EventoId` tipado; contratos em `Guid`; o leitor converte para os tipos do Domain | ✅ |
| Restrições: dependência nova justificada | R2 | ✅ |
| Restrições: Compose com healthcheck | Serviço `worker` com `wget /health/pronto` | ✅ |
| Restrições: segredos fora do repositório | Credenciais do broker por variável de ambiente (padrões só de desenvolvimento, como as atuais) | ✅ |
| Convenções: português | `DespachanteOutbox`, `ConsumidorNotificacoesTransferencia`, `EventoProcessado`, `TopologiaMensageria` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado; dois itens em Complexity
Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/010-mensageria-rabbitmq/
├── plan.md
├── research.md                         # R1–R12
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── topologia-e-mensagens.md        # exchanges, filas, headers, retry, DLQ, telemetria (1.0.0)
│   ├── evento-transferencia-concluida.md  # v1 revisada (aditiva)
│   └── evento-cobranca-paga.md         # v1, preparação Fase 4
├── checklists/requirements.md
└── tasks.md                            # /speckit-tasks
```

### Source Code (repository root)

```text
docker-compose.yml                       # + serviço worker (target worker, depende de api/rabbitmq)
.env.example                             # (sem variável nova: reusa RABBITMQ_*)
specs/001-infra-local/contracts/ambiente-local.md   # + serviço worker
specs/006-transferencias/contracts/evento-transferencia-concluida.md  # histórico → spec 010

src/backend/
├── Bank.slnx                            # + Bank.Worker
├── Directory.Packages.props             # + RabbitMQ.Client, OpenTelemetry.Api, Testcontainers.RabbitMq
├── Dockerfile                           # stages api e worker
├── src/
│   ├── Bank.Domain/Transferencias/Transferencia.cs        # evento com nomes e descrição
│   ├── Bank.Application/
│   │   ├── Transferencias/RealizarTransferencia.cs        # passa os nomes ao Executar
│   │   ├── Abstracoes/INotificadorTransferencia.cs        # efeito do consumo (R8)
│   │   ├── Notificacoes/NotificarTransferenciaConcluida.cs  # caso de uso (log + métrica)
│   │   ├── Observabilidade/MetricasNotificacoes.cs
│   │   └── DependencyInjection.cs
│   ├── Bank.Infrastructure/
│   │   ├── DependencyInjection.cs       # AddPersistencia (compartilhado) + AddMensageria (Worker)
│   │   ├── Mensageria/
│   │   │   ├── EventoOutbox.cs          # payload v1 revisado + trace_parent/trace_state
│   │   │   ├── EventoProcessado.cs
│   │   │   ├── MensageriaOptions.cs     # + validador
│   │   │   ├── ConexaoRabbitMq.cs       # conexão única, sem recuperação automática (R3)
│   │   │   ├── TopologiaMensageria.cs   # declaração idempotente (R5)
│   │   │   ├── DespachanteOutbox.cs     # BackgroundService (R4)
│   │   │   ├── PublicadorConfirmado.cs  # publicação com confirms → resultado tipado (R4)
│   │   │   ├── SessaoMensageria.cs      # laço de sessões com backoff (R3)
│   │   │   ├── ConsumidorEventos.cs     # base: sessão, leitura, idempotência, espera, DLQ (R7)
│   │   │   ├── PoliticaNovaTentativa.cs # contagem e headers (puro, testável)
│   │   │   ├── ConsumidorNotificacoesTransferencia.cs  # leitor v1 → caso de uso
│   │   │   ├── CabecalhosMensagem.cs    # propriedades, headers e propagação W3C (R6)
│   │   │   └── TelemetriaMensageria.cs  # ActivitySource + Meter Bank.Mensageria (R10)
│   │   └── Persistencia/
│   │       ├── Configuracoes/(EventoOutboxConfiguracao +colunas, EventoProcessadoConfiguracao)
│   │       ├── Sql/Consultas/(ReservarEventosPendentes.sql, RegistrarEventoProcessado.sql)
│   │       └── Migrations/*_AdicionarMensageria.cs
│   ├── Bank.Api/Program.cs              # AddPersistencia + serviços de inicialização (sem broker)
│   └── Bank.Worker/                     # novo (SDK Web)
│       ├── Bank.Worker.csproj
│       ├── Program.cs                   # composição: Application, persistência, mensageria
│       ├── Observability/TelemetriaWorker.cs   # serviço bank-worker, OTLP
│       ├── Saude/SaudeEndpoints.cs      # /health e /health/pronto
│       ├── WorkerAssemblyMarker.cs      # para o WebApplicationFactory dos testes
│       └── appsettings.json             # Mensageria (padrões)
└── tests/
    ├── Bank.UnitTests/
    │   ├── Domain/TransferenciaTests.cs (+evento)
    │   ├── Application/NotificarTransferenciaConcluidaTests.cs
    │   └── Infrastructure/(EventoOutboxTests, PoliticaNovaTentativaTests,
    │                       LeitorTransferenciaConcluidaTests, CabecalhosMensagemTests,
    │                       MensageriaOptionsTests, ContratoEventosTests)
    ├── Bank.IntegrationTests/
    │   ├── RabbitMqCompartilhado.cs     # contêiner RabbitMQ (como PostgresCompartilhado)
    │   ├── BankWorkerFactory.cs
    │   ├── TopologiaTests.cs            # FR-006
    │   ├── PublicacaoOutboxTests.cs     # US1
    │   ├── BrokerIndisponivelTests.cs   # US2 (contêiner RabbitMQ exclusivo)
    │   ├── ConsumoIdempotenteTests.cs   # US3
    │   ├── NovaTentativaEDlqTests.cs    # US4 (+ rede de segurança da DLQ)
    │   ├── RastreamentoMensageriaTests.cs  # US5
    │   └── DadosSensiveisMensageriaTests.cs  # FR-023
    └── Bank.ArchitectureTests/LayerDependencyTests.cs   # + Worker, + RabbitMQ/OpenTelemetry
```

**Structure Decision**: o Worker segue a forma da API (raiz de composição fina, regras na
Application, adaptadores na Infrastructure). Tudo o que fala com o broker fica em
`Infrastructure/Mensageria`, como o outbox já está. Os testes de integração do Worker ficam no
projeto de integração existente, que já sobe o PostgreSQL.

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| Recuperação de conexão própria (sessões com backoff) em vez da automática do cliente | Comportamento observável e testável na queda do broker (US2, FR-003); evita o defeito visto no OMS (consumidores sem inscrição até reiniciar) | A recuperação automática tem estado intermediário que confunde o descarte de conexões e não se combina bem com confirms rastreados |
| Projeto e contêiner novos (`Bank.Worker`) | Clarification Q3; mantém a API sem dependência do broker | Hosted service na API: producer e consumer no mesmo serviço e a API passa a depender do broker |
