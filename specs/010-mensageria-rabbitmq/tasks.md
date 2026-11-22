---

description: "Lista de tarefas da feature 010-mensageria-rabbitmq"
---

# Tasks: Mensageria RabbitMQ (topologia, contratos, DLQ e rastreamento)

**Input**: Design documents from `specs/010-mensageria-rabbitmq/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/topologia-e-mensagens.md](contracts/topologia-e-mensagens.md),
[contracts/evento-transferencia-concluida.md](contracts/evento-transferencia-concluida.md),
[contracts/evento-cobranca-paga.md](contracts/evento-cobranca-paga.md), [quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII; FR-025): unitários, contrato de mensageria e
integração contra PostgreSQL e RabbitMQ em contêiner. Em cada história, os testes são escritos
primeiro e devem falhar antes da implementação.

**Organization**: setup e fundação comuns (Worker, conexão, topologia, migration); depois US1
(transferência → notificação), US2 (broker fora do ar), US3 (duplicatas), US4 (novas tentativas e
DLQ) e US5 (trace ponta a ponta); por fim, transversais.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (abreviado `back/`): `back/src/Bank.*` e `back/tests/Bank.*`; comandos
`dotnet` rodam em `src/backend/`. Mensageria em `back/src/Bank.Infrastructure/Mensageria/`
(abreviado `msg/`). Arquivos de raiz usam o caminho completo.

---

## Phase 1: Setup

**Purpose**: dependências, projeto `Bank.Worker` e ambiente local

- [X] T001 Em `back/Directory.Packages.props`, acrescentar `RabbitMQ.Client` 7.2.2 e
  `OpenTelemetry.Api` 1.21.0 (grupo "Infrastructure") e `Testcontainers.RabbitMq` 4.16.0 (grupo
  "Testes"), atualizando o comentário de justificativas para citar
  `specs/010-mensageria-rabbitmq/research.md (R2)`; referenciar `RabbitMQ.Client` e
  `OpenTelemetry.Api` em `back/src/Bank.Infrastructure/Bank.Infrastructure.csproj` e
  `Testcontainers.RabbitMq` em `back/tests/Bank.IntegrationTests/Bank.IntegrationTests.csproj`
- [X] T002 Criar `back/src/Bank.Worker/Bank.Worker.csproj` (SDK `Microsoft.NET.Sdk.Web`, referências a
  `Bank.Application` e `Bank.Infrastructure`, pacotes `OpenTelemetry.Extensions.Hosting`,
  `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Instrumentation.AspNetCore`),
  `back/src/Bank.Worker/WorkerAssemblyMarker.cs` (classe pública vazia, para o
  `WebApplicationFactory` dos testes — research R12), `Program.cs` mínimo (host web com
  `/health` respondendo 200) e `Properties/launchSettings.json` com perfil `http` na porta 5081
  (não colide com a API no `dotnet run`); incluir o projeto em `back/Bank.slnx` (pasta `/src/`);
  adicionar `<ProjectReference>` ao Worker em `back/tests/Bank.IntegrationTests/Bank.IntegrationTests.csproj`
  e `back/tests/Bank.ArchitectureTests/Bank.ArchitectureTests.csproj`, e
  `<InternalsVisibleTo Include="Bank.IntegrationTests" />` em
  `back/src/Bank.Infrastructure/Bank.Infrastructure.csproj` (os tipos de `Mensageria/` são
  `internal`; os testes de integração usam `TopologiaMensageria` e as constantes de headers)
- [X] T003 [P] Em `back/Dockerfile`, transformar o estágio final em dois alvos: `api` (atual) e
  `worker` (publica `src/Bank.Worker/Bank.Worker.csproj`, mesma base `aspnet:10.0.15-alpine3.24`,
  usuário `app`, `ASPNETCORE_HTTP_PORTS=8080`, `ENTRYPOINT ["dotnet", "Bank.Worker.dll"]`); copiar
  o `.csproj` do Worker na camada de restore
- [X] T004 [P] Em `docker-compose.yml`: no serviço `api`, `build: { context: ./src/backend, target: api }`;
  novo serviço `worker` (`build: { context: ./src/backend, target: worker }`, `image: bank/worker:local`,
  `OTEL_EXPORTER_OTLP_ENDPOINT: http://aspire-dashboard:18889`, `ConnectionStrings__Bank` igual à
  da API, `Mensageria__Host: rabbitmq`, `Mensageria__VirtualHost: ${RABBITMQ_VHOST:-bank}`,
  `Mensageria__Usuario: ${RABBITMQ_USER:-bank}`, `Mensageria__Senha: ${RABBITMQ_PASSWORD:-bank_dev_password}`,
  sem porta publicada, `depends_on` `api` e `rabbitmq` `service_healthy` e `aspire-dashboard`
  `service_started`, healthcheck `wget -q -O /dev/null http://localhost:8080/health/pronto`
  com os mesmos tempos da API, `restart: unless-stopped`); atualizar o cabeçalho do arquivo
  citando a spec 010
- [X] T005 [P] Criar `back/src/Bank.Worker/appsettings.json` com a seção `Mensageria`
  (`Porta: 5672`, `IntervalosNovaTentativaMs: [1000, 2000, 4000]`, `Prefetch: 10`,
  `IntervaloDespachoMs: 1000`, `TamanhoLoteDespacho: 50`, `TimeoutConfirmacaoMs: 5000`) e
  `Persistencia:AplicarMigracoesNaInicializacao: false`; `appsettings.Development.json` com
  `Host: localhost`, `VirtualHost: bank`, `Usuario: bank` e `ConnectionStrings:Bank` local
  (sem senha versionada além dos padrões de desenvolvimento já usados pela API); e
  `appsettings.Testing.json` com intervalos `[100, 200, 400]`, `IntervaloDespachoMs: 100`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: opções, conexão, topologia, telemetria, schema e infraestrutura de testes usados por
todas as histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Testes da fundação (escrever primeiro)

- [X] T006 [P] Em `back/tests/Bank.UnitTests/Infrastructure/MensageriaOptionsTests.cs`: validador
  recusa `Host`, `VirtualHost`, `Usuario` ou `Senha` vazios, `Porta` fora de 1–65535,
  `IntervalosNovaTentativaMs` vazio, não crescente ou com valor ≤ 0, `Prefetch` < 1,
  `TamanhoLoteDespacho` < 1 e `TimeoutConfirmacaoMs` < 100; aceita os padrões (research R11)
- [X] T007 [P] Em `back/tests/Bank.UnitTests/Infrastructure/TopologiaMensageriaTests.cs`: nomes
  derivados — `FilaEspera("notificacoes.transferencias.fila", 1000)` =
  `notificacoes.transferencias.espera.1000`; argumentos da fila principal
  (`x-dead-letter-exchange=banco.eventos.dlx`, `x-dead-letter-routing-key=transferencia.concluida.dlq`)
  e da espera (`x-message-ttl`, `x-dead-letter-exchange=""`, `x-dead-letter-routing-key` = fila
  principal), conforme [contracts/topologia-e-mensagens.md](contracts/topologia-e-mensagens.md)
- [X] T008 Criar `back/tests/Bank.IntegrationTests/RabbitMqCompartilhado.cs` (como
  `PostgresCompartilhado`: imagem `rabbitmq:4.3.7-management-alpine`, um contêiner por execução,
  um **vhost novo por fábrica** criado pela API de gestão ou `rabbitmqctl add_vhost` +
  `set_permissions`, para isolar as classes paralelas) e um método para criar um contêiner
  **exclusivo** (usado pela US2, que para e religa o broker)
- [X] T009 Criar `back/tests/Bank.IntegrationTests/BankWorkerFactory.cs`:
  `WebApplicationFactory<WorkerAssemblyMarker>`, ambiente `Testing`, recebe a connection string de
  uma `BankApiFactory` (banco já migrado) e o vhost/porta do RabbitMQ; acrescenta exportadores
  OpenTelemetry em memória (spans, métricas e logs) como a API faz nos testes; expõe helpers para
  abrir um canal de teste, ler mensagens de uma fila (`BasicGetAsync`), contar mensagens e
  esperar condições com timeout
- [X] T010 Em `back/tests/Bank.IntegrationTests/TopologiaTests.cs`: com o Worker iniciado, as 2
  exchanges, as 7 filas (com argumentos) e os bindings da FR-006 existem e são duráveis
  (`QueueDeclarePassiveAsync`/`ExchangeDeclarePassiveAsync` e a API de gestão para os argumentos);
  reiniciar o Worker não falha (declaração idempotente, FR-007)
- [X] T011 Em `back/tests/Bank.ArchitectureTests/LayerDependencyTests.cs`: carregar o assembly do
  Worker; novas regras — Domain e Application não dependem de `^RabbitMQ\.Client(\..*)?$` nem de
  `^OpenTelemetry(\..*)?$`; Worker só referencia a Infrastructure a partir de `Program`
  (composição), como a regra já existente da API

### Implementação da fundação

- [X] T012 [P] Criar `msg/MensageriaOptions.cs` (seção `Mensageria`, propriedades da research R11)
  e `ValidadorMensageriaOptions` (`IValidateOptions`), registrados com `ValidateOnStart`; a senha
  nunca entra em `ToString`/log
- [X] T013 [P] Criar `msg/TopologiaMensageria.cs` (estático, `internal`): constantes
  `ExchangeEventos = "banco.eventos.topic"`, `ExchangeDlx = "banco.eventos.dlx"`; descritor de
  consumidor (fila principal, routing key, routing key de DLQ, fila DLQ, se tem filas de espera);
  `FilaEspera(fila, ms)` (troca o sufixo `.fila` por `.espera.<ms>`); `DeclararAsync(IChannel,
  intervalos, ct)` declarando exchanges, as filas e os bindings de
  [contracts/topologia-e-mensagens.md](contracts/topologia-e-mensagens.md) (cobranças sem filas de
  espera), todas `durable: true, exclusive: false, autoDelete: false` (research R5)
- [X] T014 [P] Criar `msg/TelemetriaMensageria.cs`: `ActivitySource` e `Meter` `Bank.Mensageria`
  (via `IMeterFactory`), com os instrumentos da research R10 — `banco.eventos.processados`
  (`{evento}`; tags `tipo_evento`, `status`, `motivo` só em `falha_dlq`), `banco.eventos.publicados`,
  `banco.eventos.publicacao.falhas` (`motivo` em `sem-rota`/`nack`/`timeout`/`canal`) e o gauge
  observável `banco.outbox.pendentes`; métodos de registro tipados (sem tags livres)
- [X] T015 Criar `msg/ConexaoRabbitMq.cs` (singleton): `ConnectionFactory` com `HostName`, `Port`,
  `VirtualHost`, `UserName`, `Password`, `AutomaticRecoveryEnabled = false`,
  `TopologyRecoveryEnabled = false`, `RequestedHeartbeat = 10 s`, `ClientProvidedName = "bank-worker"`;
  `ObterAsync(ct)` devolve a conexão aberta ou abre outra se a atual estiver fechada (com
  `SemaphoreSlim`); log 5019 `ConexaoBrokerFechada` no `ConnectionShutdownAsync` não pedido
  (iniciador, código e motivo, sem credenciais); `DisposeAsync` fecha (research R3)
- [X] T016 Criar `msg/SessaoMensageria.cs`: laço reutilizável de sessões —
  `ExecutarAsync(Func<CancellationToken, Task> sessao, nome, ct)` que roda a sessão até exceção ou
  sinal de perda, registra 5016 `SessaoPerdida`/5017 `FalhaReabertura` (Warning) e 5018
  `SessaoReaberta` (Information, com o tempo fora em ms) e reabre após 0 s, 1 s, 2 s, 4 s, 8 s e
  depois 10 s fixos; encerra na hora com o `ct` do host (research R3)
- [X] T017 Criar `msg/EventoProcessado.cs` (`EventoId EventoId`, `string NomeConsumidor`,
  `DateTimeOffset ProcessadoEm`) e `back/src/Bank.Infrastructure/Persistencia/Configuracoes/EventoProcessadoConfiguracao.cs`:
  tabela `eventos_processados`; `evento_id uuid NOT NULL` (com `IdentificadorConverter`),
  `nome_consumidor varchar(100) NOT NULL`, `processado_em timestamptz NOT NULL`; chave
  `pk_eventos_processados (evento_id, nome_consumidor)`; `DbSet<EventoProcessado> EventosProcessados`
  em `BankDbContext` (data-model)
- [X] T018 Em `msg/EventoOutbox.cs` e `Persistencia/Configuracoes/EventoOutboxConfiguracao.cs`:
  propriedades `string? TraceParent` (coluna `trace_parent varchar(55)` nula) e `string? TraceState`
  (coluna `trace_state varchar(512)` nula); método `MarcarPublicado(DateTimeOffset)` (única
  alteração permitida); ainda sem preencher o trace (US5)
- [X] T019 Gerar a migration `AdicionarMensageria` (`dotnet ef migrations add AdicionarMensageria
  --project src/Bank.Infrastructure --startup-project src/Bank.Api`) e conferir que só cria
  `eventos_processados` e as 2 colunas do outbox
- [X] T020 Em `back/src/Bank.Infrastructure/DependencyInjection.cs`: extrair `AddPersistencia`
  (opções, `BankDbContext`, repositórios e consultas, `TimeProvider`) do `AddInfrastructure`;
  `AddInfrastructure` passa a chamar `AddPersistencia` + serviços de inicialização (migrations,
  lançamentos de abertura) + segurança, sem mudança para a API; novo `AddMensageria(configuration)`
  registrando `MensageriaOptions`, `ConexaoRabbitMq`, `TelemetriaMensageria` (os hosted services
  entram nas histórias)
- [X] T021 Em `back/src/Bank.Worker/Program.cs`: `AddApplication(...)`, `AddPersistencia`,
  `AddMensageria`, telemetria em `Observability/TelemetriaWorker.cs` (serviço `bank-worker`; logs
  OpenTelemetry com escopos; tracing com `Bank.Mensageria`; métricas com `System.Runtime`,
  `Bank.Mensageria` e `Bank.Notificacoes`; `UseOtlpExporter` fora do ambiente `Testing`, como em
  `TelemetryExtensions` da API) e `Saude/SaudeEndpoints.cs` com `/health` (vivo)
- [X] T022 Criar `msg/TopologiaNaInicializacao.cs` (hosted service do Worker, registrado em
  `AddMensageria`) que abre um canal e chama `TopologiaMensageria.DeclararAsync` na partida, com
  as tentativas da `SessaoMensageria` se o broker ainda não estiver pronto; T010 passa

**Checkpoint**: `dotnet test` verde com T006, T007, T010 e T011; `docker compose up -d --build --wait`
sobe o `worker` *healthy* e a topologia aparece na interface de gestão (quickstart, cenário 2)

---

## Phase 3: User Story 1 - Transferência concluída gera notificação (Priority: P1) 🎯 MVP

**Goal**: o evento sai do outbox para o RabbitMQ com confirmação e o Worker registra a notificação
e o evento processado

**Independent Test**: transferir e ver `publicado_em` preenchido, a notificação no log do Worker e
1 linha em `eventos_processados` (quickstart, cenário 3)

### Testes da US1 (escrever primeiro)

- [X] T023 [P] [US1] Em `back/tests/Bank.UnitTests/Domain/TransferenciaTests.cs`: o evento de
  `Transferencia.Executar` traz `PagadorNome`, `RecebedorNome` e `Descricao` (inclusive `null`)
- [X] T024 [P] [US1] Em `back/tests/Bank.UnitTests/Infrastructure/ContratoEventosTests.cs`
  (contrato, Princípio VII): `EventoOutbox.De` gera exatamente os campos e nomes camelCase de
  [contracts/evento-transferencia-concluida.md](contracts/evento-transferencia-concluida.md)
  (`tipoEvento = "TransferenciaConcluida"`, `versao = 1`, `descricao` `null` quando ausente,
  `valor` com 2 casas, identificadores como UUID); `tipo` e `versao` do registro
- [X] T025 [P] [US1] Em `back/tests/Bank.UnitTests/Infrastructure/LeitorTransferenciaConcluidaTests.cs`:
  lê o payload completo; lê payload antigo (só os 6 campos originais) sem erro; ignora campo
  desconhecido; recusa (resultado `mensagem-invalida`) JSON inválido, campo obrigatório ausente
  (`eventoId`, `transferenciaId`, `pagadorUsuarioId`, `recebedorUsuarioId`, `valor`, `ocorridoEm`),
  `type` ≠ `transferencia.concluida` e `versao` ≠ 1 (header como fonte; corpo como alternativa)
- [X] T026 [P] [US1] Em `back/tests/Bank.UnitTests/Application/NotificarTransferenciaConcluidaTests.cs`:
  registra 2 logs (papéis `pagador` e `recebedor`) com `eventoId`, `transferenciaId`, `usuarioId` e
  valor, e incrementa `bank.notificacoes.enviadas` por papel
- [X] T027 [P] [US1] Em `back/tests/Bank.UnitTests/Infrastructure/CabecalhosMensagemTests.cs`:
  propriedades da publicação conforme [contracts/topologia-e-mensagens.md](contracts/topologia-e-mensagens.md)
  (`ContentType`, `ContentEncoding`, `DeliveryMode` persistente, `MessageId = eventoId`,
  `Type`, `Timestamp` em segundos Unix, header `versao`)
- [X] T028 [US1] Em `back/tests/Bank.IntegrationTests/PublicacaoOutboxTests.cs`: com API + Worker
  (mesmo banco, vhost exclusivo), um `POST /api/v1/transferencias` resulta, em até 5 s, em
  `publicado_em` preenchido, 1 linha em `eventos_processados` (`nome_consumidor =
  notificacoes-transferencias`), os 2 logs de notificação e métrica `banco.eventos.processados`
  com `status=sucesso`; a resposta HTTP não espera o broker (SC-001, SC-007); no Worker,
  `/health` → 200 e `/health/pronto` → 200 com o consumidor inscrito (Princípio VII)

### Implementação da US1

- [X] T029 [P] [US1] Em `back/src/Bank.Domain/Transferencias/Transferencia.cs`:
  `TransferenciaConcluida` ganha `string PagadorNome`, `string RecebedorNome`, `string? Descricao`;
  `Transferencia.Executar` recebe `pagadorNome` e `recebedorNome` e preenche o evento (descrição da
  própria transferência) — data-model
- [X] T030 [US1] Em `back/src/Bank.Application/Transferencias/RealizarTransferencia.cs`: passar
  `pagador.NomeTitular` e `destino.Nome` para `Transferencia.Executar`; ajustar os demais chamadores
  e testes que constroem o evento
- [X] T031 [US1] Em `msg/EventoOutbox.cs`: `PayloadTransferenciaConcluida` com a ordem e os campos
  do contrato (`EventoId`, `TipoEvento`, `Versao`, `TransferenciaId`, `PagadorUsuarioId`,
  `PagadorNome`, `RecebedorUsuarioId`, `RecebedorNome`, `Valor`, `Descricao`, `OcorridoEm`);
  `descricao` serializada como `null` quando ausente (T024 passa)
- [X] T032 [P] [US1] Criar `back/src/Bank.Application/Observabilidade/MetricasNotificacoes.cs`
  (meter `Bank.Notificacoes`, contador `bank.notificacoes.enviadas`, `{notificacao}`, tag `papel`)
  e `back/src/Bank.Application/Notificacoes/NotificarTransferenciaConcluida.cs` com o comando
  `NotificacaoTransferencia(EventoId, TransferenciaId, PagadorUsuarioId, RecebedorUsuarioId, Valor,
  OcorridoEm)` e logs com `LoggerMessage` (sem nome nem descrição — FR-023); o caso de uso
  implementa a abstração `back/src/Bank.Application/Abstracoes/INotificadorTransferencia.cs`
  (`Task NotificarAsync(NotificacaoTransferencia, CancellationToken)`), que é o que o consumidor
  resolve por DI (research R8); registrar métricas e caso de uso em
  `back/src/Bank.Application/DependencyInjection.cs` (T026 passa)
- [X] T033 [P] [US1] Criar `msg/CabecalhosMensagem.cs`: monta `BasicProperties` da publicação a
  partir do `EventoOutbox` e lê de volta `MessageId`, `Type`, header `versao` (aceitando `byte[]`
  e inteiros, como o cliente entrega) — T027 passa
- [X] T034 [P] [US1] Criar `msg/LeitorTransferenciaConcluida.cs`: desserializa o contrato v1
  (`System.Text.Json`, `JsonSerializerDefaults.Web`) num DTO com `Guid`, valida os obrigatórios,
  o `type` e a `versao`, e converte para `NotificacaoTransferencia` com os tipos do Domain
  (`EventoId.De`, `TransferenciaId.De`, `UsuarioId.De`); resultado `Valida | Invalida(motivo)`
  — T025 passa
- [X] T035 [US1] Criar `back/src/Bank.Infrastructure/Persistencia/Sql/Consultas/ReservarEventosPendentes.sql`
  (cabeçalho com a spec de origem; `SELECT ... FROM outbox_eventos WHERE publicado_em IS NULL
  ORDER BY ocorrido_em, id LIMIT @lote FOR UPDATE SKIP LOCKED`) e `RegistrarEventoProcessado.sql`
  (`INSERT INTO eventos_processados (evento_id, nome_consumidor, processado_em) VALUES (...)
  ON CONFLICT DO NOTHING`)
- [X] T036 [US1] Criar `msg/DespachanteOutbox.cs` (`BackgroundService`, registrado por
  `AddMensageria`): a cada `IntervaloDespachoMs`, em escopo novo e transação `ReadCommitted`, reserva
  até `TamanhoLoteDespacho` pendentes (T035), publica cada um em
  `banco.eventos.topic` com routing key = `tipo`, `mandatory: true` e as propriedades de T033 num
  canal criado com `CreateChannelOptions(publisherConfirmationsEnabled: true,
  publisherConfirmationTrackingEnabled: true)`, aguarda a confirmação até `TimeoutConfirmacaoMs`,
  marca `MarcarPublicado(agora)` e faz commit; declara a topologia ao abrir o canal; métricas
  `banco.eventos.publicados` e gauge de pendentes; log 5001 `EventoPublicado` (Debug, `eventoId`,
  `tipo`) — research R4 (o tratamento de falhas e a reabertura entram na US2)
- [X] T037 [US1] Criar `msg/ConsumidorEventos.cs` (base abstrata `BackgroundService`): numa sessão,
  abre canal de consumo (`BasicQosAsync(prefetch)`, `ConsumerDispatchConcurrency = 1`,
  `autoAck: false`) e canal de publicação com confirms; declara a topologia; inscreve
  `AsyncEventingBasicConsumer` na fila principal; para cada entrega: lê (abstrato), abre transação,
  executa `RegistrarEventoProcessado.sql` com o `nomeConsumidor` e, se inseriu, chama o
  processamento (abstrato) e faz commit; depois `BasicAckAsync`; métrica `status=sucesso`.
  Comportamento provisório até a US3/US4, sem exceção escapando do handler: duplicado (0 linhas
  inseridas) → commit e ACK sem processar; leitura inválida ou exceção no processamento →
  rollback e `BasicNackAsync(requeue: false)` (a mensagem chega à DLQ pela dead letter exchange
  da fila) com log de erro com o `eventoId`; a US3 e a US4 substituem esses ramos
- [X] T038 [US1] Criar `msg/ConsumidorNotificacoesTransferencia.cs` (nome
  `notificacoes-transferencias`, fila `notificacoes.transferencias.fila`): usa
  `LeitorTransferenciaConcluida` e chama `INotificadorTransferencia` num escopo de DI;
  registrar em `AddMensageria`
- [X] T039 [US1] Em `back/src/Bank.Worker/Saude/SaudeEndpoints.cs`: `/health/pronto` responde 200
  só com a conexão aberta e o consumidor inscrito (estado exposto pelo `ConsumidorEventos`) e 503
  caso contrário; T028 passa

**Checkpoint**: quickstart, cenário 3, passos 1–2

---

## Phase 4: User Story 2 - Nenhum evento se perde quando o broker falha (Priority: P1)

**Goal**: transferências seguem concluindo com o broker fora; pendentes saem sozinhos quando ele
volta; publicação sem confirmação nunca marca o evento

**Independent Test**: parar o RabbitMQ, transferir 3 vezes (201), religar e ver os 3 publicados e
notificados (quickstart, cenário 4)

### Testes da US2 (escrever primeiro)

- [X] T040 [US2] Em `back/tests/Bank.IntegrationTests/BrokerIndisponivelTests.cs` (contêiner
  RabbitMQ **exclusivo**, T008): com o broker parado, 3 transferências → 201 e 3 pendentes; ao
  religar, em até 60 s os 3 têm `publicado_em` e 3 linhas em `eventos_processados`, sem reiniciar
  API nem Worker, e as 3 mensagens chegam à fila na ordem de `ocorrido_em` (US2, cenário 2 — ler
  com o consumidor parado ou por `BasicGet` numa fila de teste ligada à `banco.eventos.topic`);
  `/health/pronto` do Worker responde 503 com o broker parado e volta a 200 depois (SC-002,
  FR-003, Princípio VII)
- [X] T041 [US2] No mesmo arquivo: com a fila `notificacoes.transferencias.fila` apagada (sem rota)
  e com o despachante segurado entre a confirmação e o commit (dublê de falha no `SaveChanges`),
  o evento continua pendente na 1ª situação (log 5002 com motivo `sem-rota`, métrica de falha) e
  é publicado de novo na 2ª, gerando 1 só notificação; mensagens persistentes não consumidas
  sobrevivem a `docker restart` do broker (SC-005); e, em
  `back/tests/Bank.UnitTests/Infrastructure/DespachanteOutboxTests.cs`, com um dublê de
  `IPublicadorConfirmado` (T042) que devolve nack ou não confirma no prazo: o evento continua
  pendente, os anteriores do lote são marcados, os seguintes não são tentados e a falha é contada
  com `motivo` `nack`/`timeout` (FR-002)

### Implementação da US2

- [X] T042 [US2] Extrair a publicação com confirmação para `msg/PublicadorConfirmado.cs`
  (interface `internal` `IPublicadorConfirmado` com `PublicarAsync(...)` → `Confirmado | SemRota |
  Nack | Timeout | CanalFechado`, implementação sobre o `IChannel` com confirms rastreados), para que
  o laço do despachante seja testável sem broker. Em `msg/DespachanteOutbox.cs`: rodar dentro de
  `SessaoMensageria` (T016); tratar
  `BasicReturnAsync` (sem rota), nack, timeout de confirmação e canal fechado como falha — o lote
  para no primeiro que falha, os anteriores confirmados são marcados e o commit acontece; log 5002
  `FalhaPublicacao` (Warning, `eventoId`, `tipo`, motivo) e métrica
  `banco.eventos.publicacao.falhas`; espera entre rodadas com falha 2^n s, teto 10 s, volta ao
  intervalo normal no primeiro sucesso; canal fechado encerra a sessão (research R4)
- [X] T043 [US2] Em `msg/ConsumidorEventos.cs`: rodar dentro de `SessaoMensageria`; encerrar a
  sessão em `ChannelShutdownAsync` (consumo ou publicação) e em `ConsumerCancelled` pelo broker;
  ACK/NACK e republicações sempre nos canais da sessão que entregou (entrega de sessão morta é
  ignorada — o broker reentrega); atualizar o estado de prontidão (T039)

**Checkpoint**: quickstart, cenário 4

---

## Phase 5: User Story 3 - Entrega duplicada não repete a notificação (Priority: P1)

**Goal**: a mesma mensagem entregue 2+ vezes gera 1 notificação

**Independent Test**: publicar 2 vezes o mesmo `eventoId` → 1 notificação, 1 linha, métricas
`sucesso` + `duplicado`

### Testes da US3 (escrever primeiro)

- [X] T044 [US3] Em `back/tests/Bank.IntegrationTests/ConsumoIdempotenteTests.cs`: publicar duas vezes
  a mesma mensagem (sequencial) e 5 cópias simultâneas de outra (com `Prefetch` e duas instâncias
  do Worker na mesma fila) → cada evento com 1 linha em `eventos_processados`, 1 par de logs de
  notificação e métrica `duplicado` para as demais; todas confirmadas (fila vazia) — SC-003; e,
  com os dois Workers despachando o mesmo outbox, 10 transferências → a soma de
  `banco.eventos.publicados` dos dois é exatamente 10 e cada evento chega uma vez à fila
  (`FOR UPDATE SKIP LOCKED`, FR-004)

### Implementação da US3

- [X] T045 [US3] Em `msg/ConsumidorEventos.cs`: quando `RegistrarEventoProcessado.sql` afeta 0
  linhas, commit sem processar, `BasicAckAsync`, métrica `status=duplicado` e log 5005
  `EventoDuplicado` (Information, `eventoId`, consumidor) — FR-017

**Checkpoint**: quickstart, cenário 5, passo 1

---

## Phase 6: User Story 4 - Falha passageira é tentada de novo; falha permanente vai para a DLQ (Priority: P2)

**Goal**: 3 novas tentativas (1 s, 2 s, 4 s) por filas de espera; DLQ com headers de diagnóstico;
inválidas direto para a DLQ; reprocessamento recomeça a contagem

**Independent Test**: falha nas 2 primeiras execuções → notificada na 3ª; falha sempre → DLQ após
4 execuções com `x-tentativas=4`; inválida → DLQ após 1 (quickstart, cenário 5)

### Testes da US4 (escrever primeiro)

- [X] T046 [P] [US4] Em `back/tests/Bank.UnitTests/Infrastructure/PoliticaNovaTentativaTests.cs`:
  sem headers → execução 1; `x-tentativas = 2` → execução 3; com `x-falhou-em` → execução 1 e
  `x-reprocessamentos` + 1 (1 se ausente); falha na execução N ≤ 3 → destino espera do N-ésimo
  intervalo, headers da cópia com `x-tentativas = N`, sem `x-falhou-em`/`x-motivo`/`x-ultimo-erro`,
  com `x-reprocessamentos` preservado; falha na 4ª → DLQ com `x-tentativas = 4`,
  `x-motivo = tentativas-esgotadas`, `x-falhou-em` ISO 8601 UTC e `x-ultimo-erro =
  "<TipoDaExcecao>: <mensagem>"` truncado em 500 caracteres; inválida → DLQ com
  `x-motivo = mensagem-invalida`; cópias preservam `MessageId`, `Type`, `Timestamp`, `versao`,
  `traceparent`, `tracestate` (FR-018, FR-018a–c)
- [X] T047 [US4] Em `back/tests/Bank.IntegrationTests/NovaTentativaEDlqTests.cs` (intervalos de teste
  100/200/400 ms; caso de uso de notificação substituído por dublê controlável no Worker de teste):
  (a) falha nas 2 primeiras execuções → notificação na 3ª, 1 linha, métricas `nova_tentativa` × 2
  e `sucesso`, e outra mensagem publicada no meio é processada sem esperar; (b) falha sempre →
  mensagem em `notificacoes.transferencias.dlq` com os headers do contrato, nada em
  `eventos_processados`, métrica `falha_dlq` com `motivo=tentativas-esgotadas`; (c) corpo inválido
  → DLQ após 1 execução com `mensagem-invalida`; (d) mover a mensagem de (b) de volta, com o dublê
  já sem falhar → processada, `x-reprocessamentos = 1` no log; (e) mover de novo com o dublê
  falhando sempre → DLQ com `x-tentativas = 4` e `x-reprocessamentos = 1`, sem loop (SC-004, SC-008)
- [X] T048 [US4] No mesmo arquivo: com a republicação forçada a falhar (dublê do canal de publicação
  que não confirma), a original recebe NACK sem requeue e chega à DLQ pela dead letter exchange,
  com `x-death` (rede de segurança, FR-018)

### Implementação da US4

- [X] T049 [P] [US4] Criar `msg/PoliticaNovaTentativa.cs` (puro, `internal`): a partir dos headers
  da entrega e dos intervalos configurados, calcula a execução atual e, dado o desfecho (falha com
  exceção ou inválida), o destino (`Espera(fila, ms)` ou `Dlq(motivo)`) e os headers da cópia;
  constantes `x-tentativas`, `x-reprocessamentos`, `x-motivo`, `x-ultimo-erro`, `x-falhou-em`
  — T046 passa
- [X] T050 [US4] Em `msg/ConsumidorEventos.cs`: leitura inválida → DLQ; exceção no processamento →
  rollback e destino pela política; republicar a cópia (corpo e propriedades originais, headers
  novos) na fila de espera pela exchange padrão ou na `banco.eventos.dlx` com a routing key de DLQ,
  no canal de publicação com confirms da sessão; ACK após a confirmação; se a republicação falhar,
  `BasicNackAsync(requeue: false)`; métricas `nova_tentativa`/`falha_dlq` (com `motivo`); logs 5003
  `NovaTentativa` (Warning: `eventoId`, execução, intervalo) e 5004 `EnviadoParaDlq` (Error:
  `eventoId`, motivo, tipo da exceção, execuções, reprocessamentos), nunca com payload — research R7
- [X] T051 [US4] Em `back/tests/Bank.IntegrationTests/BankWorkerFactory.cs`: permitir registrar um
  dublê de `INotificadorTransferencia` (T032) que falha nas N primeiras chamadas ou sempre, e um
  dublê de canal de publicação que não confirma; T047 e T048 passam

**Checkpoint**: quickstart, cenário 5, passos 2–4

---

## Phase 7: User Story 5 - Trace ponta a ponta da transferência à notificação (Priority: P2)

**Goal**: requisição, publicação e consumo no mesmo trace, mesmo com publicação atrasada

**Independent Test**: um `POST /transferencias` gera um trace com os spans HTTP, Producer e
Consumer (quickstart, cenário 3, passo 3)

### Testes da US5 (escrever primeiro)

- [X] T052 [P] [US5] Acrescentar em `back/tests/Bank.UnitTests/Infrastructure/EventoOutboxTests.cs`
  (arquivo existente, spec 006): com
  `Activity.Current` ativo, `EventoOutbox.De` grava `TraceParent` no formato W3C
  (`00-<32 hex>-<16 hex>-<2 hex>`) e `TraceState`; sem atividade, ambos nulos
- [X] T053 [P] [US5] Em `back/tests/Bank.UnitTests/Infrastructure/CabecalhosMensagemTests.cs`:
  injeção e extração com `Propagators.DefaultTextMapPropagator` (`traceparent`/`tracestate` como
  string UTF-8 nos headers; leitura aceita `byte[]`); headers ausentes → contexto vazio
- [X] T054 [US5] Em `back/tests/Bank.IntegrationTests/RastreamentoMensageriaTests.cs`: com
  exportadores em memória na API e no Worker, o `TraceId` do span HTTP do `POST` é o mesmo do span
  Producer `banco.eventos.topic publish` (atributos `messaging.*` do contrato) e do Consumer
  `notificacoes.transferencias.fila process`, cujo pai é o Producer; com publicação atrasada
  (Worker iniciado depois da transferência) o trace é o mesmo; mensagem sem `traceparent` →
  processada num trace novo (SC-006)

### Implementação da US5

- [X] T055 [US5] Em `msg/EventoOutbox.cs`: `De` preenche `TraceParent`/`TraceState` a partir de
  `Activity.Current` (`Id` no formato W3C e `TraceStateString`) — T052 passa
- [X] T056 [US5] Em `msg/CabecalhosMensagem.cs`: `Injetar(ActivityContext, headers)` e
  `Extrair(headers)` com `Propagators.DefaultTextMapPropagator` (`PropagationContext`) — T053 passa
- [X] T057 [US5] Em `msg/DespachanteOutbox.cs`: para cada evento, `ActivityContext.TryParse(TraceParent,
  TraceState)`, abrir `ActivityKind.Producer` `banco.eventos.topic publish` com esse pai (ou raiz),
  atributos `messaging.system=rabbitmq`, `messaging.operation.type=send`,
  `messaging.destination.name=banco.eventos.topic`, `messaging.rabbitmq.destination.routing_key`,
  `messaging.message.id`, e injetar o contexto do span nos headers; status de erro na falha
- [X] T058 [US5] Em `msg/ConsumidorEventos.cs`: extrair o contexto dos headers e abrir
  `ActivityKind.Consumer` `<fila> process` com ele como pai, atributos `messaging.system`,
  `messaging.operation.type=process`, `messaging.destination.name`, `messaging.message.id`,
  `bank.mensageria.tentativa` e `bank.mensageria.resultado`; os logs do processamento ficam
  correlacionados ao span — T054 passa

**Checkpoint**: quickstart, cenário 3, passo 3

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T059 [P] Em `back/tests/Bank.IntegrationTests/DadosSensiveisMensageriaTests.cs` (classe própria, com o ambiente de mensageria): transferência
  com descrição e nomes conhecidos passando por publicação, consumo, nova tentativa e DLQ → nenhum
  nome, descrição, documento, e-mail ou payload em logs, spans e métricas da API e do Worker;
  nenhum identificador de usuário ou de evento em tags de métrica; senha do broker ausente dos
  logs (FR-023)
- [X] T060 [P] Em `back/tests/Bank.UnitTests/Infrastructure/ContratoEventosTests.cs`: o contrato
  `cobranca.paga` serializa conforme [contracts/evento-cobranca-paga.md](contracts/evento-cobranca-paga.md)
  (record de contrato em `msg/Contratos/CobrancaPagaV1.cs`, sem produtor; `ultimosDigitosCartao`
  com exatamente 4 dígitos) — FR-012
- [X] T061 [P] Atualizar `specs/006-transferencias/contracts/evento-transferencia-concluida.md`
  (Histórico: "v1, revisão 2026-11-21 → specs/010-mensageria-rabbitmq") e
  `specs/001-infra-local/contracts/ambiente-local.md` (serviço `worker`, healthcheck, variáveis
  `Mensageria__*`) — FR-013
- [X] T062 [P] Em `docs/desenvolvimento.md` e `README.md`: o serviço `worker`, a seção de
  configuração `Mensageria`, como inspecionar e reprocessar a DLQ pela interface de gestão e como
  ver o trace ponta a ponta no Aspire Dashboard
- [X] T063 Rodar o quickstart completo (cenários 1–5), incluindo `dotnet test` em `src/backend/`;
  medir SC-001 (20 transferências, p95 até a notificação ≤ 5 s) e SC-007 (p95 da transferência
  < 1 s com o broker no ar e fora) e registrar no PR; atualizar spec, plano e contratos se algo
  divergir (Princípio I)

---

## Dependencies & Execution Order

- **Setup (1)** → **Foundational (2)** → US1 (3) → US2 (4) → US3 (5) → US4 (6) → US5 (7) → Polish (8)
- US2, US3, US4 e US5 alteram `DespachanteOutbox.cs` e/ou `ConsumidorEventos.cs` criados na US1,
  por isso seguem em sequência. US3 e US5 são independentes entre si depois da US1 e podem
  inverter a ordem; US4 depende da US3 (ramo de duplicado antes do de falha no mesmo método).
- T019 (migration) depende de T017 e T018; T021 e T022 dependem de T012–T016 e T020.
- Dentro de cada história: testes primeiro (falhando), depois implementação.

### Parallel Opportunities

- T003, T004 e T005
- T006 e T007; T012, T013 e T014
- T023, T024, T025, T026 e T027; T029, T032, T033 e T034
- T046 e T049 (teste e política, mesma dupla de arquivos novos)
- T052 e T053
- T059, T060, T061 e T062

## Parallel Example: User Story 1

```text
T023 TransferenciaTests.cs        T024 ContratoEventosTests.cs     T025 LeitorTransferenciaConcluidaTests.cs
T026 NotificarTransferenciaConcluidaTests.cs                       T027 CabecalhosMensagemTests.cs
T029 Transferencia.cs             T032 NotificarTransferenciaConcluida.cs
T033 CabecalhosMensagem.cs        T034 LeitorTransferenciaConcluida.cs
```

---

## Implementation Strategy

### MVP First

1. Setup e fundação (T001–T022)
2. US1 (T023–T039) → transferência até a notificação
3. **PARAR e VALIDAR**: quickstart, cenários 2 e 3 (passos 1–2)

### Incremental Delivery

1. US2 → resiliência à queda do broker (sessões e falhas de publicação)
2. US3 → duplicatas descartadas
3. US4 → filas de espera, DLQ com diagnóstico e reprocessamento
4. US5 → trace ponta a ponta
5. Polish → telemetria sem dados sensíveis, contratos e documentação

### Commits

Conventional Commits por fase, escopo `mensageria` (por exemplo `build(mensageria): ...`,
`feat(mensageria): ...`, `test(mensageria): ...`); PR com `Closes #20`.
