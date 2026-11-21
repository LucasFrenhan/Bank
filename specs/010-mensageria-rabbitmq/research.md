# Research: Mensageria RabbitMQ

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Decisões técnicas do plano. Cada item traz a decisão, o motivo e as alternativas descartadas. A
referência de desenho é o Order-Management-System (OMS: `docs/fluxos/08-publicacao-e-consumo-de-eventos.md`,
`Infrastructure/Mensageria/`), adaptado ao PostgreSQL e às regras desta constituição.

## R1 — Processo: `Bank.Worker` hospeda o publicador e o consumidor

**Decisão**: um projeto novo `src/backend/src/Bank.Worker` (SDK `Microsoft.NET.Sdk.Web`), raiz de
composição como a API, que hospeda dois `BackgroundService`: o **despachante do outbox** (R4) e o
**consumidor de notificações de transferência** (R7). A API **não** acessa o RabbitMQ. O Worker
expõe só `/health` (vivo) e `/health/pronto` (broker conectado e consumidor inscrito), usados pelo
healthcheck do Compose; serviço OpenTelemetry `bank-worker`.

**Motivo**: Clarification Q3 (processo próprio). Concentrar tudo o que fala com o broker num só
processo mantém a API sem conexão AMQP, sem impacto no tempo de resposta (SC-007) e com os testes
atuais intactos; é o mesmo arranjo do OMS. O SDK Web dá o endpoint de saúde com o mesmo `wget` do
healthcheck da API, sem pacote novo.

**Alternativas**: despachante na API e consumidor no Worker (duas conexões, topologia declarada em
dois lugares, a API passa a depender do broker); Worker com `Microsoft.NET.Sdk.Worker` sem HTTP
(o healthcheck exigiria arquivo de sinalização ou comando próprio).

## R2 — Dependências novas

| Pacote | Versão | Onde | Justificativa |
| --- | --- | --- | --- |
| `RabbitMQ.Client` | 7.2.2 | Infrastructure | Cliente oficial assíncrono exigido pela constituição (RN-02); a mesma versão já validada no OMS |
| `OpenTelemetry.Api` | 1.21.0 | Infrastructure | `Propagators.DefaultTextMapPropagator` (W3C `traceparent`/`tracestate`, RN-06) sem levar o SDK para a Infrastructure; versão alinhada às demais `OpenTelemetry.*` |
| `Testcontainers.RabbitMq` | 4.16.0 | IntegrationTests | RabbitMQ real em contêiner (Princípio VII); mesma versão do `Testcontainers.PostgreSql` |

O Worker usa os pacotes de OpenTelemetry que a API já usa (`Extensions.Hosting`,
`Exporter.OpenTelemetryProtocol`, `Instrumentation.AspNetCore`). Frameworks de abstração
(MassTransit, Wolverine) continuam proibidos.

## R3 — Conexão e sessões

**Decisão**: uma `IConnection` por processo, **sem** a recuperação automática do cliente
(`AutomaticRecoveryEnabled = false`, `TopologyRecoveryEnabled = false`), com heartbeat de 10 s.
Cada componente (despachante e consumidor) trabalha em **sessões**: abre os canais de que precisa,
declara a topologia (R5) e trabalha até o primeiro sinal de perda (canal ou conexão fechados,
inscrição cancelada pelo broker); então descarta a sessão, e a conexão se estiver morta, e abre
outra após 0 s, 1 s, 2 s, 4 s, 8 s e depois 10 s fixos. A configuração vem da seção `Mensageria`
(host, porta, vhost, usuário, senha), com as variáveis do Compose; a senha nunca aparece em log.

**Motivo**: o OMS teve consumidores sem inscrição até reiniciar o processo por misturar a
recuperação automática com o descarte de conexões "fechadas" (issue #41 de lá). Ser dono da
recuperação torna o comportamento testável (parar e religar o contêiner, US2) e cumpre o FR-003.

**Alternativas**: recuperação automática do cliente (estado intermediário "em recuperação" difícil
de observar e de combinar com publisher confirms); uma conexão por componente (sem ganho real em
volume de demonstração).

## R4 — Despachante do outbox

O "despachante" (`DespachanteOutbox`) é o **publicador** da spec (FR-002–FR-004).

**Decisão**: a cada 1 s (configurável), numa transação `ReadCommitted`:

1. lê até 50 pendentes com SQL puro em arquivo
   (`Persistencia/Sql/Consultas/ReservarEventosPendentes.sql`):
   `... WHERE publicado_em IS NULL ORDER BY ocorrido_em, id LIMIT @lote FOR UPDATE SKIP LOCKED`;
2. publica cada um em ordem num canal com **publisher confirms** e rastreamento
   (`CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true)`),
   `mandatory: true` e espera a confirmação por até 5 s;
3. marca `publicado_em` dos confirmados e faz commit.

No primeiro evento que falha (nack, devolvido sem rota — `BasicReturn` —, timeout ou canal
fechado), o lote para: os anteriores são marcados, o que falhou e os seguintes continuam pendentes
(FR-002, ordem de gravação). A espera até a próxima rodada cresce 2^n s com teto de 10 s e volta a
1 s no primeiro sucesso. Falha de canal encerra a sessão (R3).

**Motivo**: `FOR UPDATE SKIP LOCKED` é o equivalente PostgreSQL do `UPDLOCK, READPAST` do OMS: duas
instâncias não pegam o mesmo evento (FR-004) e não se bloqueiam. Marcar só após a confirmação
cumpre RN-01/FR-002; a queda entre confirmação e commit republica o evento, absorvido pela
idempotência do consumidor (US3). SQL em `.sql` cumpre a restrição de SQL puro. A publicação com
confirmação fica atrás de `IPublicadorConfirmado` (resultado `Confirmado | SemRota | Nack |
Timeout | CanalFechado`), para testar o laço do lote sem broker.

**Alternativas**: coluna de "reservado até" com atualização otimista (mais estado, mais casos);
`LISTEN/NOTIFY` para acordar o despachante (latência menor, mas o polling de 1 s já cumpre SC-001
de 5 s); publicar direto do caso de uso após o commit (perde evento se o processo cair, viola o
Princípio IV).

## R5 — Topologia declarada pelo Worker

**Decisão**: uma classe `TopologiaMensageria` (como no OMS) declara, a cada canal novo e de forma
idempotente: as exchanges `banco.eventos.topic` e `banco.eventos.dlx` (topic, duráveis); para cada
consumidor conhecido, a fila principal (com `x-dead-letter-exchange` e
`x-dead-letter-routing-key`), as filas de espera e a DLQ, com os bindings da FR-006; e as filas de
cobrança (principal e DLQ) sem filas de espera. As filas de espera são
`<fila sem o sufixo .fila>.espera.<ms>` — `notificacoes.transferencias.espera.1000` etc. —, com
`x-message-ttl = ms`, `x-dead-letter-exchange = ""` e `x-dead-letter-routing-key = <fila principal>`.
O despachante e o consumidor usam a mesma função, então as declarações nunca divergem
(`PRECONDITION_FAILED`). Filas clássicas duráveis.

**Motivo**: FR-006–FR-008; declarar antes de publicar garante rota para o `mandatory` (R4) mesmo
com o broker recém-criado. Uma fila de espera por intervalo (em vez de TTL por mensagem) evita o
bloqueio de "cabeça de fila" do TTL por mensagem.

**Alternativas**: quorum queues (exigem cluster para fazer sentido; o ambiente é de nó único);
TTL por mensagem numa única fila de espera (uma mensagem de 4 s seguraria a de 1 s atrás dela);
plugin `delayed-message-exchange` (exige plugin na imagem).

## R6 — Mensagem: corpo, propriedades e contexto de trace

**Decisão**:

- **Corpo**: o `payload` do outbox, publicado como está (UTF-8). O payload v1 passa a ser gravado
  com `tipoEvento`, `versao`, `pagadorNome`, `recebedorNome` e `descricao` (FR-011).
- **Propriedades**: `ContentType = application/json`, `ContentEncoding = utf-8`,
  `MessageId = eventoId`, `Type = tipo` (`transferencia.concluida`), `Timestamp = ocorridoEm`
  (segundos Unix), `DeliveryMode = Persistent`, header `versao` (int).
- **Trace**: `outbox_eventos` ganha `trace_parent` e `trace_state` (nullable), preenchidos por
  `EventoOutbox.De` a partir de `Activity.Current` no momento da gravação (FR-005). O despachante
  abre o span Producer com esse contexto como pai e injeta o contexto **do span Producer** nos
  headers com `Propagators.DefaultTextMapPropagator` (RN-06), como `traceparent`/`tracestate`
  (string UTF-8). Sem contexto gravado (eventos antigos), o span Producer abre um trace novo.

**Motivo**: o despachante roda num laço de fundo; nesse momento `Activity.Current` não é a
requisição da transferência. Guardar o contexto na gravação é o que faz o trace ir da requisição
HTTP ao Worker (US5, SC-006), mesmo com a publicação atrasada por indisponibilidade do broker.

**Alternativas**: link de span em vez de pai (o trace da requisição não mostraria a publicação nem
o consumo na mesma árvore); guardar o contexto dentro do payload (mistura telemetria no contrato de
negócio).

## R7 — Consumidor: fluxo de uma entrega

**Decisão**: canal de consumo com `prefetch = 10` e `ConsumerDispatchConcurrency = 1` (uma mensagem
por vez por Worker; ordem simples de raciocinar), `autoAck = false`, e um canal de publicação com
confirms na mesma sessão (para espera e DLQ). Para cada entrega:

1. **Ler**: desserializa o corpo; exige `eventoId`, `transferenciaId`, `pagadorUsuarioId`,
   `recebedorUsuarioId`, `valor`, `ocorridoEm` e `versao` (header ou corpo) igual a 1; tipo
   (`Type`) igual a `transferencia.concluida`. Falhou → DLQ `mensagem-invalida` (FR-018b).
2. **Contar**: se a entrega tem `x-falhou-em`, é reprocessamento: tentativas = 0 e
   `x-reprocessamentos` + 1 (FR-018c); senão, tentativas = `x-tentativas` (0 se ausente).
3. **Processar** numa transação: `INSERT INTO eventos_processados ... ON CONFLICT DO NOTHING`
   (SQL em arquivo). 0 linhas → duplicado: commit, ACK, métrica `duplicado`. 1 linha → chama o caso
   de uso `NotificarTransferenciaConcluida` (Application, R8) e faz commit; depois ACK, métrica
   `sucesso`.
4. **Falha** (exceção nos passos 3): rollback; tentativas + 1. Se ≤ 3, publica a cópia na fila de
   espera do intervalo correspondente pela exchange padrão (com `x-tentativas`, sem `x-falhou-em`,
   `x-motivo`, `x-ultimo-erro`; mantém `x-reprocessamentos`), espera a confirmação e dá ACK;
   métrica `nova_tentativa`. Se for a 4ª, publica na `banco.eventos.dlx` com a routing key de DLQ
   e os headers de diagnóstico (FR-018), espera a confirmação e dá ACK; métrica `falha_dlq`.
5. **Republicação falhou** (nack/timeout/canal): `BasicNack(requeue: false)` na original, que
   chega à DLQ pela dead letter exchange da fila (rede de segurança, FR-018).

O ACK, o NACK e a republicação usam os canais da sessão que entregou; se esse canal caiu, o broker
reentrega a mensagem com os headers de antes, e o passo 3 descarta a duplicata se o efeito já foi
gravado. O `INSERT` antes do efeito faz entregas simultâneas do mesmo evento esperarem a primeira
transação (a segunda vê o conflito e vira `duplicado`, FR-017).

`x-ultimo-erro` = `"<TipoDaExcecao>: <mensagem>"`, truncado em 500 caracteres; nunca o payload.
`x-falhou-em` = ISO 8601 UTC. As cópias preservam `MessageId`, `Type`, `Timestamp`, `versao`,
`traceparent` e `tracestate`.

**Motivo**: o modelo do OMS (Clarifications Q4–Q6), com ACK só após o commit e após a confirmação
da republicação: nenhuma mensagem se perde entre a fila principal, a espera e a DLQ.

**Alternativas**: `BasicNack(requeue: true)` para retentativa (loop imediato, sem intervalo);
registrar o processado **depois** do efeito sem transação (entregas simultâneas notificariam duas
vezes); `ConsumerDispatchConcurrency > 1` (sem necessidade de vazão na demonstração).

## R8 — Onde ficam as regras: Application x Infrastructure

**Decisão**:

- **Domain**: `TransferenciaConcluida` ganha `PagadorNome`, `RecebedorNome` e `Descricao`;
  `Transferencia.Executar` recebe os nomes (já lidos pelo caso de uso: `pagador.NomeTitular` e
  `destino.Nome`) e preenche o evento; a descrição vem da própria transferência.
- **Application**: caso de uso `Notificacoes/NotificarTransferenciaConcluida` com o comando
  `NotificacaoTransferencia(EventoId, TransferenciaId, PagadorUsuarioId, RecebedorUsuarioId, Valor,
  OcorridoEm)` (tipos do Domain). Ele registra um log estruturado por destinatário ("notificação de
  transferência enviada/recebida", com `eventoId`, `transferenciaId`, `usuarioId`, papel e valor;
  sem nome nem descrição — FR-023) e conta a notificação. O consumidor depende da abstração
  `Abstracoes/INotificadorTransferencia`, implementada pelo caso de uso, o que permite trocar o
  efeito por um dublê nos testes de novas tentativas e DLQ e, no futuro, pelo envio real.
- **Infrastructure/Mensageria**: conexão, topologia, despachante, consumidor base (leitura,
  idempotência, espera, DLQ, spans e métricas), o leitor do contrato v1 (DTO com `Guid`,
  convertido para os tipos do Domain) e as entidades `EventoProcessado`/`EventoOutbox`.
- **Worker**: só composição (DI, telemetria, health).

**Motivo**: Princípio II (Domain sem infraestrutura; regras em Application/Domain) e a regra dos
identificadores tipados (contrato externo em `Guid`, tipos do Domain por dentro). O caso de uso de
notificação é trivial hoje, mas é onde o envio real (e-mail/push) entrará.

**Alternativas**: nomes buscados pelo consumidor no banco (Clarification Q1 escolheu enviá-los no
evento); notificação dentro da Infrastructure (regra de negócio fora da Application).

## R9 — Persistência no Worker

**Decisão**: o Worker usa o mesmo `BankDbContext` (registro extraído para `AddPersistencia`), sem
os serviços de inicialização da API (migrations e lançamentos de abertura ficam só na API). No
Compose, o Worker depende da API saudável, que já aplicou as migrations. A migration
`AdicionarMensageria` cria `eventos_processados` e as colunas `trace_parent`/`trace_state` do
outbox.

**Motivo**: um único dono das migrations evita duas aplicações concorrentes no mesmo banco; um
único `DbContext` evita duplicar mapeamentos do outbox.

**Alternativas**: `DbContext` próprio do Worker (duplicaria `outbox_eventos`, e o snapshot de
migrations ficaria em dois lugares); Worker também aplicando migrations (corrida na partida).

## R10 — Telemetria

**Decisão**: `ActivitySource` e `Meter` `Bank.Mensageria` (Infrastructure), registrados no Worker.

| Sinal | Nome | Detalhes |
| --- | --- | --- |
| Span Producer | `banco.eventos.topic publish` | `messaging.system=rabbitmq`, `messaging.operation.type=send`, `messaging.destination.name`, `messaging.rabbitmq.destination.routing_key`, `messaging.message.id`; erro → `Status=Error` |
| Span Consumer | `notificacoes.transferencias.fila process` | pai extraído dos headers; `messaging.operation.type=process`, `messaging.destination.name` (fila), `messaging.message.id`, `bank.mensageria.tentativa`, `bank.mensageria.resultado` |
| Contador | `banco.eventos.processados` (`{evento}`) | `tipo_evento`, `status` (`sucesso`, `duplicado`, `nova_tentativa`, `falha_dlq`), `motivo` só em `falha_dlq`; exportado em Prometheus como `banco_eventos_processados_total` |
| Contador | `banco.eventos.publicados` (`{evento}`) | `tipo_evento` |
| Contador | `banco.eventos.publicacao.falhas` (`{evento}`) | `tipo_evento`, `motivo` (`sem-rota`, `nack`, `timeout`, `canal`) |
| Gauge observável | `banco.outbox.pendentes` (`{evento}`) | último total de pendentes lido pelo despachante (`COUNT` a cada rodada, apoiado pelo índice parcial existente) |
| Contador | `bank.notificacoes.enviadas` (`{notificacao}`) | `papel` (`pagador`, `recebedor`) — Application |

Logs com `EventId` próprios (faixa 5000, como no OMS): evento publicado (Debug), falha de
publicação (Warning), nova tentativa (Warning), enviado para a DLQ (Error), duplicado
(Information), sessão perdida/reaberta (Warning/Information). Nenhum com payload, nome, descrição
ou credencial.

Os nomes dos spans seguem a issue #20. A convenção semântica atual prefere
`{operação} {destino}` (`publish banco.eventos.topic`); manter o nome da issue é aceitável porque
o backend de telemetria filtra pelos atributos `messaging.*`, que seguem a convenção.

## R11 — Configuração

Seção `Mensageria` (`MensageriaOptions`, validada na partida): `Host`, `Porta` (5672),
`VirtualHost`, `Usuario`, `Senha` (obrigatórios; no Compose, as variáveis `RABBITMQ_*` do
`.env`), `IntervalosNovaTentativaMs` (`[1000, 2000, 4000]`), `Prefetch` (10),
`IntervaloDespachoMs` (1000), `TamanhoLoteDespacho` (50), `TimeoutConfirmacaoMs` (5000). Os
testes usam intervalos curtos (`[100, 200, 400]`), o que também muda o nome das filas de espera.

## R12 — Testes

- **Unitários**: `TransferenciaConcluida`/`Transferencia.Executar` com nomes e descrição;
  `EventoOutbox.De` (payload v1 completo, contexto de trace); `NotificarTransferenciaConcluida`;
  política de tentativas (contagem, reprocessamento, cabeçalhos da cópia e da DLQ, truncamento do
  erro); leitor da mensagem (válida, inválida, versão desconhecida, campos novos ignorados, sem
  nomes); `MensageriaOptions`.
- **Contrato** (Princípio VII): serialização do payload confere com o exemplo e com a tabela de
  campos de [contracts/evento-transferencia-concluida.md](contracts/evento-transferencia-concluida.md);
  leitura de um payload antigo (sem os campos novos) continua válida; `cobranca.paga` serializa
  conforme [contracts/evento-cobranca-paga.md](contracts/evento-cobranca-paga.md).
- **Integração** (Testcontainers PostgreSQL + RabbitMQ; Worker via `WebApplicationFactory` com um
  marcador do assembly do Worker, para não conflitar com o `Program` da API): topologia declarada
  (filas, argumentos e bindings); transferência → outbox publicado → notificação → linha em
  `eventos_processados` (US1); broker parado → pendentes → broker religado → publicados (US2);
  mensagem duplicada e entregas simultâneas (US3); falha passageira, falha permanente (4 execuções
  → DLQ com headers), mensagem inválida e reprocessamento da DLQ (US4); spans Producer/Consumer no
  mesmo trace do `POST` (US5, exportador em memória); telemetria sem dados sensíveis.
- **Arquitetura**: `Bank.Worker` entra nas regras de dependência (como a API: só a raiz de
  composição conhece a Infrastructure); Domain e Application não dependem de `RabbitMQ.Client`
  nem de `OpenTelemetry`.
