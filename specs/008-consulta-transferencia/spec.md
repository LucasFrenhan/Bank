# Feature Specification: Consulta de transferência por identificador

**Feature Branch**: `008-consulta-transferencia`

**Created**: 2026-11-18

**Status**: Draft

**Input**: Issue #36 — "Consulta de transferência por identificador
(`GET /api/v1/transferencias/{id}`)": permitir reabrir o comprovante de uma transferência a
qualquer momento (a partir do extrato ou em outra sessão). Hoje o contrato da spec
[006-transferencias](../006-transferencias/spec.md) só devolve o comprovante na resposta do envio.
Requisito levantado pela spec [007-frontend-transferencias](../007-frontend-transferencias/spec.md)
(FR-014), cuja fase 10 (issue #32) consome esta consulta.

## Clarifications

### Session 2026-11-18

- Q: O que a consulta deve devolver no campo "chave de idempotência" quando quem consulta é o
  recebedor da transferência? → A: Mesmo formato do comprovante do envio; o campo vem vazio para o
  recebedor e preenchido para o pagador.
- Q: Quando o recebedor consulta a transferência, a resposta deve incluir também o documento de
  quem pagou, mascarado? → A: Sim, para os dois: o documento do pagador vem mascarado (regra do
  extrato: CPF `•••.982.247-••`) tanto para o pagador quanto para o recebedor.

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes do Bank autenticados (Pessoa Física ou Jurídica), por meio do frontend.
Quem pagou e quem recebeu uma transferência podem consultá-la depois; ninguém mais consegue saber
que ela existe.

### User Story 1 - Pagador reabre o comprovante (Priority: P1)

Uma Pessoa Física que fez uma transferência abre, dias depois ou em outro dispositivo, o
comprovante dessa transferência a partir do extrato, e vê os mesmos dados que viu ao concluí-la.

**Why this priority**: é o motivo da issue — o comprovante deixa de depender da sessão em que a
transferência foi feita.

**Independent Test**: transferir de uma PF para uma PJ, autenticar a PF numa nova sessão e
consultar a transferência pelo identificador; comparar com o comprovante devolvido no envio.

**Acceptance Scenarios**:

1. **Given** uma transferência concluída de A para B, **When** A a consulta pelo identificador,
   **Then** recebe os mesmos dados do comprovante devolvido no envio: identificador, chave de
   idempotência, pagador (identificador, nome e CPF mascarado), recebedor (identificador, nome,
   documento e tipo de conta), valor, descrição, situação `Concluida` e data/hora UTC.
2. **Given** a mesma transferência, **When** A a consulta várias vezes ou em sessões diferentes,
   **Then** a resposta é sempre a mesma e nenhum saldo muda.

---

### User Story 2 - Recebedor consulta uma transferência recebida (Priority: P1)

Quem recebeu uma transferência (PF ou PJ) abre o comprovante a partir do próprio extrato, para
conferir quem pagou, quanto e quando.

**Why this priority**: o extrato do recebedor lista a entrada; sem esta consulta ele não tem
comprovante (FR-014 da spec 007 vale para pagador e recebedor).

**Independent Test**: transferir de uma PF para uma PJ e consultar a transferência com o token da
PJ; conferir os dados e que a chave de idempotência do pagador não é exposta.

**Acceptance Scenarios**:

1. **Given** uma transferência de A (PF) para B (PJ), **When** B a consulta, **Then** recebe
   identificador, pagador (identificador, nome e CPF mascarado `•••.982.247-••`), recebedor (os
   próprios dados), valor, descrição, situação e data/hora, com o campo da chave de idempotência
   vazio.
2. **Given** uma transferência entre duas PF, **When** a PF recebedora a consulta, **Then** a
   resposta segue a mesma regra de B no cenário 1.

---

### User Story 3 - Terceiros não descobrem transferências alheias (Priority: P1)

Um cliente que não participou de uma transferência, ainda que conheça seu identificador, não
consegue ver os dados nem distinguir "não existe" de "não é sua".

**Why this priority**: os dados de uma transferência (nomes, documento, valor) são pessoais e
financeiros; vazar a existência já é uma falha de privacidade.

**Independent Test**: com uma transferência de A para B, consultar com o token de C (outra conta)
e com um identificador aleatório; comparar as duas respostas.

**Acceptance Scenarios**:

1. **Given** uma transferência de A para B, **When** C a consulta, **Then** recebe 404
   `transferencia-nao-encontrada`.
2. **Given** um identificador válido que não corresponde a nenhuma transferência, **When** é
   consultado, **Then** a resposta é idêntica à do cenário 1 (mesmo status, mesmo `type`, mesmo
   texto), salvo o `traceId`.
3. **Given** um identificador em formato inválido (não é um UUID), **When** é consultado,
   **Then** a resposta é 400 `validacao` com erro no parâmetro `transferenciaId`.
4. **Given** uma requisição sem token ou com token inválido, **When** é feita, **Then** a resposta
   é 401 `nao-autenticado`; com token válido sem as informações de identidade exigidas, 403
   `acesso-negado`.

---

### Edge Cases

- Identificador com letras maiúsculas ou sem hífens: aceito se for um UUID válido; texto qualquer
  → 400.
- Transferência para si mesmo: não existe (bloqueada pela spec 006), portanto não há caso em que
  pagador e recebedor sejam a mesma pessoa.
- Lançamento de abertura ("Saldo inicial de demonstração"): não é uma transferência e não tem
  identificador consultável.
- Pagador ou recebedor que mudou de nome depois da transferência: a consulta mostra o nome atual
  do titular, como o extrato.
- Muitas consultas seguidas de identificadores inexistentes pelo mesmo usuário: todas recebem
  404; a métrica por resultado permite detectar a tentativa de varredura.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Clientes autenticados (PF e PJ) MUST poder consultar uma transferência pelo
  identificador quando forem o pagador ou o recebedor dela.
- **FR-002**: A resposta MUST ter os mesmos campos e valores do comprovante devolvido no envio
  (spec 006, FR-009): identificador, chave de idempotência, pagador (identificador, nome e
  documento mascarado), recebedor (identificador, nome, documento e tipo de conta), valor,
  descrição, situação e data/hora UTC.
- **FR-002a**: O comprovante MUST passar a incluir o documento do pagador **mascarado** pela mesma
  regra do extrato (CPF `•••.982.247-••`), tanto na consulta (para pagador e recebedor) quanto na
  resposta do envio (spec 006), que ganha esse campo de forma aditiva para manter um único formato
  de comprovante.
- **FR-003**: A resposta MUST usar o mesmo formato do comprovante do envio para pagador e
  recebedor; para o recebedor, o campo da chave de idempotência MUST vir vazio (ela é um dado do
  envio do pagador), e para o pagador, preenchido; os demais campos seguem FR-002. O documento do
  recebedor vem completo (para o pagador, foi ele quem o informou; para o recebedor, é o próprio).
- **FR-004**: Transferência inexistente e transferência de que o usuário não participa MUST
  receber a mesma resposta 404 `transferencia-nao-encontrada`, sem nenhuma diferença que revele a
  existência da transferência.
- **FR-005**: Identificador em formato inválido MUST receber 400 `validacao` com erro no
  parâmetro `transferenciaId`; sem token ou token inválido, 401 `nao-autenticado`; token sem as
  informações de identidade exigidas, 403 `acesso-negado` (Princípio III).
- **FR-006**: A consulta MUST ser somente leitura: nenhum saldo, lançamento, transferência ou
  evento é criado ou alterado.
- **FR-007**: As consultas MUST ser contadas em métrica própria por resultado (`encontrada`,
  `nao-encontrada`, `invalida`), para permitir detectar varreduras; identificadores, documentos,
  nomes e e-mails MUST NOT ir para logs, traces ou métricas (Princípio VI).
- **FR-008**: O endpoint MUST ser publicado no OpenAPI com o esquema `Bearer` e as respostas 200,
  400, 401, 403 e 404, e o contrato de transferências MUST passar à versão 1.1.0; nos endpoints
  existentes, a única mudança permitida é o campo aditivo do documento mascarado do pagador
  (FR-002a), compatível com os consumidores atuais (Princípio V).
- **FR-009**: O `type` `transferencia-nao-encontrada` MUST ser documentado no contrato junto com
  os demais erros da spec 006.

### Key Entities

- **Transferência (consulta)**: visão de leitura de uma transferência concluída da spec 006, com
  as partes (pagador com CPF mascarado, recebedor com documento completo), o valor, a descrição,
  a situação e a data/hora; a chave de idempotência aparece só para o pagador.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% dos casos, a consulta do pagador devolve exatamente os dados do comprovante
  recebido no envio.
- **SC-002**: Em 100% dos casos, pagador e recebedor conseguem consultar a transferência, e
  qualquer outra conta recebe a mesma resposta dada a um identificador inexistente.
- **SC-003**: 0 ocorrências da chave de idempotência do pagador nas consultas feitas pelo
  recebedor.
- **SC-004**: 95% das consultas respondem em menos de 1 segundo no ambiente local.
- **SC-005**: 0 ocorrências de identificador da transferência, documento, nome ou e-mail na
  telemetria gerada pelos testes; as consultas aparecem na métrica por resultado.
- **SC-006**: Saldos, lançamentos e eventos ficam idênticos antes e depois de qualquer número de
  consultas.

## Assumptions

- **Base**: usa as transferências e o razão da spec 006 sem alterações de modelo; nenhuma
  migração de dados é esperada.
- **Chave para o recebedor** (FR-003): omitida por ser um dado técnico do envio do pagador, sem
  uso para quem recebe; o campo continua no contrato (vazio para o recebedor) para que pagador e
  recebedor recebam o mesmo formato.
- **Documento do pagador** (FR-002a): vem sempre mascarado, para pagador e recebedor, pela
  mesma regra que o extrato já usa para contrapartes PF; o pagador é sempre PF (spec 006), então
  é sempre um CPF. A fase 10 da spec 007 passa a usar esse campo no comprovante, e a FR-011 da
  spec 007 (CPF do pagador só para o próprio pagador) é atualizada no PR que finaliza a issue #32.
- **Nomes atuais**: pagador e recebedor aparecem com o nome atual do cadastro, como no extrato.
- **Sem histórico de consultas**: a consulta não é auditada individualmente; a métrica por
  resultado é suficiente para detectar abuso nesta fase.
- **Consumidor**: a fase 10 da spec 007 (issue #32) consome esta consulta depois do merge.
