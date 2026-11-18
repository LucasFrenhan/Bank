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
   idempotência, pagador (identificador e nome), recebedor (identificador, nome, documento e tipo
   de conta), valor, descrição, situação `Concluida` e data/hora UTC.
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
   identificador, pagador (identificador e nome), recebedor (os próprios dados), valor,
   descrição, situação e data/hora, **sem** a chave de idempotência do pagador.
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
  (spec 006, FR-009): identificador, chave de idempotência, pagador (identificador e nome),
  recebedor (identificador, nome, documento e tipo de conta), valor, descrição, situação e
  data/hora UTC.
- **FR-003**: Para o recebedor, a chave de idempotência MUST vir vazia (ela é um dado do envio do
  pagador); os demais campos seguem FR-002. O documento do recebedor é o do próprio usuário
  (completo), e o pagador continua sem documento na resposta.
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
  400, 401, 403 e 404, e o contrato de transferências MUST passar à versão 1.1.0, sem alterar os
  endpoints existentes (Princípio V).
- **FR-009**: O `type` `transferencia-nao-encontrada` MUST ser documentado no contrato junto com
  os demais erros da spec 006.

### Key Entities

- **Transferência (consulta)**: visão de leitura de uma transferência concluída da spec 006, com
  as partes, o valor, a descrição, a situação e a data/hora; a chave de idempotência aparece só
  para o pagador.

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
- **Documento do pagador**: não entra na resposta (como no comprovante da spec 006); o frontend
  mostra o CPF do pagador só ao próprio pagador, a partir dos dados do titular (spec 005).
- **Nomes atuais**: pagador e recebedor aparecem com o nome atual do cadastro, como no extrato.
- **Sem histórico de consultas**: a consulta não é auditada individualmente; a métrica por
  resultado é suficiente para detectar abuso nesta fase.
- **Consumidor**: a fase 10 da spec 007 (issue #32) consome esta consulta depois do merge.
