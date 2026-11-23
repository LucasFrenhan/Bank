# Feature Specification: Notificações de transferência persistidas (auditoria)

**Feature Branch**: `011-notificacoes-auditoria`

**Created**: 2026-11-23

**Status**: Draft

**Input**: Issue #21 ("Mensageria Nativa RabbitMQ (Publisher, Consumer Worker, DLQ e
OpenTelemetry)", TASK-08), complementar à issue #20 / spec 010-mensageria-rabbitmq (PR #47).
A spec 010 já entregou quase tudo o que a issue #21 pede: cliente nativo, topologia, publisher
confirms, publicação pelo outbox, Worker com ACK manual e prefetch 10, `eventos_processados`,
novas tentativas, DLQ, trace W3C e a métrica `banco_eventos_processados_total`. Ficou de fora,
por decisão da spec 010 (Clarifications: "não há envio real nem tabela de notificações"), a
persistência das notificações: hoje o consumidor só escreve um log estruturado por
destinatário. Esta spec cobre **somente essa lacuna**: o consumidor de notificações de
transferência passa a gravar, na mesma transação do registro em `eventos_processados`, uma
notificação para o pagador (comprovante de envio) e outra para o recebedor (aviso de crédito)
na tabela `notificacoes_auditoria`. Todo o desenho da spec 010 é mantido.

## Clarifications

### Session 2026-11-23

- Q: Cada notificação deve ter um campo `tipo` (`comprovante_envio` / `aviso_credito`), além
  dos campos da issue, com a regra de que um evento não pode ter duas notificações do mesmo
  tipo? → A: Sim: campo `tipo` e unicidade por `(evento_id, tipo)`.
- Q: Qual valor o campo `canal` deve ter enquanto não existe envio real? → A: `simulado`;
  canais reais (`email`, `push`) entram como valores novos quando houver envio.
- Q: O texto gravado deve incluir o nome da outra parte ou só o valor? → A: Valor e nome da
  outra parte quando o evento trouxer; sem nome, só o valor; nunca a descrição.
- Q: O destinatário deve ser obrigatoriamente um usuário cadastrado (chave estrangeira) ou só
  o identificador do evento? → A: Chave estrangeira para `usuarios`, sem exclusão em cascata
  (`RESTRICT`).

## User Scenarios & Testing *(mandatory)*

Os "usuários" desta spec são os titulares (pagador e recebedor) a quem cada notificação se
destina e a equipe que opera o Bank, que precisa comprovar quais notificações foram geradas
para cada transferência (auditoria e atendimento). O fluxo de mensageria (outbox → broker →
Worker), a idempotência e a política de falhas já existem (spec 010).

### User Story 1 - Transferência concluída gera duas notificações registradas (Priority: P1)

Uma Pessoa Física conclui uma transferência. Pouco depois, o Worker consome o evento
`transferencia.concluida` e registra, de forma permanente, o comprovante de envio para o
pagador e o aviso de crédito para o recebedor, com o texto da notificação e o instante do
registro.

**Why this priority**: é o objetivo da spec: sem o registro persistido, não há como comprovar
depois que a notificação foi gerada (hoje ela só existe no log, que tem retenção limitada).

**Independent Test**: com API, Worker, PostgreSQL e RabbitMQ no ar, fazer uma transferência
pela API e verificar que, após o consumo, existem exatamente duas notificações para o evento:
uma destinada ao pagador (comprovante de envio) e outra ao recebedor (aviso de crédito).

**Acceptance Scenarios**:

1. **Given** uma transferência concluída e o evento publicado, **When** o Worker o consome com
   sucesso, **Then** existem duas notificações para o `eventoId`: o comprovante de envio,
   destinado ao pagador, e o aviso de crédito, destinado ao recebedor, cada uma com canal,
   mensagem e data de criação.
2. **Given** o evento consumido com sucesso, **When** se consultam `eventos_processados` e as
   notificações, **Then** o registro do evento processado e as duas notificações foram gravados
   juntos (mesma transação) e a mensagem só recebeu ACK depois disso.
3. **Given** um evento com os nomes de pagador e recebedor, **When** as notificações são
   registradas, **Then** a mensagem do pagador cita o valor e o nome do recebedor, e a do
   recebedor cita o valor e o nome do pagador.
4. **Given** um evento sem os nomes (evento antigo, anterior à spec 010), **When** as
   notificações são registradas, **Then** as mensagens citam o valor sem o nome da outra parte,
   e o consumo não falha.

---

### User Story 2 - Entregas repetidas não duplicam as notificações (Priority: P1)

Por causa da garantia "pelo menos uma vez", a mesma mensagem pode chegar duas ou mais vezes ao
Worker, em sequência ou ao mesmo tempo. Cada titular continua com uma única notificação por
transferência.

**Why this priority**: notificação duplicada é defeito de correção e polui a auditoria; a
idempotência é o que torna seguro republicar.

**Independent Test**: publicar a mesma mensagem (mesmo `eventoId`) duas vezes, e também
entregá-la a dois consumidores ao mesmo tempo, e verificar que existem exatamente duas
notificações para o evento (uma por destinatário) e uma única linha em `eventos_processados`.

**Acceptance Scenarios**:

1. **Given** um evento já processado, **When** a mesma mensagem chega de novo, **Then** nenhuma
   notificação nova é gravada e a mensagem é confirmada como `duplicado` (comportamento da spec
   010).
2. **Given** duas entregas simultâneas do mesmo evento, **When** o Worker processa as duas,
   **Then** existem exatamente duas notificações para o evento.
3. **Given** que o Worker gravou as notificações mas caiu antes do ACK, **When** a mensagem é
   reentregue, **Then** ela é tratada como duplicada, sem notificações novas.

---

### User Story 3 - Falha no registro não deixa notificação pela metade (Priority: P2)

O registro das notificações falha (por exemplo, o banco do Worker cai no meio da gravação).
Nada fica gravado pela metade: nem o evento processado nem uma notificação isolada. A mensagem
segue a política de novas tentativas e DLQ já existente e, quando finalmente processada (por
nova tentativa ou reprocessamento manual da DLQ), gera as duas notificações uma única vez.

**Why this priority**: garante a consistência entre "evento processado" e "notificações
registradas"; só entra em ação em falhas.

**Independent Test**: (a) com o registro falhando nas 2 primeiras execuções, verificar que após
a 3ª existem exatamente duas notificações; (b) com o registro falhando sempre, verificar que a
mensagem chega à DLQ sem nenhuma notificação nem linha em `eventos_processados` para o evento;
depois, corrigida a causa e reprocessada a mensagem da DLQ, verificar exatamente duas
notificações.

**Acceptance Scenarios**:

1. **Given** uma falha ao gravar a segunda notificação, **When** o Worker trata o erro, **Then**
   a primeira notificação e o registro em `eventos_processados` também são desfeitos, e a
   mensagem vai para a fila de espera ou para a DLQ conforme a política da spec 010.
2. **Given** uma mensagem que falhou e foi tentada de novo, **When** uma execução posterior tem
   sucesso, **Then** existem exatamente duas notificações para o evento.
3. **Given** uma mensagem na DLQ reprocessada manualmente, **When** é processada com sucesso,
   **Then** existem exatamente duas notificações para o evento, mesmo que ele tenha falhado
   várias vezes antes.

---

### Edge Cases

- **Eventos processados antes desta spec**: eventos que já estão em `eventos_processados` não
  ganham notificações retroativas (sem carga histórica); se forem reentregues, continuam
  tratados como duplicados.
- **Nomes ausentes no evento**: a mensagem é montada sem o nome da outra parte (US1, cenário 4).
- **Descrição da transferência**: não entra no texto da notificação (pode conter dados livres
  digitados pelo titular).
- **Valores**: o texto exibe o valor em reais no formato brasileiro (por exemplo,
  `R$ 1.234,56`), sem arredondamento além das 2 casas do evento.
- **Mensagem inválida**: vai direto para a DLQ como na spec 010 e não gera notificação.
- **Destinatário inexistente** (evento com um `pagadorUsuarioId` ou `recebedorUsuarioId` que
  não está em `usuarios`): o registro falha por integridade referencial, nada é gravado e a
  mensagem segue as novas tentativas e, ao esgotá-las, vai para a DLQ com
  `x-motivo` = `tentativas-esgotadas` (spec 010).
- **Exclusão de usuário**: um usuário com notificações não pode ser excluído enquanto elas
  existirem (sem exclusão em cascata); hoje não há exclusão de usuários no sistema.
- **Log e métrica atuais**: o log estruturado por destinatário e a métrica
  `bank.notificacoes.enviadas` continuam existindo; não são substituídos pela tabela.

## Requirements *(mandatory)*

### Functional Requirements

**Registro das notificações**

- **FR-001**: Para cada evento `transferencia.concluida` processado com sucesso pelo consumidor
  de notificações de transferência, o sistema MUST registrar exatamente duas notificações:
  - **comprovante de envio**, destinado ao pagador (`pagadorUsuarioId`);
  - **aviso de crédito**, destinado ao recebedor (`recebedorUsuarioId`).
- **FR-002**: Cada notificação MUST conter: identificador próprio; `eventoId` de origem;
  usuário destinatário; tipo (`comprovante_envio` ou `aviso_credito`); canal; texto da mensagem;
  e data/hora de criação em UTC (o instante do processamento).
- **FR-003**: O canal MUST ser `simulado` enquanto não houver envio real (e-mail, push); o
  campo existe para que canais reais entrem depois sem mudar o modelo.
- **FR-004**: O texto da mensagem MUST ser em português e:
  - para o pagador: informar o valor enviado e, quando o evento trouxer, o nome do recebedor
    (por exemplo, "Você enviou R$ 150,00 para Maria Souza.");
  - para o recebedor: informar o valor recebido e, quando o evento trouxer, o nome do pagador
    (por exemplo, "Você recebeu R$ 150,00 de João Silva.");
  - MUST NOT conter a descrição da transferência, documentos ou dados de cartão.
- **FR-005**: O registro das duas notificações MUST ocorrer na mesma transação do registro do
  par `(eventoId, nome do consumidor)` em `eventos_processados`, e a mensagem MUST receber ACK
  somente após o commit. Se qualquer parte falhar, nada é gravado e a mensagem segue a política
  de novas tentativas e DLQ da spec 010 (FR-018 a FR-018c).

**Idempotência e integridade**

- **FR-006**: Um mesmo evento MUST NOT gerar mais de uma notificação de cada tipo, inclusive sob
  entregas repetidas ou simultâneas e após reprocessamento da DLQ. A garantia principal continua
  sendo `eventos_processados` (spec 010, FR-017); a tabela de notificações MUST também impedir,
  por restrição de unicidade em `(eventoId, tipo)`, duas notificações do mesmo tipo para o
  mesmo evento.
- **FR-007**: As notificações são registros de auditoria: MUST NOT ser alteradas nem apagadas
  depois de gravadas.

**Persistência**

- **FR-008**: As notificações MUST ser gravadas na tabela `notificacoes_auditoria`, com as
  colunas `id`, `evento_id`, `usuario_destino_id`, `tipo`, `canal`, `mensagem` e `criado_em`,
  criada por migration versionada. `usuario_destino_id` MUST referenciar `usuarios` por chave
  estrangeira, sem exclusão em cascata (`RESTRICT`).

**Compatibilidade com a spec 010**

- **FR-009**: Topologia, contratos (`transferencia.concluida` v1 sem mudanças), publicação pelo
  outbox, política de novas tentativas, DLQ, trace e métricas da spec 010 MUST permanecer como
  estão. O log estruturado por destinatário e a métrica `bank.notificacoes.enviadas` MUST ser
  mantidos.
- **FR-010**: Logs, spans e métricas MUST continuar sem nomes, descrição ou texto da mensagem
  (spec 010, FR-023). Os nomes podem aparecer apenas no texto persistido da notificação.
- **FR-011**: A spec 010 MUST ser atualizada (documentação viva) para apontar que a notificação
  de transferência passou a ser persistida por esta spec.

**Qualidade**

- **FR-012**: MUST haver teste unitário do caso de uso de notificação cobrindo as duas
  notificações geradas (tipo, destinatário e texto, com e sem nomes) e teste de idempotência:
  o mesmo `eventoId` processado duas vezes resulta em um único par de notificações.
- **FR-013**: MUST haver testes de integração (PostgreSQL e RabbitMQ reais em contêiner):
  ponta a ponta (`POST /api/v1/transferencias` → publicado → consumido → duas linhas em
  `notificacoes_auditoria`), entregas repetidas e simultâneas, falha com rollback e
  reprocessamento da DLQ. Os testes da spec 010 que verificam a notificação MUST ser ajustados
  para verificar também o registro persistido.

### Key Entities

- **Notificação de auditoria** (`notificacoes_auditoria`, nova): registro imutável de uma
  notificação gerada para um titular a partir de um evento. Atributos: identificador,
  `eventoId`, usuário destinatário (referência obrigatória a um usuário cadastrado), tipo (`comprovante_envio` / `aviso_credito`), canal
  (`simulado`), mensagem e data de criação. Cada evento de transferência gera exatamente duas.
- **Evento processado** (`eventos_processados`, existente, spec 010): continua sendo a garantia
  de idempotência do consumidor; agora é gravado junto com as duas notificações.
- **Evento `transferencia.concluida`** (existente, v1): fonte dos dados da notificação
  (identificadores, valor, nomes opcionais); não muda.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos eventos de transferência processados com sucesso depois desta spec têm
  exatamente duas notificações registradas, uma por destinatário.
- **SC-002**: Com todos os serviços no ar, 95% das transferências concluídas têm as duas
  notificações registradas em até 5 segundos (mesma meta da spec 010, SC-001).
- **SC-003**: Entregando a mesma mensagem 2 ou mais vezes (em sequência ou em paralelo), cada
  evento continua com exatamente 2 notificações.
- **SC-004**: Em 100% das falhas de registro, não sobra notificação isolada nem evento marcado
  como processado sem as suas duas notificações.
- **SC-005**: Após uma falha permanente seguida de reprocessamento manual da DLQ, o evento
  termina com exatamente 2 notificações.
- **SC-006**: O tempo de resposta das transferências não muda: o registro acontece no Worker,
  fora da requisição.

## Assumptions

- **Escopo**: issue #21 (TASK-08); a issue cita `specs/03-mensageria-rabbitmq.md`, mas segue a
  numeração do Spec Kit (`specs/011-notificacoes-auditoria/`). Os itens da issue #21 já
  entregues pela spec 010 (PR #47) são apenas marcados como concluídos na issue; esta spec não
  os reimplementa.
- **Diferenças em relação ao texto da issue**: a spec 010 decidiu, e esta spec mantém,
  publicação pelo outbox (em vez de publicar direto após o commit), novas tentativas antes da
  DLQ (em vez de NACK imediato) e os nomes atuais das classes (`ConexaoRabbitMq`, etc.).
- **Tipo da notificação**: a issue lista `Id`, `EventoId`, `UsuarioDestinoId`, `Canal`,
  `Mensagem` e `CriadoEm`; esta spec acrescenta `tipo` para distinguir comprovante de envio e
  aviso de crédito sem depender do texto, e para a restrição de unicidade (FR-006).
- **Notificação ainda simulada**: não há envio real nem preferências de notificação; o registro
  persistido é o "envio" (canal `simulado`).
- **Sem consulta exposta**: nenhum endpoint de consulta das notificações nesta spec; a consulta
  é feita direto no banco pela equipe.
- **Retenção**: as notificações não são expurgadas nesta spec.
- **Cobranças**: notificações de `cobranca.paga` ficam para a spec de cobranças (Fase 4).
- **Nomes**: o nome do titular não é dado sensível segundo a constituição (Princípio VI);
  ele pode ser persistido no texto da notificação, mas continua fora de logs, spans e métricas.
