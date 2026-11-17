# Feature Specification: Motor de transferências e extrato

**Feature Branch**: `006-transferencias`

**Created**: 2026-11-17

**Status**: Draft

**Input**: Issues #8 ("Motor de Transferências Bancárias, Concorrência e Idempotência" — regras,
concorrência, modelo e contratos) e #13 ("Motor de Transferências (P2P/P2B), Lock PostgreSQL,
Idempotência e Extrato" — execução e plano de testes), tratadas juntas por decisão do usuário:
"a #8 diz o que e como, a #13 a execução".

## Clarifications

### Session 2026-11-17

- Q: Qual o nome do cabeçalho de idempotência — `Idempotency-Key` (constituição) ou
  `Chave-Idempotencia` (issues)? → A: `Idempotency-Key`, como fixado pela constituição
  (Princípio IV); o texto das issues fica desatualizado nesse ponto.
- Q: O lançamento de abertura do saldo de demonstração aparece no extrato? → A: Sim, como uma
  entrada "Saldo inicial de demonstração"; o extrato lista os lançamentos da carteira e fecha com
  o saldo.
- Q: As telas de transferência do protótipo entram nesta spec? → A: Não; ficam numa spec de
  frontend separada, logo depois, consumindo o contrato desta.
- Q: Esta spec inclui uma consulta prévia do destinatário pelo documento, para confirmar o
  nome antes do envio? → A: Sim — consulta autenticada (só PF, quem pode transferir) que devolve o
  nome completo do destinatário, para PF e PJ.
- Q: Deve existir limite de valor por transferência ou por dia, além do saldo? → A: Não nesta
  spec; a regra de limite de transferência fica para uma issue futura.
- Q: O extrato aceita filtro de direção (Todas, Entradas, Saídas)? → A: Sim — parâmetro
  opcional `direcao` (`Entrada` ou `Saida`; ausente = todas), aplicado antes da paginação e
  refletido no total.

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes do Bank autenticados. **Pessoa Física** envia transferências para
outras pessoas (P2P) ou para empresas (P2B); **Pessoa Jurídica** só recebe (RN-06 da spec 003).
Ambos consultam o extrato da própria carteira. A spec 003 já entrega cadastro, login, carteira e
as permissões por perfil; esta spec movimenta saldo pela primeira vez.

### User Story 1 - Transferir para outra conta (Priority: P1)

Uma Pessoa Física informa o documento (CPF ou CNPJ) de quem vai receber, o valor e,
opcionalmente, uma descrição. Se tiver saldo, o valor sai da sua carteira e entra na do
recebedor de uma só vez, e ela recebe um comprovante.

**Why this priority**: é a razão de existir da carteira; sem ela não há movimentação.

**Independent Test**: com duas contas (PF pagadora com saldo e PF ou PJ recebedora), transferir
e verificar o comprovante e os saldos das duas carteiras; provocar cada recusa e conferir a
resposta padronizada sem alteração de saldo.

**Acceptance Scenarios**:

1. **Given** uma PF com R$ 10,00 e uma PJ cadastrada, **When** a PF transfere R$ 4,50 para o
   CNPJ da PJ, **Then** recebe 201 com o comprovante (identificador, pagador, recebedor com nome
   e documento, valor, descrição, situação `Concluida`, data/hora UTC) e os saldos passam a
   R$ 5,50 e R$ 4,50.
2. **Given** uma PF pagadora e outra PF recebedora, **When** a transferência é feita para o CPF
   (com ou sem máscara), **Then** é concluída (P2P).
3. **Given** uma conta PJ autenticada, **When** tenta transferir, **Then** recebe 403
   (`acesso-negado`) e nenhum saldo muda.
4. **Given** um documento sem conta no Bank, **When** a PF tenta transferir, **Then** recebe 404
   (`destinatario-nao-encontrado`).
5. **Given** o próprio documento como destino, **When** a PF tenta transferir, **Then** recebe
   422 (`autotransferencia`).
6. **Given** saldo menor que o valor, **When** a PF tenta transferir, **Then** recebe 422
   (`saldo-insuficiente`) e os saldos não mudam.
7. **Given** valor zero, negativo, com mais de 2 casas decimais, descrição com mais de 200
   caracteres ou chave de idempotência ausente/inválida, **When** a PF envia, **Then** recebe 400
   com erros por campo.

---

### User Story 1a - Confirmar o destinatário antes de transferir (Priority: P1)

Antes de enviar, a Pessoa Física informa o documento de quem vai receber e vê o nome do titular
e o tipo de conta, para conferir que é a pessoa ou empresa certa.

**Why this priority**: é a principal proteção contra transferir para a pessoa errada e é parte
do fluxo do protótipo (Transferir → Confirmação), consumido pela issue #32.

**Independent Test**: consultar o CPF de uma PF e o CNPJ de uma PJ cadastradas e conferir nome e
tipo; consultar um documento sem conta, o próprio documento e um documento inválido.

**Acceptance Scenarios**:

1. **Given** uma PF autenticada e uma PJ cadastrada, **When** consulta o CNPJ da PJ (com ou sem
   máscara), **Then** recebe 200 com o nome completo e o tipo `PessoaJuridica`.
2. **Given** uma PF autenticada e outra PF cadastrada, **When** consulta o CPF, **Then** recebe
   200 com o nome completo e o tipo `PessoaFisica`.
3. **Given** um documento válido sem conta no Bank, **When** é consultado, **Then** a resposta é
   404 `destinatario-nao-encontrado`.
4. **Given** o próprio documento, **When** é consultado, **Then** a resposta é 422
   `autotransferencia`.
5. **Given** um documento com formato ou dígito verificador inválido, **When** é consultado,
   **Then** a resposta é 400 com erro no campo `documento`.
6. **Given** uma conta PJ autenticada, **When** tenta consultar, **Then** recebe 403
   `acesso-negado`.

---

### User Story 2 - Repetir a requisição sem pagar duas vezes (Priority: P1)

Se a conexão cair ou a pessoa clicar de novo, o cliente reenvia a mesma transferência com a
mesma chave de idempotência. O Bank devolve o comprovante original sem debitar de novo; se a
chave for reutilizada com dados diferentes, recusa.

**Why this priority**: a constituição exige idempotência em toda operação que movimenta saldo
(Princípio IV); sem ela há risco real de débito duplicado.

**Independent Test**: enviar a mesma transferência duas (e N concorrentes) vezes com a mesma
chave e verificar um único débito; reenviar a chave com outro valor ou destino e verificar 409.

**Acceptance Scenarios**:

1. **Given** uma transferência concluída com a chave K, **When** a mesma requisição é reenviada
   com K, **Then** a resposta é 200 com o mesmo comprovante (mesmo identificador) e o saldo não
   muda.
2. **Given** uma transferência concluída com a chave K, **When** K é reenviada com valor ou
   destinatário diferentes, **Then** a resposta é 409 (`idempotencia-divergente`).
3. **Given** várias requisições simultâneas com a mesma chave K, **When** são processadas,
   **Then** exatamente uma transferência é criada e as demais recebem 200 com o mesmo
   comprovante.
4. **Given** uma chave K usada por outro pagador, **When** um pagador diferente a usa, **Then**
   ela é tratada como nova (a idempotência é por pagador).

---

### User Story 3 - Nunca gastar o mesmo dinheiro duas vezes (Priority: P1)

Várias transferências simultâneas da mesma carteira, ou transferências cruzadas entre duas
carteiras ao mesmo tempo, nunca deixam saldo negativo, nunca perdem nem criam dinheiro e nunca
travam o sistema.

**Why this priority**: integridade financeira (Princípio IV); falha aqui é defeito de correção.

**Independent Test**: carteira com R$ 1.000,00 dispara 5 transferências simultâneas de
R$ 300,00 → exatamente 3 concluídas, 2 recusadas por saldo insuficiente, saldo final R$ 100,00;
transferências cruzadas A→B e B→A simultâneas concluem sem impasse.

**Acceptance Scenarios**:

1. **Given** saldo de R$ 1.000,00, **When** 5 transferências de R$ 300,00 chegam ao mesmo tempo,
   **Then** 3 são concluídas, 2 recebem 422 `saldo-insuficiente` e o saldo final é R$ 100,00.
2. **Given** duas carteiras com saldo, **When** dezenas de transferências cruzadas A→B e B→A
   ocorrem simultaneamente, **Then** todas terminam (nenhuma fica presa por impasse) e a soma
   dos saldos das duas carteiras é igual à soma inicial.
3. **Given** qualquer conjunto de transferências concluídas, **When** os saldos são conferidos,
   **Then** cada saldo é igual à soma dos lançamentos da carteira e nunca é negativo.

---

### User Story 4 - Consultar o extrato (Priority: P2)

PF e PJ consultam o extrato da própria carteira: as transferências enviadas (saída) e recebidas
(entrada), da mais recente para a mais antiga, com a contraparte, o valor e a descrição, em
páginas.

**Why this priority**: dá visibilidade às movimentações; depende das US1–US3 para ter conteúdo.

**Independent Test**: após transferências entre duas contas, o pagador vê a operação como
`Saida` e o recebedor como `Entrada`, com paginação correta.

**Acceptance Scenarios**:

1. **Given** uma transferência de A para B, **When** A consulta o extrato, **Then** vê a operação
   como `Saida` com o nome de B; **When** B consulta, **Then** vê como `Entrada` com o nome de A.
2. **Given** 25 movimentações, **When** a primeira página é pedida com o tamanho padrão, **Then**
   vêm as 20 mais recentes e o total 25; a página 2 traz as 5 restantes.
3. **Given** `tamanhoPagina` acima de 100, zero ou negativo, ou `pagina` menor que 1, **When** o
   extrato é pedido, **Then** a resposta é 400 com erro por parâmetro.
4. **Given** uma carteira sem movimentações, **When** o extrato é pedido, **Then** vem uma lista
   vazia com total 0.
4a. **Given** 25 entradas e 5 saídas, **When** o extrato é pedido com `direcao=Saida`, **Then**
   vêm só as 5 saídas e o total é 5; com `direcao=Entrada`, vêm 20 entradas na primeira página e
   o total é 25; um valor diferente de `Entrada`/`Saida` → 400.
5. **Given** uma PF recém-cadastrada com R$ 10,00 de saldo de demonstração, **When** consulta o
   extrato, **Then** vê uma `Entrada` "Saldo inicial de demonstração" de R$ 10,00, sem
   contraparte, e a soma do extrato é igual ao saldo.

---

### Edge Cases

- Documento de destino com máscara, espaços ou CNPJ alfanumérico em minúsculas: normalizado como
  no cadastro (spec 003) antes da busca.
- Destino PJ e destino PF: ambos permitidos (P2B e P2P); pagador sempre PF.
- Transferência que deixaria o saldo exatamente em zero: permitida.
- Replay idempotente quando o saldo atual já não cobriria o valor: devolve o comprovante original
  (200), sem reavaliar o saldo.
- Mesma chave de idempotência em requisições simultâneas com dados diferentes: uma conclui; as
  outras recebem 409.
- Valor acima do saldo por um centavo: 422 `saldo-insuficiente`.
- Valor com mais de 2 casas decimais (ex.: 10,005): 400, sem arredondar.
- Descrição só com espaços: tratada como ausente.
- Pagador ou recebedor removido da base entre a busca e o débito: a transferência falha sem
  alterar saldos.
- Extrato pedido além da última página: lista vazia, com o total correto.
- Carteira pagadora com dezenas de transferências simultâneas para destinos diferentes: as que
  cabem no saldo concluem; as demais recebem `saldo-insuficiente`.

## Requirements *(mandatory)*

### Functional Requirements

**Transferência**

- **FR-001**: Apenas usuários Pessoa Física MUST poder iniciar transferências; Pessoa Jurídica
  MUST receber 403 `acesso-negado`, aplicando a permissão `RealizarTransferencia` já definida na
  spec 003 (RN-01 da #8).
- **FR-002**: O destino MUST ser identificado pelo documento (CPF ou CNPJ, numérico ou
  alfanumérico, com ou sem máscara, normalizado como na spec 003) e pode ser PF ou PJ; documento
  sem conta → 404 `destinatario-nao-encontrado` (RN-02).
- **FR-003**: Transferência para a própria carteira MUST ser recusada com 422 `autotransferencia`
  (RN-03).
- **FR-004**: O valor MUST ser ≥ R$ 0,01 e ter no máximo 2 casas decimais, sem arredondamento;
  descrição opcional com até 200 caracteres; violações → 400 com erros por campo (RN-04).
- **FR-005**: A verificação de saldo MUST acontecer com as carteiras envolvidas bloqueadas contra
  alterações concorrentes; saldo menor que o valor → 422 `saldo-insuficiente` sem alterar nada
  (RN-05).
- **FR-006**: Débito da origem, crédito do destino, atualização da data de movimentação das duas
  carteiras e registro imutável da transferência MUST acontecer numa única operação atômica:
  tudo ou nada (RN-06).
- **FR-007**: As carteiras envolvidas MUST ser bloqueadas sempre na mesma ordem determinística
  (pelo identificador da carteira, crescente), de modo que transferências cruzadas simultâneas
  nunca entrem em impasse (estratégia da #8, seção 2).
- **FR-008**: Transferências concluídas e seus lançamentos MUST ser imutáveis: nenhuma operação
  desta spec altera ou apaga um registro existente (Princípio IV).
- **FR-009**: O comprovante MUST conter identificador da transferência, chave de idempotência,
  pagador (identificador e nome), recebedor (identificador, nome e documento), valor, descrição,
  situação (`Concluida`) e data/hora UTC.

**Consulta do destinatário**

- **FR-009a**: Pessoas Físicas autenticadas MUST poder consultar, pelo documento (mesma
  normalização de FR-002), o nome completo e o tipo de conta do titular de destino, sem expor
  outros dados (e-mail, identificadores internos, carteira ou saldo). Documento inválido → 400;
  sem conta → 404 `destinatario-nao-encontrado`; próprio documento → 422 `autotransferencia`;
  PJ → 403 `acesso-negado`.
- **FR-009b**: As consultas de destinatário MUST ser contadas em métrica própria (por resultado)
  para permitir detectar varreduras de documentos; documento e nome consultados MUST NOT ir para
  a telemetria.

**Idempotência**

- **FR-010**: Toda requisição de transferência MUST trazer uma chave de idempotência (UUID) no
  cabeçalho `Idempotency-Key` (Princípio IV); ausente ou inválida → 400.
- **FR-011**: A chave MUST ser única por pagador, garantida pela base: reenvio com os mesmos dados
  (destinatário e valor) → 200 com o comprovante original, sem novo débito; reenvio com dados
  divergentes → 409 `idempotencia-divergente`; requisições simultâneas com a mesma chave produzem
  uma única transferência (RN-07).

**Extrato**

- **FR-012**: PF e PJ autenticadas MUST poder consultar o extrato da própria carteira: os
  lançamentos da carteira, do mais recente para o mais antigo, cada um com tipo
  (`Transferencia` ou `Abertura`), direção (`Entrada` ou `Saida`), valor, descrição e data/hora
  UTC; os de transferência trazem também o identificador da transferência e o nome e o documento
  da contraparte; o de abertura tem a descrição "Saldo inicial de demonstração" e nenhuma
  contraparte. A soma das entradas menos as saídas de todas as páginas é igual ao saldo.
- **FR-013**: O extrato MUST ser paginado (`pagina` padrão 1, mínimo 1; `tamanhoPagina` padrão
  20, entre 1 e 100), informando a página atual e o total de itens, e MUST aceitar o filtro
  opcional `direcao` (`Entrada` ou `Saida`; ausente = todas), aplicado antes da paginação e
  refletido no total; parâmetros inválidos → 400.
- **FR-014**: O documento da contraparte no extrato MUST ser exibido completo quando for CNPJ e
  mascarado quando for CPF (`•••.982.247-••`), minimizando a exposição de dados pessoais de
  terceiros.

**Razão e lançamento de abertura**

- **FR-015**: Para toda carteira, o saldo MUST ser igual à soma dos seus lançamentos (créditos
  menos débitos).
- **FR-016**: Fora de `Production`, ao iniciar a aplicação, MUST ser criado um lançamento de
  abertura para cada carteira cujo saldo não esteja coberto por lançamentos (o saldo de
  demonstração da spec 003, inclusive o das carteiras criadas antes desta spec); a operação é
  idempotente (rodar de novo não duplica). Carteiras novas recebem o lançamento de abertura no
  próprio cadastro, na mesma transação.

**Evento de integração**

- **FR-017**: Cada transferência concluída MUST gerar o evento `transferencia.concluida`
  (versão de schema 1; identificador do evento, identificador da transferência, pagador,
  recebedor, valor e data/hora), registrado de forma consistente com a transação (se a
  transferência não for gravada, o evento também não é). A entrega ao RabbitMQ fica para a issue
  da mensageria (TASK-09); nesta spec o evento fica registrado e pronto para envio (Princípio IV,
  Transactional Outbox).

**Transversais**

- **FR-018**: Todos os erros MUST seguir `ProblemDetails` com `traceId` e `type` estável
  documentado no contrato (Princípio III).
- **FR-019**: Documentos, nomes e e-mails MUST NOT aparecer em logs, traces ou métricas; a
  telemetria registra apenas identificadores, valores agregados e motivos de recusa
  (Princípio VI).
- **FR-020**: O sistema MUST expor métricas de transferências concluídas e recusadas por motivo
  (`validacao`, `saldo-insuficiente`, `autotransferencia`, `destinatario-nao-encontrado`,
  `idempotencia-divergente`) e de replays idempotentes (Princípio VI). Recusas por perfil (403)
  acontecem na autorização, antes do caso de uso, e são contadas pelas métricas HTTP por status.
- **FR-021**: Os endpoints `POST /api/v1/transferencias`, `GET /api/v1/transferencias/extrato`
  e o da consulta do destinatário (FR-009a) MUST ser publicados no OpenAPI com o esquema `Bearer`, o cabeçalho de idempotência e todas as
  respostas documentadas (Princípio V).
- **FR-022**: Esta spec entrega apenas o backend; as telas de transferência do protótipo
  (Transferir, Confirmação, Comprovante e Extrato, desktop e mobile) ficam numa spec de frontend
  separada, que consome o contrato desta.

### Key Entities

- **Transferência**: movimentação imutável de uma carteira de origem (PF) para uma de destino (PF
  ou PJ): identificador (ordenável por tempo), chave de idempotência (única por pagador), valor
  (> 0, 2 casas), descrição opcional (≤ 200), situação (`Concluida`), data/hora UTC.
- **Lançamento**: entrada imutável do razão de uma carteira — crédito ou débito com valor,
  origem (abertura ou transferência) e data/hora; a soma dos lançamentos é o saldo.
- **Item de extrato**: visão de leitura de um lançamento da carteira (tipo, direção, valor,
  descrição, data/hora e, quando for transferência, a contraparte).
- **Evento `transferencia.concluida`**: fato publicado após cada transferência, com versão de
  schema, registrado junto com a transferência e entregue depois.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Em 100% das transferências concluídas, a soma dos saldos de origem e destino é a
  mesma antes e depois, e o comprovante corresponde aos saldos finais.
- **SC-002**: Com R$ 1.000,00 e 5 transferências simultâneas de R$ 300,00, exatamente 3 são
  concluídas, 2 recusadas por saldo insuficiente e o saldo final é R$ 100,00, em 100% das
  execuções.
- **SC-003**: 50 transferências cruzadas simultâneas entre duas carteiras terminam todas, sem
  impasse, em menos de 10 segundos no ambiente local.
- **SC-004**: Reenviar a mesma transferência (sequencial ou 20 vezes em paralelo) produz
  exatamente 1 débito em 100% dos casos.
- **SC-005**: Em qualquer momento, 100% das carteiras têm saldo igual à soma dos seus
  lançamentos e nenhum saldo negativo.
- **SC-006**: 95% das transferências e das consultas de extrato respondem em menos de 1 segundo
  no ambiente local.
- **SC-007**: 100% das transferências concluídas têm o evento `transferencia.concluida`
  registrado, e nenhuma transferência recusada gera evento.
- **SC-008**: 0 ocorrências de documento, nome ou e-mail na telemetria gerada pelos testes.

## Assumptions

- **Escopo**: issues #8 e #13 juntas (#8: regras, concorrência, modelo e contratos; #13:
  execução e testes). A spec fica em `specs/006-transferencias/` (convenção do repositório), não
  em `specs/02-transferencias.md` como citam as issues.
- **Situação única**: toda transferência gravada está `Concluida`; recusas não geram registro.
  Estornos ou cancelamentos são novas operações compensatórias em spec futura (Princípio IV).
- **Moeda**: BRL, como na spec 003.
- **Limites de transferência** (Clarifications): nesta spec o único limite é o saldo
  disponível. **Pendente — issue #33**: criar a regra de limite de transferência (por
  operação e/ou diário, configurável, com erro próprio e auditoria).
- **Documento no comprovante**: o recebedor aparece com o documento completo, pois foi o próprio
  pagador que o informou.
- **Concorrência**: o bloqueio de linhas ordenado é a estratégia definida na #8; se exigir SQL
  puro, segue a regra da constituição v1.1.0 (arquivo `.sql` em `Persistencia/Sql/`).
- **Saldo de demonstração**: PF começa com R$ 10,00 fora de `Production` (spec 003); os testes
  que precisam de mais saldo ajustam o saldo e o lançamento de abertura diretamente na base de
  teste.
- **Mensageria**: o envio efetivo ao RabbitMQ e o consumo do evento ficam para a issue da
  mensageria (TASK-09).
