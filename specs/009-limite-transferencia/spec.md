# Feature Specification: Limite de transferência

**Feature Branch**: `009-limite-transferencia`

**Created**: 2026-11-18

**Status**: Draft

**Input**: Issue #33 ("Regra de limite de transferência (por operação e diário)"). A regra ficou
fora da spec `006-transferencias` por decisão registrada no clarify daquela spec: lá, o único
limite é o saldo disponível. Pontos da issue: limite por operação e/ou diário por pagador (dia no
fuso de Brasília), valores configuráveis por ambiente e eventualmente por conta, erro próprio com
o limite disponível, interação com idempotência e concorrência, auditoria/métricas das recusas e
reflexo na tela Transferir (issue #32, entregue na spec 007). A regra deve servir de base para
limites futuros por modalidade (ex.: limite máximo por transação da TED, issue #31).

## Clarifications

### Session 2026-11-18

- Q: Limites personalizados por conta entram nesta spec? → A: Não; os limites vêm da
  configuração do ambiente e são iguais para todas as PF. Personalização por conta (inclusive a
  PF reduzir o próprio limite) fica para uma issue futura.
- Q: Quais os valores padrão dos limites? → A: Por operação R$ 1.000,00 e diário R$ 5.000,00,
  em todos os ambientes; os testes ajustam os valores pela configuração.
- Q: A recusa por limite usa um único `type` de erro ou um por tipo de limite? → A: Um único
  `type` `limite-excedido`, com as extensões `tipoLimite` (`PorOperacao` ou `Diario`), `limite` e
  `valorDisponivel`; novos limites acrescentam valores a `tipoLimite`, sem novo `type`.
- Q: Os limites aparecem em outra tela além de Transferir? → A: Não; só na tela Transferir (dica
  do campo valor e aviso de limite excedido nas etapas Dados e Confirmação). Início e Perfil não
  mudam; uma tela "Meus limites" fica para a issue de personalização por conta.

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes Pessoa Física autenticados, os únicos que podem transferir (spec 006,
FR-001). Hoje uma PF pode transferir qualquer valor que caiba no saldo. Esta spec acrescenta dois
tetos: um por operação e um diário (soma das transferências enviadas no dia). O motor de
transferências, a idempotência, o bloqueio das carteiras e as telas Transferir e Confirmação já
existem (specs 006 e 007).

### User Story 1 - Recusar transferência acima do limite por operação (Priority: P1)

Uma Pessoa Física tenta transferir, de uma só vez, um valor maior que o limite por operação. O
Bank recusa com um erro próprio que informa o limite, sem mexer em saldo nenhum.

**Why this priority**: é a proteção mais simples e a de maior efeito contra perdas por fraude ou
engano em uma única operação; não depende de histórico.

**Independent Test**: com limite por operação de R$ 1.000,00 e saldo suficiente, transferir
R$ 1.000,00 (concluída) e R$ 1.000,01 (recusada com o erro de limite, saldos inalterados).

**Acceptance Scenarios**:

1. **Given** limite por operação de R$ 1.000,00 e saldo de R$ 5.000,00, **When** a PF transfere
   R$ 1.000,00, **Then** a transferência é concluída (o limite é inclusivo).
2. **Given** o mesmo cenário, **When** a PF transfere R$ 1.000,01, **Then** recebe 422
   `limite-excedido` indicando que o limite é o por operação e que o valor máximo permitido é
   R$ 1.000,00, e nenhum saldo muda.
3. **Given** valor acima do limite por operação **e** acima do saldo, **When** a PF transfere,
   **Then** a recusa é por `limite-excedido` (o limite é verificado antes do saldo).

---

### User Story 2 - Recusar transferência que estoura o limite diário (Priority: P1)

Uma Pessoa Física já transferiu parte do limite diário. Uma nova transferência que faria a soma
do dia passar do limite é recusada com o valor que ainda pode ser enviado hoje. No dia seguinte
(meia-noite de Brasília), o limite volta a estar inteiro.

**Why this priority**: o limite diário é o que contém o prejuízo de uma conta comprometida, que
poderia fazer muitas transferências pequenas.

**Independent Test**: com limite diário de R$ 2.000,00, concluir R$ 1.500,00, tentar R$ 600,00
(recusada, disponível R$ 500,00), transferir R$ 500,00 (concluída) e conferir disponível R$ 0,00.

**Acceptance Scenarios**:

1. **Given** limite diário de R$ 2.000,00 e R$ 1.500,00 já transferidos hoje, **When** a PF
   transfere R$ 600,00, **Then** recebe 422 `limite-excedido` indicando o limite diário e
   R$ 500,00 disponíveis hoje, e nada muda.
2. **Given** o mesmo cenário, **When** a PF transfere R$ 500,00, **Then** a transferência é
   concluída e o disponível do dia passa a R$ 0,00.
3. **Given** R$ 2.000,00 transferidos até 23h59 de um dia (horário de Brasília), **When** a PF
   transfere às 00h00 do dia seguinte, **Then** o limite diário está inteiro de novo.
4. **Given** transferências recusadas (por qualquer motivo) no dia, **When** o disponível é
   calculado, **Then** elas não contam; só contam transferências concluídas enviadas pela PF.
5. **Given** transferências recebidas pela PF no dia, **When** o disponível é calculado,
   **Then** elas não aumentam nem reduzem o limite.

---

### User Story 3 - Limite consistente com concorrência e idempotência (Priority: P1)

Várias transferências simultâneas da mesma PF nunca ultrapassam juntas o limite diário, e
repetir uma transferência já concluída (mesma chave de idempotência) devolve o comprovante
original sem consumir limite de novo.

**Why this priority**: integridade financeira (Princípio IV); um limite que pode ser furado por
requisições paralelas não protege nada.

**Independent Test**: com limite diário de R$ 1.000,00 e saldo de R$ 5.000,00, disparar 5
transferências simultâneas de R$ 300,00 → exatamente 3 concluídas e 2 recusadas por
`limite-excedido`; reenviar uma das concluídas com a mesma chave → 200 com o comprovante
original, mesmo com o limite do dia já esgotado.

**Acceptance Scenarios**:

1. **Given** limite diário de R$ 1.000,00 e saldo de R$ 5.000,00, **When** 5 transferências de
   R$ 300,00 chegam ao mesmo tempo, **Then** 3 são concluídas, 2 recebem 422 `limite-excedido` e
   o total enviado no dia é R$ 900,00.
2. **Given** o limite do dia esgotado, **When** uma transferência já concluída é reenviada com a
   mesma chave e os mesmos dados, **Then** a resposta é 200 com o comprovante original e o
   disponível do dia não muda.
3. **Given** uma transferência concluída quando o limite era maior, **When** ela é reenviada
   depois de o limite ter sido reduzido, **Then** a resposta continua sendo o comprovante
   original (o replay não reavalia limite, como já não reavalia saldo).

---

### User Story 4 - Ver o limite antes de transferir (Priority: P2)

Na tela Transferir, a Pessoa Física vê quanto ainda pode enviar hoje e o máximo por operação,
e, se o Bank recusar por limite, vê um aviso claro com o valor disponível, para ajustar o valor
sem tentativa e erro.

**Why this priority**: melhora a experiência e evita recusas, mas a proteção (US1–US3) funciona
sem ela.

**Independent Test**: em desktop e mobile, abrir Transferir e conferir os limites exibidos;
informar um valor acima do disponível e conferir o aviso; o aviso também aparece quando a recusa
vem do Bank na confirmação.

**Acceptance Scenarios**:

1. **Given** uma PF autenticada, **When** consulta seus limites, **Then** recebe o limite por
   operação, o limite diário, o total já transferido hoje e o disponível hoje
   (diário − transferido, nunca negativo).
2. **Given** a etapa "Dados" da tela Transferir, **When** a tela carrega, **Then** a dica do
   campo valor mostra, além do saldo, o máximo que pode ser enviado agora (o menor entre o
   limite por operação e o disponível hoje).
3. **Given** um valor acima do que o limite permite, **When** a PF tenta continuar ou o Bank
   recusa por `limite-excedido` na confirmação, **Then** aparece o aviso "Limite excedido" com o
   tipo de limite e o valor disponível, e a PF pode ajustar o valor.
4. **Given** uma conta PJ autenticada, **When** consulta limites, **Then** recebe 403
   `acesso-negado` (PJ não transfere).

---

### Edge Cases

- Valor exatamente igual ao limite por operação ou exatamente igual ao disponível do dia:
  permitido.
- Valor que excede o limite por um centavo: 422 `limite-excedido`.
- Valor que excede limite e saldo ao mesmo tempo: `limite-excedido` (limites antes do saldo).
- Valor que cabe no limite mas não no saldo: `saldo-insuficiente`, como hoje.
- Transferência no limite da meia-noite: o dia de uma transferência é a data da sua conclusão no
  horário de Brasília (`America/Sao_Paulo`), não em UTC.
- Limite reduzido por configuração durante o dia, ficando abaixo do já transferido: disponível
  R$ 0,00 (nunca negativo); transferências já concluídas não são afetadas.
- Limite não configurado (ausente) para um tipo: aquele tipo não é aplicado, e a consulta indica
  "sem limite" para ele.
- Requisições simultâneas com a mesma chave de idempotência: comportamento da spec 006 inalterado
  (uma transferência; as demais recebem o comprovante); o limite é consumido uma única vez.
- Replay idempotente com o limite do dia já esgotado: 200 com o comprovante original.
- Consulta de limites indisponível na tela Transferir (erro de rede ou do Bank): a dica mostra só
  o saldo, a PF pode continuar e o Bank continua aplicando o limite no envio (recusa com o aviso
  de FR-014).
- Saldo de demonstração (R$ 10,00) menor que qualquer limite padrão: o saldo continua sendo o
  fator limitante nas contas novas; isso é esperado.

## Requirements *(mandatory)*

### Functional Requirements

**Regra de limite**

- **FR-001**: Toda transferência iniciada por Pessoa Física MUST respeitar dois limites: o
  **limite por operação** (valor máximo de uma transferência) e o **limite diário** (soma máxima
  das transferências concluídas enviadas pela PF no mesmo dia, incluindo a atual).
- **FR-002**: O dia do limite diário MUST ser o dia civil no horário de Brasília
  (`America/Sao_Paulo`): começa às 00h00 e termina às 23h59:59,999 locais, independentemente do
  horário UTC em que as transferências são gravadas.
- **FR-003**: Para o limite diário MUST contar apenas transferências **concluídas** e **enviadas**
  pela PF; recusas e transferências recebidas não contam.
- **FR-004**: Os limites são inclusivos: valor igual ao limite (ou igual ao disponível do dia) é
  permitido.
- **FR-005**: Os valores dos limites MUST ser configuráveis por ambiente e por perfil de conta
  (nesta spec, apenas Pessoa Física transfere), sem alteração de código. Os limites são
  iguais para todas as contas do mesmo perfil; um limite não configurado (ausente ou com valor
  vazio, que sobrescreve o padrão) não é aplicado.
- **FR-006**: Valores padrão, em todos os ambientes: limite por operação R$ 1.000,00 e limite
  diário R$ 5.000,00; testes ajustam os valores pela configuração.
- **FR-007**: A regra MUST ser modelada de forma que novos limites (por exemplo, por modalidade
  PIX/TED na issue #31, ou por perfil PJ) possam ser acrescentados sem mudar o contrato do erro
  nem a ordem das verificações.

**Ordem das verificações e concorrência**

- **FR-008**: A ordem das verificações de uma transferência MUST ser: validação da requisição
  (400) → idempotência (replay ou divergência) → destinatário (404) → autotransferência (422) →
  limite por operação (422) → limite diário (422) → saldo (422). Replays idempotentes não passam
  pelas verificações de limite.
- **FR-009**: A verificação do limite diário MUST acontecer com a carteira do pagador bloqueada
  contra alterações concorrentes (o mesmo bloqueio da verificação de saldo da spec 006), de modo
  que transferências simultâneas da mesma PF nunca somem, juntas, mais que o limite diário.
- **FR-010**: Uma transferência recusada por limite MUST NOT alterar saldos nem gerar
  transferência, lançamento, evento ou registro de idempotência (a mesma chave pode ser usada
  depois com outro valor).

**Erro**

- **FR-011**: A recusa por limite MUST retornar 422 com `ProblemDetails` de `type`
  `limite-excedido` (estável e documentado), `traceId`, uma mensagem em português e as extensões
  `tipoLimite` (`PorOperacao` ou `Diario`), `limite` (valor do limite aplicado) e
  `valorDisponivel` (máximo que poderia ser enviado agora por aquele limite). Consumidores usam
  `type` e as extensões, nunca o texto de `detail`. Limites futuros (FR-007) acrescentam valores a
  `tipoLimite`, sem criar outro `type`.

**Consulta de limites**

- **FR-012**: Pessoas Físicas autenticadas MUST poder consultar os próprios limites: limite por
  operação, limite diário, total transferido hoje e disponível hoje (limite diário − transferido,
  mínimo zero), com indicação de "sem limite" para os não configurados. PJ → 403 `acesso-negado`.

**Frontend**

- **FR-013**: A etapa "Dados" da tela Transferir MUST mostrar o máximo que pode ser enviado
  agora (menor entre limite por operação e disponível hoje) junto ao saldo, e MUST impedir
  continuar com valor acima dele, exibindo o aviso de limite, em desktop e mobile. Os limites
  aparecem apenas no fluxo de transferência; as telas Início e Perfil não mudam.
- **FR-014**: Quando o Bank recusar com `limite-excedido` (na confirmação), a tela MUST exibir o
  aviso "Limite excedido" com o tipo de limite e o valor disponível vindos do erro, e permitir
  voltar e ajustar o valor.

**Observabilidade e contrato**

- **FR-015**: A métrica de transferências recusadas por motivo (spec 006, FR-020) MUST incluir o
  motivo `limite-excedido`, diferenciado pelo tipo de limite (`PorOperacao`, `Diario`).
- **FR-016**: Cada recusa por limite MUST gerar um log estruturado de auditoria com o
  identificador do pagador, o tipo de limite, o valor pedido, o limite e o disponível; documentos,
  nomes e e-mails MUST NOT aparecer na telemetria (Princípio VI).
- **FR-017**: O contrato e o OpenAPI de `POST /api/v1/transferencias` MUST documentar a nova
  resposta 422 `limite-excedido` com suas extensões, e o endpoint de consulta de limites MUST ser
  publicado com o esquema `Bearer` e todas as respostas (Princípio V). A mudança é
  retrocompatível: só acrescenta um erro e um endpoint.

### Key Entities

- **Política de limite** (`LimitesTransferencia` no data-model): conjunto de limites aplicáveis a um perfil de conta — limite por
  operação e limite diário (cada um opcional), definidos por configuração do ambiente.
- **Uso diário**: soma dos valores das transferências concluídas enviadas por uma carteira no dia
  civil de Brasília; derivado das transferências existentes, não um contador separado.
- **Resumo de limites**: visão de leitura para a PF — limites vigentes, transferido hoje e
  disponível hoje.
- **Recusa por limite**: resultado de negócio (não persistido) com tipo de limite, limite e valor
  disponível, refletido no erro, na métrica e no log de auditoria.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% das execuções, 5 transferências simultâneas de R$ 300,00 com limite diário
  de R$ 1.000,00 e saldo suficiente resultam em exatamente 3 concluídas e 2 recusadas por limite.
- **SC-002**: Em nenhum dia e para nenhuma PF a soma das transferências concluídas enviadas
  ultrapassa o limite diário vigente no momento de cada transferência.
- **SC-003**: 100% das transferências com valor acima do limite por operação são recusadas, sem
  alteração de saldo.
- **SC-004**: Reenviar uma transferência concluída (sequencialmente ou 20 vezes em paralelo)
  consome o limite exatamente uma vez.
- **SC-005**: Transferências feitas entre 21h00 e 23h59 de Brasília (já no dia seguinte em UTC)
  contam para o dia de Brasília em 100% dos casos de teste.
- **SC-006**: Na tela Transferir, a PF vê o valor máximo permitido antes de enviar, e 100% das
  recusas por limite mostram o valor disponível, em desktop e mobile.
- **SC-007**: 95% das transferências continuam respondendo em menos de 1 segundo no ambiente
  local com a verificação de limite ativa.
- **SC-008**: 100% das recusas por limite aparecem na métrica por motivo e no log de auditoria,
  com 0 ocorrências de documento, nome ou e-mail na telemetria.

## Assumptions

- **Escopo**: issue #33; backend (regra, erro, consulta de limites, métricas) e o reflexo nas
  telas Transferir e Confirmação da spec 007, na mesma spec.
- **Quem é limitado**: só Pessoa Física transfere (spec 006); limites para PJ ficam para quando
  PJ puder transferir.
- **Limite noturno** (como o do PIX regulado, das 20h às 6h): fora do escopo; pode ser um novo
  tipo de limite no futuro (FR-007).
- **Limites por modalidade** (PIX/TED): fora do escopo; a issue #31 os acrescenta sobre esta base.
- **Auditoria**: recusas não geram registro na base (como na spec 006); a auditoria é feita por
  métrica e log estruturado.
- **Uso diário derivado**: o total do dia é calculado a partir das transferências concluídas, que
  são imutáveis; não há contador separado que possa divergir.
- **Mudança de configuração**: alterações de limite valem para as transferências seguintes; não
  afetam transferências já concluídas nem replays.
- **Limites por conta** (Clarifications): fora do escopo; issue futura.
- **Moeda**: BRL, como nas specs 003 e 006.
