# Feature Specification: Mensageria RabbitMQ (topologia, contratos, DLQ e rastreamento)

**Feature Branch**: `010-mensageria-rabbitmq`

**Created**: 2026-11-21

**Status**: Draft

**Input**: Issue #20 ("Topologia RabbitMQ, Contratos de Eventos, DLQ e Rastreamento
Distribuído", TASK-09). Hoje cada transferência concluída grava o evento
`transferencia.concluida` v1 na tabela `outbox_eventos`, na mesma transação (spec 006, FR-017 e
research R9), mas nada o publica: `publicado_em` fica nulo. Esta spec define como esses eventos
saem do outbox para o RabbitMQ e chegam a um consumidor de notificações: topologia (exchanges,
filas e DLQs), contratos das mensagens, garantias de entrega, idempotência no consumidor,
tratamento de mensagens com erro e continuidade do trace distribuído. Pontos da issue: RN-01
(publicar só após o commit; falha no envio não desfaz a operação), RN-02 (cliente nativo, com
confirmação do broker e ACK/NACK manuais), RN-03 (durabilidade), RN-04 (mensagem com erro vai
para a DLQ, sem loop), RN-05 (idempotência por `eventos_processados`) e RN-06 (propagação de
`traceparent`/`tracestate` W3C).

## Clarifications

### Session 2026-11-21

- Q: O payload da issue traz `tipoEvento`, `pagadorNome`, `recebedorNome` e `descricao`, mas o
  contrato v1 atual só tem identificadores. Como fica o contrato? → A: Mantém a versão 1 e
  acrescenta, como campos novos (compatíveis), `tipoEvento`, `versao`, `pagadorNome`,
  `recebedorNome` e `descricao` (opcional, como na transferência; corrigido no plano: a
  transferência tem descrição opcional desde a spec 006).
- Q: O que o consumidor de notificações de transferência faz nesta spec? → A: Simula a
  notificação com um log estruturado e registra o evento em `eventos_processados`; não há envio
  real (e-mail, push) nem tabela de notificações.
- Q: Onde roda o consumidor? → A: Em um processo próprio (Worker), separado da API, com
  container próprio no ambiente local; assim o trace mostra dois serviços.
- Q: Ao esgotar as tentativas, como a mensagem chega à DLQ? → A: O consumidor a republica na
  `banco.eventos.dlx` (routing key `*.dlq` da fila de origem) com os headers de diagnóstico
  `x-tentativas`, `x-ultimo-erro`, `x-motivo` e `x-falhou-em`, com confirmação do broker, e só
  então dá ACK na original. A fila principal mantém `x-dead-letter-exchange` como rede de
  segurança: um NACK sem requeue também chega à DLQ (só com `x-death`).
- Q: Quantas novas tentativas antes da DLQ, e com quais intervalos? → A: 3 novas tentativas,
  após 1 s, 2 s e 4 s (4 execuções no total), como no OMS, por filas de espera com TTL por
  consumidor (`<fila>.espera.1000`, `.espera.2000`, `.espera.4000`) que devolvem a mensagem à
  fila principal. Mensagens inválidas vão direto para a DLQ, sem nova tentativa.
- Q: Uma mensagem reprocessada da DLQ recomeça a contagem de tentativas? → A: Sim. A mensagem
  que chega à fila principal com `x-falhou-em` (header que só a cópia da DLQ tem) recomeça do
  zero (ganha 3 novas tentativas) e tem `x-reprocessamentos` incrementado, que acompanha a
  mensagem até a espera e a DLQ.

## User Scenarios & Testing *(mandatory)*

Os "usuários" desta spec são os titulares que recebem a notificação de uma transferência e a
equipe que opera o Bank (que precisa diagnosticar falhas e reprocessar mensagens). O motor de
transferências e o outbox já existem (spec 006); o RabbitMQ já sobe no ambiente local (spec 001).

### User Story 1 - Transferência concluída gera notificação (Priority: P1)

Uma Pessoa Física conclui uma transferência. Pouco depois, o serviço de notificações recebe o
evento `transferencia.concluida` e registra a notificação para pagador e recebedor, sem que a
transferência espere por isso.

**Why this priority**: é o fluxo que justifica toda a mensageria; sem ele os eventos ficam
parados no outbox.

**Independent Test**: com API, Worker, PostgreSQL e RabbitMQ no ar, fazer uma transferência e
verificar que o registro do outbox fica marcado como publicado, que a mensagem passou pela fila
`notificacoes.transferencias.fila` e que o Worker registrou a notificação e o evento em
`eventos_processados`.

**Acceptance Scenarios**:

1. **Given** uma transferência concluída, **When** o publicador processa o outbox, **Then** a
   mensagem é publicada em `banco.eventos.topic` com routing key `transferencia.concluida`, o
   broker confirma o recebimento e o registro do outbox recebe a data de publicação.
2. **Given** a mensagem publicada, **When** o Worker a consome com sucesso, **Then** ele registra
   a notificação (log estruturado), grava `(eventoId, nome do consumidor)` em
   `eventos_processados` e só então confirma (ACK) a mensagem.
3. **Given** uma transferência concluída, **When** a resposta HTTP é enviada ao cliente,
   **Then** ela não depende da publicação nem do consumo (a resposta não espera o broker).

---

### User Story 2 - Nenhum evento se perde quando o broker falha (Priority: P1)

O RabbitMQ fica indisponível (ou não confirma uma publicação) enquanto transferências acontecem.
As transferências continuam sendo concluídas normalmente e, quando o broker volta, todos os
eventos pendentes são publicados.

**Why this priority**: integridade financeira (Princípio IV): a operação nunca é desfeita por
falha de notificação, e o evento derivado nunca se perde.

**Independent Test**: parar o RabbitMQ, fazer 3 transferências (todas concluídas, 201), conferir
que os 3 eventos seguem pendentes no outbox, religar o RabbitMQ e verificar que os 3 são
publicados e consumidos.

**Acceptance Scenarios**:

1. **Given** o broker fora do ar, **When** uma PF transfere, **Then** a transferência é concluída
   normalmente e o evento permanece pendente no outbox.
2. **Given** eventos pendentes, **When** o broker volta, **Then** o publicador os publica sem
   intervenção manual, na ordem em que foram gravados.
3. **Given** uma publicação que o broker recusa ou não confirma no prazo, **When** o publicador
   trata o resultado, **Then** o evento continua pendente e é tentado de novo depois.
4. **Given** o RabbitMQ reiniciado com mensagens ainda não consumidas, **When** ele volta,
   **Then** exchanges, filas e mensagens continuam lá (tudo durável e persistente).

---

### User Story 3 - Entrega duplicada não repete a notificação (Priority: P1)

Por causa da garantia "pelo menos uma vez", a mesma mensagem pode chegar duas vezes ao Worker
(republicação após falha ao marcar o outbox, ou reentrega após queda do consumidor antes do
ACK). O titular recebe uma única notificação.

**Why this priority**: duplicidade de efeitos é defeito de correção; a idempotência no
consumidor é o que torna seguro republicar.

**Independent Test**: publicar duas vezes a mesma mensagem (mesmo `eventoId`) e verificar uma
única notificação registrada, uma única linha em `eventos_processados` e a métrica com um
`sucesso` e um `duplicado`.

**Acceptance Scenarios**:

1. **Given** um evento já registrado em `eventos_processados` para o consumidor, **When** a
   mesma mensagem chega de novo, **Then** o Worker não registra outra notificação, confirma (ACK)
   a mensagem e conta o processamento como `duplicado`.
2. **Given** duas entregas simultâneas do mesmo evento, **When** o Worker processa as duas,
   **Then** apenas uma gera notificação.

---

### User Story 4 - Falha passageira é tentada de novo; falha permanente vai para a DLQ (Priority: P2)

Uma mensagem válida falha durante o processamento (por exemplo, o banco do Worker cai por um
instante). Ela é tentada de novo depois de 1 s, 2 s e 4 s, sem bloquear as demais mensagens da
fila; se ainda falhar, vai para a DLQ correspondente com o motivo e o último erro. Uma mensagem
que nunca poderá ser processada (JSON inválido, tipo ou versão desconhecidos, campo obrigatório
ausente) vai direto para a DLQ. A operação inspeciona a DLQ e reprocessa à mão.

**Why this priority**: falhas passageiras se resolvem sozinhas e o loop de "poison message",
que travaria a fila, é impossível; só entra em ação em falhas.

**Independent Test**: (a) com um processamento que falha nas 2 primeiras execuções, verificar que
a notificação é registrada na 3ª, uma única vez; (b) com um que sempre falha, verificar que a
mensagem chega a `notificacoes.transferencias.dlq` após 4 execuções (~7 s), com
`x-tentativas` = 4 e `x-motivo` = `tentativas-esgotadas`; (c) com payload inválido, verificar
que ela chega à DLQ após 1 execução, com `x-motivo` = `mensagem-invalida`.

**Acceptance Scenarios**:

1. **Given** uma falha no processamento de uma mensagem válida, **When** é a 1ª, 2ª ou 3ª
   execução, **Then** a mensagem vai para a fila de espera de 1 s, 2 s ou 4 s, respectivamente,
   volta sozinha à fila principal e as outras mensagens continuam sendo consumidas enquanto isso.
2. **Given** uma falha na 4ª execução, **When** o Worker trata o erro, **Then** a mensagem vai
   para a DLQ com os headers de diagnóstico, o evento não é registrado em `eventos_processados` e
   o erro é logado com o `eventoId` (sem o payload).
3. **Given** uma mensagem com payload que não desserializa, tipo ou versão desconhecidos ou campo
   obrigatório ausente, **When** o Worker a recebe, **Then** ela vai direto para a DLQ, sem nova
   tentativa.
4. **Given** uma mensagem na DLQ, **When** a operação a move de volta para a fila principal
   (pela interface de gestão do RabbitMQ) depois de corrigida a causa, **Then** ela é processada
   normalmente, com a contagem de tentativas recomeçada e `x-reprocessamentos` = 1.
5. **Given** uma mensagem reprocessada que volta a falhar sempre, **When** esgota as 4 execuções,
   **Then** volta à DLQ com `x-tentativas` = 4 e `x-reprocessamentos` = 1, sem loop.

---

### User Story 5 - Trace ponta a ponta da transferência à notificação (Priority: P2)

Ao investigar uma transferência, a equipe abre o trace da requisição `POST /transferencias` e
vê, na mesma árvore, a publicação do evento e o processamento no Worker.

**Why this priority**: diagnóstico ponta a ponta é exigido pelo Princípio VI; depende das
histórias anteriores para ter o que rastrear.

**Independent Test**: fazer uma transferência e, no coletor de telemetria, encontrar um único
trace com os spans da requisição HTTP, da publicação (Producer) e do consumo (Consumer), este
último no serviço do Worker.

**Acceptance Scenarios**:

1. **Given** uma transferência feita dentro de um trace, **When** o evento é publicado (mesmo que
   minutos depois, após indisponibilidade do broker), **Then** a mensagem carrega nos headers o
   `traceparent` (e `tracestate`, se houver) do trace da transferência.
2. **Given** uma mensagem com `traceparent`, **When** o Worker a processa, **Then** o span de
   consumo pertence ao mesmo trace e tem como pai o span de publicação.
3. **Given** uma mensagem sem `traceparent` (por exemplo, republicada à mão), **When** o Worker a
   processa, **Then** o processamento acontece normalmente em um novo trace.

---

### Edge Cases

- **Falha entre publicar e marcar o outbox**: o broker confirmou, mas a gravação de
  `publicado_em` falhou. O evento é publicado de novo na próxima rodada; a idempotência do
  consumidor (US3) absorve a duplicata.
- **Vários publicadores ao mesmo tempo** (mais de uma instância da API): um mesmo evento
  pendente não deve ser publicado em paralelo por duas instâncias; se acontecer, a idempotência
  do consumidor absorve.
- **Mensagem sem rota**: publicação com routing key sem fila vinculada é tratada como falha de
  publicação (o evento fica pendente e gera log de erro), nunca descartada em silêncio.
- **Versão de schema desconhecida** (`versao` maior que a suportada): a mensagem vai para a DLQ.
- **Campos novos desconhecidos** no payload: são ignorados pelo consumidor (compatibilidade
  retroativa da versão 1).
- **Eventos antigos no outbox**, gravados antes desta spec, sem nomes nem contexto de trace:
  são publicados com os campos que têm (nomes ausentes) e em um novo trace; o consumidor aceita
  a ausência dos nomes.
- **Worker parado**: as mensagens se acumulam na fila durável e são consumidas quando ele volta.
- **Banco indisponível para o Worker**: o processamento falha e a mensagem é tentada de novo
  após 1 s, 2 s e 4 s; se o banco não voltar nesse tempo, ela vai para a DLQ e a recuperação é o
  reprocessamento manual.
- **Sucesso já gravado, mas ACK perdido** (queda do Worker entre o registro em
  `eventos_processados` e o ACK): a reentrega é tratada como duplicada (US3), sem nova
  notificação.
- **Worker cai com a mensagem na fila de espera**: a fila de espera é durável; ao expirar, a
  mensagem volta à fila principal e é consumida quando o Worker voltar.
- **`CobrancaPaga`**: a fila e a DLQ de cobranças existem desde já, mas nenhum evento é
  publicado nem consumido até a spec de cobranças (Fase 4).

## Requirements *(mandatory)*

### Functional Requirements

**Publicação (outbox → broker)**

- **FR-001**: Eventos de integração MUST ser publicados no broker apenas a partir do outbox, ou
  seja, depois do commit da transação que os gravou. A operação de negócio MUST NOT publicar
  direto no broker nem depender do resultado da publicação (RN-01).
- **FR-002**: Um publicador em segundo plano MUST ler os eventos pendentes do outbox
  (`publicado_em` nulo) em ordem de gravação, publicá-los e, somente após a confirmação do broker
  (publisher confirm), registrar `publicado_em`. Publicação recusada, sem rota ou sem confirmação
  no prazo MUST deixar o evento pendente para nova tentativa.
- **FR-003**: O publicador MUST tentar de novo os pendentes periodicamente e se reconectar
  sozinho ao broker quando ele voltar, sem reiniciar a aplicação.
- **FR-004**: Um mesmo evento pendente MUST NOT ser publicado em paralelo por duas instâncias do
  publicador.
- **FR-005**: O registro do outbox MUST guardar o contexto de trace ativo no momento da gravação
  (`traceparent` e `tracestate`), para que a publicação, mesmo tardia, continue o trace da
  operação de origem.

**Topologia**

- **FR-006**: A topologia MUST ser:

  | Exchange | Tipo | Uso |
  | --- | --- | --- |
  | `banco.eventos.topic` | topic, durável | Principal: todos os eventos de domínio concluídos |
  | `banco.eventos.dlx` | topic, durável | Dead letter exchange das filas principais |

  | Fila (durável) | Exchange | Routing key | Dead letter |
  | --- | --- | --- | --- |
  | `notificacoes.transferencias.fila` | `banco.eventos.topic` | `transferencia.concluida` | `banco.eventos.dlx` / `transferencia.concluida.dlq` |
  | `notificacoes.transferencias.espera.1000` / `.2000` / `.4000` | — (publicação direta pela exchange padrão) | nome da fila | TTL de 1 s / 2 s / 4 s; ao expirar, exchange padrão → `notificacoes.transferencias.fila` |
  | `notificacoes.transferencias.dlq` | `banco.eventos.dlx` | `transferencia.concluida.dlq` | — |
  | `notificacoes.cobrancas.fila` | `banco.eventos.topic` | `cobranca.paga` | `banco.eventos.dlx` / `cobranca.paga.dlq` |
  | `notificacoes.cobrancas.dlq` | `banco.eventos.dlx` | `cobranca.paga.dlq` | — |

  As filas de espera existem por consumidor: as de `notificacoes.cobrancas.fila` serão
  declaradas pela spec que criar o consumidor de cobranças.

- **FR-007**: A topologia MUST ser declarada automaticamente pelo Worker, que hospeda o
  publicador e o consumidor (a API não acessa o broker), a cada canal aberto e de forma
  idempotente, sem passo manual no ambiente.
- **FR-008**: Exchanges e filas MUST ser duráveis e toda mensagem MUST ser publicada como
  persistente (RN-03).
- **FR-009**: A comunicação com o broker MUST usar o cliente oficial do RabbitMQ diretamente,
  com confirmação de publicação e ACK/NACK manuais; frameworks de abstração de mensageria não são
  permitidos (RN-02; constituição, Restrições Técnicas).

**Contratos**

- **FR-010**: Toda mensagem MUST ter corpo JSON (camelCase, UTF-8) e propriedades AMQP:
  `content-type` `application/json`, `content-encoding` `utf-8`, `message-id` igual a
  `eventoId`, `type` igual ao tipo do evento (`transferencia.concluida`), `timestamp` igual a
  `ocorridoEm`, entrega persistente, e headers `versao` (versão do schema) e, quando houver,
  `traceparent`/`tracestate`.
- **FR-011**: O evento `transferencia.concluida` MUST continuar na versão 1 e passar a conter,
  além dos campos atuais (`eventoId`, `transferenciaId`, `pagadorUsuarioId`,
  `recebedorUsuarioId`, `valor`, `ocorridoEm`), os campos `tipoEvento`
  (`"TransferenciaConcluida"`), `versao` (`1`), `pagadorNome` e `recebedorNome` (nome da PF ou
  razão social/nome do titular PJ no momento da transferência) e `descricao` (descrição opcional
  da transferência; `null` quando ausente). Os campos novos são opcionais para o consumidor
  (eventos antigos não os têm).
- **FR-012**: O contrato `cobranca.paga` v1 MUST ser documentado (campos `eventoId`,
  `tipoEvento` `"CobrancaPaga"`, `versao`, `cobrancaId`, `lojistaUsuarioId`, `pagadorUsuarioId`,
  `valor`, `ultimosDigitosCartao` — só os 4 últimos dígitos — e `ocorridoEm`), como preparação
  para a spec de cobranças; nada o publica nem o consome nesta spec.
- **FR-013**: Os contratos MUST ficar em `contracts/` desta spec, e o contrato do evento da spec
  006 MUST ser atualizado (histórico da v1) para refletir os campos novos.
- **FR-014**: Identificadores trafegam como UUID nos eventos, sem expor os tipos do domínio, e
  valores monetários como número em BRL com 2 casas.

**Consumo, idempotência e DLQ**

- **FR-015**: O consumo MUST acontecer em um processo próprio (Worker), separado da API, que
  consome `notificacoes.transferencias.fila` com ACK manual e um número limitado de mensagens em
  processamento simultâneo (prefetch).
- **FR-016**: Para cada mensagem válida, o Worker MUST registrar a notificação para pagador e
  recebedor em log estruturado, registrar o par `(eventoId, nome do consumidor)` em
  `eventos_processados` e confirmar (ACK) a mensagem somente depois disso.
- **FR-017**: Se o par `(eventoId, nome do consumidor)` já existir, o Worker MUST confirmar a
  mensagem sem repetir a notificação, inclusive sob entregas simultâneas (RN-05).
- **FR-018**: Mensagens enviadas à DLQ (RN-04) MUST ser republicadas pelo consumidor na
  `banco.eventos.dlx`, com a routing key de DLQ da fila de origem, o corpo e as propriedades
  originais (incluindo `message-id` e headers de trace) e os headers de diagnóstico
  `x-tentativas` (execuções feitas), `x-ultimo-erro` (tipo e mensagem do erro, sem payload nem
  dados sensíveis), `x-motivo` (`mensagem-invalida` ou `tentativas-esgotadas`) e `x-falhou-em`
  (instante UTC). A original só recebe ACK depois da confirmação do broker; se a republicação
  falhar, a original recebe NACK sem requeue e chega à DLQ pela dead letter exchange da fila
  (rede de segurança, só com `x-death`). Nenhuma mensagem volta à fila principal em loop.
- **FR-018a**: Falhas no processamento de uma mensagem válida MUST ter 3 novas tentativas, após
  1 s, 2 s e 4 s (4 execuções no total). Na falha da execução N (N ≤ 3), o consumidor MUST
  republicar a mensagem na fila de espera do N-ésimo intervalo, com o header `x-tentativas` = N
  e o corpo e as propriedades originais, e dar ACK na original só após a confirmação do broker.
  Ao expirar o TTL, a fila de espera devolve a mensagem à fila principal. Na falha da 4ª
  execução, a mensagem vai para a DLQ com `x-motivo` = `tentativas-esgotadas` (FR-018).
- **FR-018b**: Mensagens inválidas (corpo que não desserializa, tipo ou versão de schema
  desconhecidos, campo obrigatório ausente) MUST ir direto para a DLQ, com `x-motivo` =
  `mensagem-invalida`, sem nova tentativa.
- **FR-018c**: Uma mensagem que chega à fila principal com o header `x-falhou-em` (cópia da DLQ
  reprocessada manualmente) MUST recomeçar a contagem: o `x-tentativas` anterior é ignorado e
  `x-reprocessamentos` é incrementado (1 se ausente). Ao republicar na fila de espera, o
  consumidor MUST remover `x-falhou-em`, `x-motivo` e `x-ultimo-erro` (para que o retorno da
  espera não seja confundido com um novo reprocessamento) e MUST manter `x-reprocessamentos`,
  que também segue para a DLQ.
- **FR-019**: A tabela `eventos_processados` MUST ter `evento_id` (uuid), `nome_consumidor`
  (texto até 100 caracteres) e `processado_em` (data e hora UTC), com chave primária composta
  por `evento_id` e `nome_consumidor`, criada por migration versionada.

**Observabilidade**

- **FR-020**: A publicação MUST gerar um span do tipo Producer chamado
  `banco.eventos.topic publish`, filho do contexto guardado no outbox (FR-005), com os atributos
  `messaging.system` = `rabbitmq`, `messaging.destination.name` = `banco.eventos.topic`,
  `messaging.rabbitmq.destination.routing_key` e `messaging.message.id`; e MUST injetar o
  contexto desse span nos headers da mensagem no padrão W3C Trace Context (RN-06).
- **FR-021**: O consumo MUST gerar um span do tipo Consumer chamado
  `notificacoes.transferencias.fila process`, cujo pai é o contexto extraído dos headers da
  mensagem pelo propagador padrão do OpenTelemetry; sem headers, um novo trace.
- **FR-022**: O Worker MUST expor o contador de eventos processados (exportado como
  `banco_eventos_processados_total`) com as tags `tipo_evento` e `status` (`sucesso`,
  `duplicado`, `nova_tentativa`, `falha_dlq`); `falha_dlq` também carrega a tag `motivo`
  (`mensagem-invalida` ou `tentativas-esgotadas`). O publicador MUST expor métricas de eventos publicados, falhas de
  publicação e quantidade de pendentes no outbox.
- **FR-023**: Logs, spans e métricas MUST NOT conter nomes, descrição, o payload, documentos
  ou dados de cartão; usam `eventoId`, tipo do evento e identificadores técnicos. As tags de
  métrica MUST NOT ter identificadores de usuário ou de evento.
- **FR-024**: O Worker MUST exportar traces, métricas e logs via OTLP como a API, com nome de
  serviço próprio, e ter healthcheck no ambiente local (Docker Compose).

**Qualidade**

- **FR-025**: Os contratos MUST ter testes de serialização e conformidade de schema; publicação,
  consumo, idempotência, DLQ e propagação de trace MUST ter testes de integração com RabbitMQ e
  PostgreSQL reais em contêiner.

### Key Entities

- **Evento do outbox** (existente, `outbox_eventos`): evento gravado na transação da operação;
  ganha o contexto de trace da gravação. Só `publicado_em` é atualizado depois da gravação.
- **Mensagem de integração**: o evento publicado no broker — corpo JSON do contrato e
  propriedades/headers AMQP (id, tipo, versão, trace).
- **Evento processado** (`eventos_processados`): marca que um consumidor já tratou um evento;
  chave `(evento_id, nome_consumidor)`, com `processado_em`.
- **Topologia**: exchanges, filas, bindings e DLQs da FR-006.
- **Notificação de transferência**: efeito do consumo (log estruturado), não persistido como
  entidade nesta spec. Desde a
  [spec 011](../011-notificacoes-auditoria/spec.md) também é persistida: comprovante de envio
  (pagador) e aviso de crédito (recebedor) em `notificacoes_auditoria`, na mesma transação de
  `eventos_processados`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Com todos os serviços no ar, 95% das transferências concluídas têm a notificação
  registrada pelo Worker em até 5 segundos.
- **SC-002**: Com o broker fora do ar por qualquer período, 100% das transferências continuam
  sendo concluídas e 100% dos eventos gravados nesse período são publicados e consumidos depois
  que o broker volta, sem ação manual.
- **SC-003**: Entregando a mesma mensagem 2 ou mais vezes (em sequência ou em paralelo), cada
  evento gera exatamente 1 notificação.
- **SC-004**: 100% das mensagens inválidas terminam na DLQ após uma única execução, e 100% das
  que falham sempre terminam na DLQ após exatamente 4 execuções (em até ~10 s); nenhuma fica em
  loop.
- **SC-005**: Após reiniciar o broker, 0 mensagens não consumidas são perdidas.
- **SC-006**: Para 100% das transferências feitas com o trace ativo, o coletor mostra um único
  trace com os spans da requisição, da publicação e do consumo.
- **SC-007**: O tempo de resposta das transferências não piora de forma perceptível: 95%
  continuam respondendo em menos de 1 segundo no ambiente local, com o broker no ar ou fora.
- **SC-008**: Uma falha passageira de até 7 s no processamento é absorvida pelas novas
  tentativas: a notificação é registrada uma única vez, sem ação manual.

## Assumptions

- **Escopo**: issue #20 (TASK-09); a issue cita `specs/03-mensageria-rabbitmq.md`, mas segue a
  numeração do Spec Kit (`specs/010-mensageria-rabbitmq/`).
- **Outbox existente**: a gravação no outbox (spec 006) é mantida; esta spec acrescenta o
  contexto de trace ao registro, os nomes ao payload e o publicador.
- **Cobranças**: o evento `cobranca.paga` e sua fila são preparação para a Fase 4; o consumidor
  de cobranças fica para aquela spec.
- **Notificação simulada** (Clarifications): não há envio real nem preferências de notificação;
  o log estruturado é o "envio".
- **Novas tentativas no consumidor** (Clarifications): modelo do Order-Management-System —
  filas de espera com TTL por consumidor e DLQ com headers de diagnóstico. Toda exceção no
  processamento de uma mensagem válida é tratada como possivelmente passageira; não há pausa do
  consumo por dependência indisponível nem circuit breaker nesta spec. O reprocessamento da DLQ é
  manual, pela interface de gestão do RabbitMQ (sem endpoint de reenvio).
- **Ordem**: a publicação segue a ordem de gravação, mas o consumo não garante ordem entre
  eventos; os consumidores não dependem dela.
- **Nomes no evento**: o nome do titular não é dado sensível segundo a constituição
  (Princípio VI), mas ainda assim não vai para logs, spans nem métricas (FR-023).
- **Retenção**: `eventos_processados` e eventos publicados do outbox não são expurgados nesta
  spec.
- **Configuração**: conexão com o broker (host, vhost, credenciais) vem de configuração/
  variáveis de ambiente, como no `docker-compose.yml` atual; segredos não são versionados.
- **Moeda**: BRL, como nas specs 003 e 006.
