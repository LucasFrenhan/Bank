# Feature Specification: Cartão de crédito, cobranças, faturas e recebimento do lojista

**Feature Branch**: `014-cartao-credito-cobrancas`

**Created**: 2026-11-24

**Status**: Draft

**Input**: Issue #22 ("Cobrança via Cartão de Crédito (PJ), Autorizador Simulado e Liquidação"),
ampliada pelo contexto fornecido pelo responsável em 2026-11-24. Uma Pessoa Jurídica (PJ) emite
cobranças e uma Pessoa Física (PF) as paga com o cartão de crédito One Bank, autorizado por uma
bandeira simulada conforme o limite disponível. A compra é lançada na fatura por um processamento
diário, a PF consulta e paga faturas mensais e o lojista recebe o valor em 30 dias úteis ou o
antecipa com taxa de 2%. O documento `specs/04-cobrancas-cartao.md` citado na issue não existe;
esta spec o substitui.

Pontos do contexto ampliado:

- Bandeiras Visa (`001`) e Mastercard (`002`); a PF escolhe a bandeira ao solicitar o cartão e não
  pode trocá-la depois; no máximo um cartão por CPF.
- A autorização é feita pela bandeira: ela confirma, no pagamento, se o cliente tem limite. A
  compensação na fatura ocorre num processamento diário que começa às 00:00 de Brasília.
- CVV de 3 dígitos.
- O lojista recebe o valor integral em 30 dias úteis; se quiser antes, paga taxa de 2%.
- Faturas mensais, sempre consultáveis: fecham no dia 07, vencem no dia 10 e só ficam disponíveis
  para pagamento depois de lançadas todas as compras do dia anterior. Compras do dia 07 entram na
  fatura que fecha nesse dia.
- Cada item da fatura traz o nome do estabelecimento e o valor.

### Diferenças em relação à issue #22

Quando a issue e o contexto ampliado divergem, vale o contexto ampliado.

| Issue #22 | Esta spec |
| --- | --- |
| RN-03: qualquer usuário paga, exceto o emissor | Só PF paga, com o próprio cartão; a regra do emissor fica coberta, pois a PJ não paga |
| RN-05: final `0000`, final `9999` e valor acima de R$ 10.000 são recusados | Regras removidas: a bandeira recusa por limite insuficiente |
| RN-05: qualquer cartão válido pelo Luhn é aceito | Só o cartão One Bank do próprio pagador |
| RN-05: CVV com 3 ou 4 dígitos | CVV com 3 dígitos |
| RN-06: valor creditado na hora, na carteira do lojista | Crédito em 30 dias úteis ou antecipado com taxa de 2% |
| RN-06: evento publicado após o commit | Mantido, pelo mecanismo já existente (spec 010) |
| Cabeçalho `Chave-Idempotencia` | `Idempotency-Key`, como exige a constituição e já fazem as transferências |
| Cancelamento citado na RN-02, sem contrato | Cancelamento pelo emissor incluído |

## Clarifications

### Session 2026-11-24

- Q: O que entra na spec 014? → A: O backend completo (cartão, cobranças, autorização,
  processamento diário, faturas, recebimento e antecipação), sem telas. O frontend fica para outra
  issue.
- Q: Qual é o limite do cartão? → A: Fixo por configuração, R$ 5.000,00 para todos os cartões,
  ajustável por ambiente.
- Q: Como o autorizador decide? → A: Aceita só o cartão One Bank do próprio pagador e recusa por
  limite insuficiente; as regras de final `0000`, final `9999` e valor acima de R$ 10.000 saem.
- Q: Como o titular vê os dados completos do cartão? → A: Por uma opção de exibir dados, que mostra um
  cartão preenchido com nome, número, validade e CVV. A tela fica para a issue de frontend; esta spec
  entrega a consulta que a alimenta, sem gravar número nem CVV.
- Q: O que acontece com a fatura em atraso? → A: Há juros por atraso, com taxa variável (configurável)
  e base de 15% ao mês.
- Q: O que muda em dias não úteis? → A: Não há lançamentos em fins de semana e feriados nacionais; eles
  são feitos no próximo dia útil. O vencimento que cai em dia não útil passa para o próximo dia útil.
- Q: Como funciona a antecipação do lojista? → A: Por item (um recebível por vez), com taxa fixa de 2%
  qualquer que seja o prazo restante.
- Q: Como os 15% ao mês de juros são cobrados? → A: Juros simples por dia de atraso (15% ÷ 30 = 0,5% ao
  dia sobre o total), somados no pagamento da própria fatura.
- Q: Em qual fatura entra a compra feita até o dia 07 e lançada depois, por cair em dia não útil? → A: Vale
  a data da compra: entra na fatura desse mês, mesmo lançada depois. O fechamento continua no dia 07.
- Q: A PF com fatura vencida pode continuar comprando? → A: Não. Novas compras são recusadas com o erro
  de cartão bloqueado por fatura vencida, até o pagamento.
- Q: Quais datas contam como feriado para os dias úteis? → A: O calendário bancário: feriados nacionais,
  Sexta-feira Santa, Carnaval (segunda e terça) e Corpus Christi.
- Q: Para exibir os dados completos do cartão, o titular precisa confirmar a senha? → A: Não; basta estar
  logado.

## User Scenarios & Testing *(mandatory)*

Os usuários são a PF titular do cartão (pagadora), a PJ que emite cobranças (lojista) e a equipe
que opera o Bank. Todos os dias e horários de regra de negócio (fechamento, vencimento, dia da
compra, dias úteis) são do calendário de Brasília. Cadastro, login, carteira, permissões por perfil,
extrato, idempotência e mensageria já existem (specs 003, 006, 010 e 011).

### User Story 1 - Solicitar o cartão de crédito (Priority: P1)

Uma PF solicita o seu cartão One Bank escolhendo a bandeira, Visa ou Mastercard. O cartão nasce
ativo, com o limite padrão, e ela passa a consultar bandeira, final, validade, limite total e
limite disponível.

**Why this priority**: sem cartão não há como pagar cobranças; é a base das demais histórias.

**Independent Test**: com uma PF sem cartão, solicitar com a bandeira 001 e conferir o cartão
criado e o limite de R$ 5.000,00; repetir a solicitação e receber a recusa.

**Acceptance Scenarios**:

1. **Given** uma PF sem cartão, **When** solicita um com a bandeira `001`, **Then** recebe um
   cartão Visa ativo, com número válido, validade futura, CVV de 3 dígitos e limite total e
   disponível de R$ 5.000,00.
2. **Given** uma PF sem cartão, **When** solicita um com a bandeira `002`, **Then** recebe um
   cartão Mastercard nas mesmas condições.
3. **Given** uma PF que já tem cartão, **When** solicita outro, com a mesma ou outra bandeira,
   **Then** a solicitação é recusada e o cartão original não muda.
4. **Given** duas solicitações simultâneas da mesma PF, **When** ambas são processadas, **Then**
   existe um único cartão.
5. **Given** uma PJ, **When** solicita um cartão, **Then** é recusada por falta de permissão.
6. **Given** uma bandeira ausente ou desconhecida, **When** a PF solicita, **Then** recebe erro de
   validação e nenhum cartão é criado.
7. **Given** uma PF logada com cartão, **When** pede para exibir os dados do cartão, quantas vezes quiser,
   **Then** vê nome impresso, número completo, validade e CVV, sempre os mesmos, sem confirmar a senha.
8. **Given** uma pessoa sem autenticação, uma PJ ou uma PF sem cartão, **When** pede esses dados,
   **Then** não recebe os dados de nenhum cartão.

---

### User Story 2 - Emitir e acompanhar cobranças (Priority: P1)

Uma PJ emite uma cobrança com valor e descrição, acompanha a lista das que emitiu e cancela as
ainda pendentes. Quem vai pagar consulta a cobrança para ver o valor e o lojista antes de informar
o cartão.

**Why this priority**: a cobrança é o objeto que será pago; é o ponto de partida da issue.

**Independent Test**: com uma PJ, emitir, consultar, listar com filtro e cancelar uma cobrança; com
uma PF, consultar a cobrança pelo identificador e tentar emitir uma.

**Acceptance Scenarios**:

1. **Given** uma PJ autenticada, **When** emite uma cobrança de R$ 299,90 com descrição, **Then**
   ela é criada como `Pendente`, com os dados do lojista e o instante de emissão.
2. **Given** uma PF autenticada, **When** tenta emitir, listar emitidas ou cancelar uma cobrança,
   **Then** é recusada por falta de permissão.
3. **Given** valor zero, negativo ou com mais de 2 casas, ou descrição vazia ou com mais de 200
   caracteres, **When** a PJ emite, **Then** recebe erro de validação por campo.
4. **Given** uma cobrança existente, **When** qualquer usuário autenticado a consulta pelo
   identificador, **Then** vê lojista, valor, descrição, status e instantes; se paga, só o lojista e
   o pagador veem os 4 últimos dígitos do cartão e o código de autorização.
5. **Given** uma PJ com cobranças de vários status, **When** lista as suas com filtro de status,
   **Then** vê só as próprias, das mais novas para as mais antigas, em páginas.
6. **Given** uma cobrança `Pendente`, **When** o emissor a cancela, **Then** ela fica `Cancelada` e
   não pode mais ser paga.
7. **Given** uma cobrança `Paga` ou já `Cancelada`, **When** o emissor tenta cancelá-la, **Then** é
   recusado por conflito e nada muda.
8. **Given** uma cobrança de outra PJ, **When** uma PJ tenta cancelá-la, **Then** recebe a mesma
   resposta de cobrança inexistente.

---

### User Story 3 - Pagar uma cobrança com o cartão (Priority: P1)

Uma PF paga uma cobrança pendente informando os dados do seu cartão. A bandeira simulada verifica o
limite e aprova ou recusa na hora. Se aprovar, a cobrança fica paga, o limite disponível diminui e o
lojista passa a ter o valor a receber; nenhum saldo muda nesse momento.

**Why this priority**: é o objetivo central da issue.

**Independent Test**: com cartão de limite R$ 5.000,00, pagar uma cobrança de R$ 299,90 (aprovada,
disponível R$ 4.700,10) e tentar pagar uma de R$ 4.700,11 (recusada, continua pendente).

**Acceptance Scenarios**:

1. **Given** cobrança `Pendente` de R$ 299,90 e limite suficiente, **When** a PF paga com os dados
   corretos do seu cartão, **Then** a cobrança fica `Paga` com 4 últimos dígitos, código de
   autorização de 6 caracteres alfanuméricos e instante do pagamento, o limite disponível cai
   R$ 299,90 e os saldos das duas carteiras não mudam.
2. **Given** limite disponível igual ao valor da cobrança, **When** a PF paga, **Then** é aprovada
   (o limite é inclusivo).
3. **Given** limite disponível menor que o valor, **When** a PF paga, **Then** é recusada com o
   motivo "limite insuficiente", a cobrança continua `Pendente` com o registro da última recusa e o
   limite não muda.
4. **Given** uma cobrança com recusa anterior, **When** outra PF com limite a paga, **Then** é
   aprovada normalmente.
5. **Given** número que não passa no Luhn, validade malformada ou vencida, CVV que não tenha 3
   dígitos ou titular vazio, **When** a PF paga, **Then** recebe erro de validação por campo e
   nenhuma tentativa é enviada à bandeira.
6. **Given** dados bem formados que não correspondem ao cartão da própria PF (cartão de terceiro, de
   outro banco, CVV ou validade errados, ou PF sem cartão), **When** ela paga, **Then** é recusada
   com um único erro de cartão não autorizado, que não diz qual dado falhou.
7. **Given** uma cobrança `Paga` ou `Cancelada`, **When** alguém tenta pagá-la com nova chave de
   idempotência, **Then** recebe conflito e nada muda.
8. **Given** um pagamento aprovado, **When** a mesma PF repete a requisição com a mesma chave,
   **Then** recebe o resultado original, sem novo consumo de limite.
9. **Given** duas PFs pagando a mesma cobrança ao mesmo tempo, **When** ambas são processadas,
   **Then** exatamente uma é aprovada e só o limite dela é consumido.
10. **Given** uma PF pagando ao mesmo tempo duas cobranças que, somadas, excedem o limite, **When**
    ambas são processadas, **Then** só uma é aprovada.
11. **Given** uma PJ autenticada, inclusive a emissora, **When** tenta pagar, **Then** é recusada
    por falta de permissão.
12. **Given** um pagamento aprovado, **When** a operação conclui, **Then** exatamente um evento
    `cobranca.paga` é publicado.
13. **Given** uma PF com fatura vencida e limite suficiente, **When** paga uma cobrança, **Then** é
    recusada com o motivo "fatura vencida", a cobrança continua `Pendente` e o limite não muda.
14. **Given** essa PF depois de pagar todas as faturas vencidas, **When** tenta de novo, **Then** é
    aprovada.

---

### User Story 4 - Lançar compras e consultar faturas (Priority: P2)

Todo dia útil, a partir das 00:00, o sistema lança na fatura as compras feitas até o dia anterior. A PF
consulta a qualquer momento a fatura atual e as de meses anteriores, com o nome do estabelecimento e o
valor de cada compra.

**Why this priority**: completa o ciclo da compra para a PF e é pré-requisito do pagamento da fatura.

**Independent Test**: pagar cobranças em dias diferentes ao redor do dia 07, executar o processamento
com o relógio controlado e conferir em qual fatura cada compra aparece e quando ela fica disponível.

**Acceptance Scenarios**:

1. **Given** uma compra feita hoje, **When** a PF consulta a fatura antes do próximo processamento,
   **Then** a compra ainda não está entre os lançamentos, mas o limite disponível já a considera.
2. **Given** essa compra, **When** o processamento das 00:00 do próximo dia útil termina, **Then** ela
   aparece na fatura com o nome do estabelecimento, o valor e a data da compra.
3. **Given** uma compra no dia 07 de dezembro, inclusive às 23:59:59, **When** é processada,
   **Then** entra na fatura com vencimento em 10 de dezembro.
4. **Given** uma compra à 00:00:00 de 08 de dezembro, **When** é processada, **Then** entra na
   fatura de janeiro, com vencimento em 11/01/2027 (10/01/2027 é domingo).
5. **Given** a fatura de dezembro fechada no fim do dia 07, **When** o processamento de 08 de
   dezembro ainda não terminou, **Then** ela aparece como fechada e não pode ser paga.
6. **Given** esse processamento terminado, **When** a PF consulta, **Then** a fatura está disponível
   para pagamento, com total e vencimento em 10 de dezembro.
7. **Given** o processamento interrompido no meio ou executado mais de uma vez, **When** roda de
   novo, **Then** toda compra aparece uma única vez e nenhuma se perde.
8. **Given** o sistema parado à 00:00, **When** volta a funcionar, **Then** processa os dias
   pendentes e cada compra entra na fatura do ciclo em que foi feita.
9. **Given** uma PF com faturas de vários meses, **When** lista as faturas, **Then** vê uma por mês,
   da mais nova para a mais antiga, e abre o detalhe de qualquer uma.
10. **Given** uma fatura de outra pessoa, **When** a PF tenta abri-la, **Then** recebe a mesma
    resposta de fatura inexistente.
11. **Given** uma compra na sexta-feira 07/05/2027, **When** chegam o sábado e o domingo, **Then** nada
    é lançado; o lançamento ocorre na segunda-feira 10/05/2027, quando a fatura de maio é liberada.

---

### User Story 5 - Pagar a fatura (Priority: P2)

A PF paga a fatura disponível, pelo valor total, com o saldo da carteira. O limite correspondente
volta a ficar disponível.

**Why this priority**: fecha o ciclo da PF; sem ele, o limite só diminui.

**Independent Test**: com uma fatura disponível de R$ 299,90 e saldo suficiente, pagar e conferir
saldo, extrato, status da fatura e limite disponível.

**Acceptance Scenarios**:

1. **Given** fatura disponível de R$ 299,90 e saldo suficiente, **When** a PF a paga, **Then** o
   saldo cai R$ 299,90, o extrato ganha a saída, a fatura fica `Paga` e o limite disponível sobe
   R$ 299,90.
2. **Given** saldo menor que o total, **When** a PF paga, **Then** é recusada por saldo insuficiente
   e nada muda.
3. **Given** fatura aberta, fechada ou já paga, **When** a PF tenta pagá-la, **Then** é recusada por
   conflito e nada muda.
4. **Given** um pagamento concluído, **When** repetido com a mesma chave de idempotência, **Then**
   devolve o resultado original, sem novo débito.
5. **Given** dois pagamentos simultâneos da mesma fatura, **When** ambos são processados, **Then**
   há um único débito.
6. **Given** uma fatura de R$ 1.000,00 não paga até o fim do dia do vencimento, **When** a PF a consulta
   10 dias depois do vencimento, **Then** ela aparece como vencida, com juros de R$ 50,00 e valor a pagar
   de R$ 1.050,00.
7. **Given** essa fatura, **When** a PF a paga nesse dia, **Then** o saldo cai R$ 1.050,00, a fatura fica
   `Paga` com total, juros e valor pago registrados, e o limite disponível sobe R$ 1.000,00.

---

### User Story 6 - Lojista recebe ou antecipa (Priority: P2)

A PJ vê os seus recebíveis: cada cobrança paga tem valor, data prevista e situação. Na data prevista,
30 dias úteis após a compra, o valor integral entra na carteira. Antes disso, ela pode antecipar um
recebível e recebe na hora o valor menos 2%.

**Why this priority**: completa o ciclo do lojista; substitui a liquidação imediata da issue.

**Independent Test**: com uma cobrança paga de R$ 299,90, conferir a data prevista; antecipar e
conferir o crédito de R$ 293,90 e a taxa de R$ 6,00; em outra cobrança, avançar o relógio até a data
prevista e conferir o crédito integral.

**Acceptance Scenarios**:

1. **Given** uma compra em 24/11/2026, **When** o lojista consulta o recebível, **Then** vê o valor
   integral e a data prevista 07/01/2027 (30º dia útil, sem contar 25/12 e 01/01).
2. **Given** um recebível previsto para hoje, **When** o processamento diário roda, **Then** o valor
   integral entra na carteira do lojista, com entrada no extrato, e o recebível fica `Liquidado`.
3. **Given** um recebível pendente de R$ 299,90, **When** o lojista o antecipa, **Then** a carteira
   recebe R$ 293,90 na hora, a taxa de R$ 6,00 fica registrada e o recebível fica `Antecipado`.
4. **Given** um recebível antecipado, **When** chega a data prevista, **Then** nada mais é creditado.
5. **Given** um recebível já liquidado ou antecipado, **When** o lojista tenta antecipá-lo, **Then**
   é recusado por conflito e nada muda.
6. **Given** uma antecipação repetida com a mesma chave, ou simultânea à liquidação diária, **When**
   processada, **Then** há um único crédito.
7. **Given** o processamento parado na data prevista, **When** volta, **Then** credita os recebíveis
   vencidos, uma única vez cada.
8. **Given** um recebível de outra PJ, **When** a PJ tenta consultá-lo ou antecipá-lo, **Then**
   recebe a mesma resposta de recebível inexistente.

---

### User Story 7 - Notificar o pagamento da cobrança (Priority: P3)

Após o pagamento, pagador e lojista têm uma notificação registrada, como já acontece com as
transferências.

**Why this priority**: a fila de cobranças já existe sem consumidor; sem ele, as mensagens se
acumulariam.

**Independent Test**: pagar uma cobrança e conferir duas notificações registradas para o evento.

**Acceptance Scenarios**:

1. **Given** um evento `cobranca.paga`, **When** é consumido, **Then** existem exatamente duas
   notificações, uma para o pagador e uma para o lojista, com o valor e sem dados de cartão.
2. **Given** o mesmo evento entregue de novo, **When** é consumido, **Then** nenhuma notificação
   nova é criada.
3. **Given** uma mensagem inválida, **When** é consumida, **Then** segue a política de novas
   tentativas e DLQ da spec 010.

### Edge Cases

- **Pagamento na virada do dia**: uma compra autorizada às 23:59:59 cuja gravação termine depois da
  00:00 não pode ficar fora da fatura do seu dia, nem a fatura pode ser liberada sem ela.
- **Mais de uma instância da aplicação**: o processamento diário não pode lançar nem creditar em
  duplicidade.
- **Falha ao lançar uma compra**: a fatura afetada não fica disponível até o lançamento ser
  concluído; as faturas de outras pessoas seguem normalmente.
- **Mês sem compras**: não gera fatura a pagar; o mês atual sem compras aparece com total zero.
- **Fatura vencida e limite insuficiente ao mesmo tempo**: a recusa é por fatura vencida.
- **Fatura que vence hoje**: não bloqueia; o bloqueio começa no dia seguinte ao vencimento.
- **Vencimento em dia não útil**: passa para o próximo dia útil, sem juros nesse intervalo.
- **Compra em dia não útil**: é autorizada normalmente; só o lançamento espera o próximo dia útil. A
  fatura é a da data da compra: a de domingo 07/03/2027, lançada em 08/03, entra na fatura de março.
- **Liberação depois do vencimento** (processamento parado): o vencimento da fatura passa a ser o dia da
  liberação, sem juros nem bloqueio pelo atraso do próprio sistema.
- **Fatura liberada no dia do vencimento**: quando os dias 08 e 09 não são úteis, a liberação ocorre no
  próprio vencimento.
- **Horário de verão**: se a 00:00 não existir, o dia começa no primeiro horário válido.
- **Virada de ano**: compras de 08 a 31 de dezembro entram na fatura de janeiro.
- **Cobrança acima do limite total do cartão**: pode ser emitida, mas toda tentativa de pagamento é
  recusada por limite.
- **Cancelamento e pagamento simultâneos**: exatamente um vence; o outro recebe conflito.
- **Mesma chave de idempotência com outro conteúdo**: recusada como nas transferências.
- **Taxa com arredondamento**: 2% de R$ 0,01 é R$ 0,00 e o lojista recebe R$ 0,01.
- **Antecipação no próprio dia previsto**: se o crédito ainda não ocorreu, a antecipação é recusada
  (o valor integral já é devido).
- **Mudança do limite configurado**: vale para cartões novos; os existentes mantêm o seu.
- **Nome do titular do cartão**: é exigido, mas não decide a autorização.
- **Dados do cartão em erros**: mensagens de erro nunca repetem o número ou o CVV enviados.

## Requirements *(mandatory)*

### Functional Requirements

**Cartão**

- **FR-001**: O sistema MUST ter um cadastro fixo de bandeiras: `001` Visa e `002` Mastercard.
- **FR-002**: Só PF MUST poder solicitar cartão, escolhendo obrigatoriamente a bandeira. O cartão
  nasce ativo, sem análise de crédito.
- **FR-003**: Cada CPF MUST ter no máximo um cartão, inclusive com solicitações simultâneas, e a
  bandeira MUST NOT mudar depois da criação.
- **FR-004**: O cartão MUST ter número de 16 dígitos único, válido pelo Luhn e coerente com a
  bandeira, validade (mês e ano) de 5 anos e CVV de 3 dígitos. Os números são fictícios.
- **FR-005**: O limite total MUST ser definido na criação pela configuração do ambiente, com padrão
  R$ 5.000,00, e ficar registrado no cartão.
- **FR-006**: O limite disponível MUST ser o limite total menos todas as compras autorizadas ainda
  não quitadas por pagamento de fatura, lançadas ou não.
- **FR-007**: O titular MUST consultar bandeira, 4 últimos dígitos, validade, limite total, limite
  disponível e total de compras ainda não lançadas. Também MUST poder ver, só ele e autenticado,
  os dados completos do cartão: nome impresso, número, validade e CVV, sem confirmar a senha. O nome
  impresso é o nome do titular, em maiúsculas. Essa resposta MUST NOT ser guardada em cache, e a
  consulta comum do cartão MUST NOT trazer o número completo nem o CVV.
- **FR-008**: Número completo e CVV MUST NOT ser gravados no banco de dados nem aparecer em logs,
  traces, métricas, eventos, notificações ou mensagens de erro. Só os 4 últimos dígitos e a bandeira
  podem ser armazenados e exibidos fora da consulta prevista no FR-007. A informação gravada MUST
  NOT permitir, sozinha, recuperar o número ou o CVV.

**Cobranças**

- **FR-009**: Só PJ MUST poder emitir cobrança, com valor entre R$ 0,01 e o máximo aceito nas
  transferências, até 2 casas decimais, em BRL, e descrição de 1 a 200 caracteres (espaços das
  pontas removidos). A cobrança nasce `Pendente`.
- **FR-010**: A cobrança MUST ter os status `Pendente`, `Paga` e `Cancelada`. `Paga` e `Cancelada`
  são terminais. Uma recusa mantém `Pendente` e registra o motivo e o instante da última recusa.
- **FR-011**: Qualquer usuário autenticado MUST poder consultar uma cobrança pelo identificador,
  vendo lojista (nome e CNPJ), valor, descrição, status e instantes de emissão, pagamento e cancelamento. Só o
  emissor e o pagador veem os 4 últimos dígitos, a bandeira e o código de autorização; só o emissor
  vê a última recusa, sem identificação de quem tentou. A identidade do pagador não é exposta.
- **FR-012**: A PJ MUST listar as cobranças que emitiu, das mais novas para as mais antigas, com
  filtro opcional de status e paginação com padrão 20 e máximo 100 por página.
- **FR-013**: O emissor MUST poder cancelar a própria cobrança `Pendente`. Em outro status recebe
  conflito; cobrança de outra PJ é tratada como inexistente.

**Pagamento e autorização**

- **FR-014**: Só PF MUST poder pagar cobrança, informando nome do titular, número, validade
  (`MM/AA`), CVV e chave de idempotência.
- **FR-015**: Antes de qualquer outra verificação, o sistema MUST validar a estrutura: titular
  preenchido, número válido pelo Luhn, validade bem formada e não vencida (o cartão vale até o fim
  do mês indicado) e CVV com 3 dígitos. Falhas são erro de validação por campo.
- **FR-016**: A bandeira simulada MUST aprovar somente se os dados corresponderem ao cartão do
  próprio pagador, ele não tiver fatura vencida e o valor for menor ou igual ao limite disponível,
  verificados nesta ordem. Cada recusa tem erro próprio: cartão não autorizado (único para qualquer
  dado que não corresponda), cartão bloqueado por fatura vencida e limite insuficiente.
- **FR-017**: Cada decisão da bandeira MUST ser registrada de forma imutável, com cobrança, bandeira,
  4 últimos dígitos, resultado, motivo e instante. Na aprovação, o registro tem também código de
  autorização com 6 caracteres alfanuméricos maiúsculos.
- **FR-018**: A aprovação MUST, numa única operação atômica, marcar a cobrança `Paga` com pagador, 4
  últimos dígitos, bandeira, código e instante; consumir o limite; criar a compra a lançar; criar o
  recebível do lojista; e registrar o evento `cobranca.paga`. Nenhum saldo muda.
- **FR-019**: O pagamento MUST ser idempotente por pagador e chave: a repetição devolve o resultado
  original; a mesma chave com outra cobrança ou outros dados é recusada. A chave vale para
  aprovações; uma recusa não a consome.
- **FR-020**: Pagamentos simultâneos MUST NOT permitir que uma cobrança seja paga duas vezes nem que
  o limite disponível fique negativo.
- **FR-021**: O evento `cobranca.paga` MUST seguir o contrato v1 da spec 010 com o campo opcional
  `lojistaNome` acrescentado por esta spec, publicado pelo mecanismo já existente, exatamente um por
  cobrança paga.

**Processamento diário e faturas**

- **FR-022**: Um processamento MUST iniciar às 00:00 de Brasília de todo dia útil e, nesta ordem,
  lançar as compras feitas até o dia anterior, liberar as faturas fechadas e liquidar os recebíveis
  devidos. Lançar vem antes de liberar em cada cartão, e os recebíveis vêm depois dos cartões. Em dia não útil nada é lançado, liberado nem liquidado; fica para o próximo dia útil.
- **FR-022a**: Dia útil é de segunda a sexta, exceto os feriados nacionais fixos (01/01, 21/04, 01/05,
  07/09, 12/10, 02/11, 15/11, 20/11 e 25/12) e, calculados a partir da Páscoa, Carnaval (segunda e terça),
  Sexta-feira Santa e Corpus Christi. Vale para qualquer ano, sem cadastro manual.
- **FR-023**: O processamento MUST ser reexecutável sem duplicar efeitos, seguro com várias
  instâncias e recuperar dias perdidos quando o sistema esteve parado.
- **FR-024**: A fatura do mês M vence no dia 10 ou, se não for dia útil, no próximo dia útil. Ela MUST
  reunir as compras feitas de 00:00 do dia 08 do mês anterior até o fim do dia 07 do mês M, pela data da
  autorização, mesmo que lançadas depois. O fechamento é sempre no dia 07. Duas exceções técnicas: fatura
  liberada depois do vencimento passa a vencer no dia da liberação; e compra autorizada por um relógio
  atrasado, com a fatura do ciclo já liberada, recebe a data do primeiro dia do ciclo seguinte, que vale
  também para os 30 dias úteis do lojista.
- **FR-025**: A fatura MUST ter a situação `Aberta` (até o fim do dia 07), `Fechada` (do fim do dia
  07 até todas as compras do ciclo serem lançadas), `Disponivel` (pode ser paga), `Vencida`
  (disponível e não paga após o fim do dia do vencimento) ou `Paga`.
- **FR-026**: Cada lançamento da fatura MUST ter nome do estabelecimento (razão social do lojista no
  momento da compra), valor, descrição da cobrança e data da compra, e ser imutável.
- **FR-027**: A PF MUST consultar a qualquer momento a fatura atual e as de meses anteriores, listadas
  por mês, e o detalhe de cada uma com lançamentos, total, fechamento, vencimento e situação. Faturas
  de outra pessoa são tratadas como inexistentes.
- **FR-028**: A PF MUST pagar uma fatura `Disponivel` ou `Vencida` pelo valor total, com o saldo da
  carteira e chave de idempotência. O pagamento é atômico: debita o saldo, registra a saída no
  extrato, marca a fatura `Paga` e libera o limite. Não há pagamento parcial. É permitido em qualquer
  dia, inclusive não útil.
- **FR-028a**: A fatura paga depois do vencimento MUST ter juros simples por atraso, com taxa mensal
  configurável por ambiente e padrão de 15%: total × (taxa ÷ 30) × dias corridos de atraso, contados do
  dia seguinte ao vencimento até o dia do pagamento, inclusive, e arredondado para 2 casas (meio centavo
  para cima). O juro MUST ser cobrado no pagamento da própria fatura e não consome limite. A consulta de
  fatura vencida MUST mostrar dias de atraso, juros e valor a pagar do dia; a fatura paga MUST guardar a
  taxa, os dias, os juros e o valor pago, que não mudam se a configuração mudar.
- **FR-029**: O pagamento de fatura MUST NOT contar para os limites de transferência (spec 009).

**Recebimento do lojista**

- **FR-030**: Cada cobrança paga MUST gerar um recebível no valor integral, com data prevista no 30º
  dia útil seguinte ao dia da compra.
- **FR-031**: Na data prevista, o processamento MUST creditar o valor integral na carteira do lojista,
  uma única vez, com entrada no extrato, e marcar o recebível `Liquidado`.
- **FR-032**: Antes da data prevista, o lojista MUST poder antecipar um recebível `Pendente`, um por
  vez, com chave de idempotência. A taxa é de 2% do valor, arredondada para 2 casas (meio centavo para
  cima), qualquer que seja o prazo restante. O líquido é creditado na hora, com entrada no extrato, e
  o recebível fica `Antecipado`, com bruto, taxa e líquido registrados. A operação não pode ser
  desfeita e é permitida em qualquer dia, inclusive não útil.
- **FR-033**: O lojista MUST listar os seus recebíveis com valor bruto, taxa, líquido, data prevista,
  situação e cobrança de origem, com filtro opcional de situação e paginação.
- **FR-034**: Crédito e débito em carteira MUST seguir as regras já existentes de atomicidade,
  bloqueio e lançamento imutável, e o saldo de cada carteira MUST continuar igual à soma dos seus
  lançamentos.

**Notificações, erros e observabilidade**

- **FR-035**: O consumo de `cobranca.paga` MUST registrar uma notificação para o pagador e uma para o
  lojista, com as garantias das specs 010 e 011, sem dados de cartão.
- **FR-036**: Erros MUST seguir o formato padrão, com `type` estável para: cartão já existente,
  cartão não encontrado, cobrança não encontrada, cobrança não pendente, cartão não autorizado, cartão
  bloqueado por fatura vencida, limite do cartão insuficiente, fatura não encontrada, fatura não
  disponível, recebível não encontrado e recebível não antecipável.
- **FR-037**: O sistema MUST emitir métricas de autorizações por resultado e bandeira, e do
  processamento diário (execuções, compras lançadas, recebíveis liquidados, falhas e duração), sem
  dados pessoais nem de cartão.
- **FR-038**: As funcionalidades existentes MUST continuar sem mudança de comportamento; o extrato da
  carteira passa a ter novos tipos de lançamento, sem alterar os campos atuais, e a tela de extrato
  atual e as movimentações do início MUST exibi-los com rótulo próprio.

### Key Entities

- **Bandeira**: Visa (`001`) ou Mastercard (`002`); autoriza os pagamentos.
- **Cartão**: pertence a uma PF (um por CPF); bandeira imutável, 4 últimos dígitos, validade e limite
  total. Número e CVV não são gravados.
- **Cobrança**: emitida por uma PJ; valor, descrição, status, última recusa e, quando paga, pagador,
  4 últimos dígitos, bandeira, código de autorização e instante.
- **Autorização**: decisão imutável da bandeira sobre uma tentativa de pagamento.
- **Compra**: efeito de uma autorização aprovada para a PF; consome limite e vira lançamento de
  fatura no processamento diário.
- **Fatura**: ciclo mensal de um cartão; período, fechamento (07), vencimento (10 ou próximo dia útil),
  total, juros por atraso e situação.
- **Lançamento de fatura**: item imutável com estabelecimento, valor, descrição e data da compra.
- **Recebível**: direito do lojista sobre uma cobrança paga; bruto, taxa, líquido, data prevista e
  situação (`Pendente`, `Liquidado`, `Antecipado`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma PF sem cartão obtém um pronto para uso com uma única solicitação.
- **SC-002**: A resposta de aprovação ou recusa chega ao pagador em até 2 segundos em 95% dos casos.
- **SC-003**: Com 50 pagamentos simultâneos da mesma cobrança, exatamente um é aprovado; com 50
  solicitações simultâneas da mesma PF, existe um único cartão.
- **SC-004**: O limite disponível nunca fica negativo e, em qualquer momento, é igual ao limite total
  menos as compras não quitadas.
- **SC-005**: 100% das compras aparecem exatamente uma vez na fatura do seu ciclo, inclusive com o
  processamento interrompido, repetido ou atrasado.
- **SC-006**: Nenhuma fatura pode ser paga antes de todas as compras do seu ciclo estarem lançadas.
- **SC-007**: O processamento de 10.000 compras pendentes termina em até 5 minutos.
- **SC-008**: 100% dos recebíveis são creditados uma única vez, pelo valor integral na data prevista
  ou pelo líquido na antecipação.
- **SC-009**: Nenhuma ocorrência de número completo ou CVV em banco de dados, logs, traces, métricas
  ou mensagens após a execução de todos os testes.
- **SC-010**: Todos os testes automatizados das specs anteriores continuam passando.

## Assumptions

- **Frontend fora do escopo**: as telas de cartão, cobranças, checkout, faturas e recebíveis ficam
  para outra issue; a única exceção é o rótulo dos novos lançamentos (FR-038).
- **Só compras à vista**: sem parcelamento, estorno, contestação ou cancelamento de cobrança paga.
- **Ciclo do cartão**: bloqueio pelo titular, cancelamento, segunda via, renovação, alteração de limite
  e troca de bandeira ficam fora do escopo.
- **Atraso**: o limite continua consumido até o pagamento. Não há multa, só juros.
- **Cobrança sem prazo**: a cobrança pendente não expira.
- **`Paga` significa autorizada**: o lojista vê a cobrança paga na hora, mas o dinheiro depende do
  recebível.
- **Origem do dinheiro**: o crédito ao lojista é feito pelo One Bank, que assume o risco de a PF não
  pagar a fatura; não há conta contábil do banco nesta entrega e a taxa de 2% fica só registrada no
  recebível.
- **Prazo curto para pagar**: entre a liberação e o vencimento há de um a três dias, como resulta das
  datas 07 e 10 definidas no contexto.
- **Permissões**: a matriz atual já diz que PF paga e PJ gera cobranças; são acrescentadas as ações
  de cartão e fatura (PF) e de recebíveis (PJ).
- **Contrato do evento**: `cobranca.paga` v1 ainda não tem produtor e pode ser ajustado sem quebra.
- **Dependências**: carteira e razão (spec 006), permissões (spec 003), calendário de Brasília (spec
  009), mensageria (spec 010) e notificações (spec 011).
