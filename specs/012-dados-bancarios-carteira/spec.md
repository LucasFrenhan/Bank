# Feature Specification: Dados bancários da carteira

**Feature Branch**: `012-dados-bancarios-carteira`

**Created**: 2026-11-23

**Status**: Draft

**Input**: Issue #28 ("Geração e Exposição de Dados Bancários na Carteira", TASK-15). Hoje a
carteira de cada usuário é identificada só por um identificador interno; não tem banco, agência,
número de conta nem tipo de conta. Esta entrega dá a toda carteira dados bancários no formato
usual brasileiro — código do banco, agência, número da conta com dígito verificador e tipo da
conta (corrente ou poupança) —, preenche esses dados para todas as carteiras que já existem,
passa a gerá-los automaticamente no cadastro e os expõe na consulta da própria carteira. Para
simplificar, existem apenas cinco bancos: o nosso, de código 001, e outros quatro com nomes
simbólicos, que servem para registrar contas de outros bancos e simular uma "integração" com
eles. O documento `specs/06-dados-bancarios-carteira.md` citado na issue não existe no
repositório; esta spec o substitui.

## Clarifications

### Session 2026-11-23

- Q: Como as contas dos bancos 002–005 devem existir no sistema? → A: Registro independente
  (banco, agência, número, tipo, nome e documento do titular), sem usuário, login nem saldo.
- Q: A conta poupança criada por script pertence a quem? → A: A um novo titular no banco 001,
  cuja única conta é a poupança; a regra de uma carteira por usuário é mantida.
- Q: Os números de conta são gerados em sequência ou de forma aleatória? → A: Em sequência,
  por um contador único no banco de dados, usado também pela atualização das contas antigas e
  pelo script de poupança.
- Q: Na consulta da carteira, os dados bancários vêm agrupados ou como campos soltos? → A:
  Agrupados em `contaBancaria`: `{ banco: { codigo, nome }, agencia, numero, digito, tipo }`.
- Q: Quais nomes os cinco bancos devem ter? → A: 001 "One Bank" (o nosso), 002 "Banco
  Aurora", 003 "Banco Horizonte", 004 "Banco Atlântico", 005 "Banco Cerrado". O frontend deve
  passar a usar o nome "One Bank" na task futura de atualização do frontend.
- Q: Os dados bancários aparecem só na consulta da própria carteira ou também em outras
  respostas? → A: Só na consulta da própria carteira; as demais respostas não mudam.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Nova conta nasce com dados bancários (Priority: P1)

Quem se cadastra recebe, junto com a carteira, os dados bancários completos da conta no nosso
banco (001): agência, número da conta com dígito verificador e tipo **conta corrente**. O
cadastro não oferece escolha de tipo; conta corrente é o padrão do sistema.

**Why this priority**: é a regra que vale daqui para frente para toda conta nova; sem ela, o
restante (consulta, contas existentes) não tem o que mostrar nem o padrão a seguir.

**Independent Test**: cadastrar uma PF e uma PJ e verificar que cada carteira tem banco 001,
agência, número de conta válido (dígito verificador confere), tipo conta corrente, e que os
dois números são diferentes.

**Acceptance Scenarios**:

1. **Given** um visitante com dados de cadastro válidos, **When** ele se cadastra (PF ou PJ),
   **Then** a carteira criada tem banco 001, agência 0001, um número de conta inédito com
   dígito verificador correto e tipo conta corrente.
2. **Given** vários cadastros feitos ao mesmo tempo, **When** todos concluem, **Then** nenhum
   par de contas do mesmo banco e agência tem o mesmo número.
3. **Given** um cadastro recusado por qualquer validação já existente (e-mail ou documento
   repetido, senha fraca), **When** ele falha, **Then** nenhum número de conta fica associado a
   uma carteira e o comportamento de erro continua igual ao de hoje.

---

### User Story 2 - Contas existentes ganham dados bancários (Priority: P1)

Todas as carteiras criadas antes desta entrega passam, na atualização, a ter dados bancários no
banco 001, agência 0001, com número de conta único e tipo conta corrente — sem perder saldo,
histórico de transferências ou qualquer outro dado.

**Why this priority**: a mudança altera a estrutura atual; se as contas existentes ficarem sem
dados bancários, os campos não podem ser obrigatórios e a consulta da carteira quebra para
quem já tem conta.

**Independent Test**: com carteiras criadas antes da atualização (com saldo e transferências),
aplicar a atualização e verificar que todas têm dados bancários completos e únicos e que
saldos e extratos são os mesmos de antes.

**Acceptance Scenarios**:

1. **Given** N carteiras existentes sem dados bancários, **When** a atualização é aplicada,
   **Then** as N carteiras têm banco 001, agência 0001, número de conta válido e tipo conta
   corrente, e os N números são distintos.
2. **Given** carteiras com saldo e transferências, **When** a atualização é aplicada, **Then**
   saldo, lançamentos e transferências de cada uma permanecem idênticos.
3. **Given** a atualização já aplicada, **When** um novo cadastro acontece, **Then** o número
   gerado não colide com nenhum dos números atribuídos às contas antigas.
4. **Given** um banco de dados vazio (ambiente novo), **When** a atualização é aplicada,
   **Then** ela conclui sem erro.

---

### User Story 3 - Consultar os dados bancários da própria conta (Priority: P2)

O titular autenticado vê, na consulta da própria carteira, os dados bancários da conta: código
e nome do banco, agência, número da conta com dígito e tipo da conta, além do que já é
exibido hoje.

**Why this priority**: é o que torna os dados úteis para o usuário (informar a conta a quem
vai pagá-lo), mas depende das histórias 1 e 2.

**Independent Test**: autenticar com uma conta e consultar a própria carteira; conferir que os
dados bancários batem com os gravados e que os campos anteriores continuam presentes.

**Acceptance Scenarios**:

1. **Given** um titular autenticado, **When** consulta a própria carteira, **Then** a resposta
   traz banco (código 001 e nome), agência, número da conta com dígito e tipo da conta, além
   de todos os campos que já existiam.
2. **Given** um titular cuja conta foi atualizada pela história 2, **When** consulta a
   carteira, **Then** vê os dados atribuídos na atualização.
3. **Given** uma requisição sem autenticação, **When** consulta a carteira, **Then** o
   comportamento continua o de hoje (recusada).

---

### User Story 4 - Contas de outros bancos e conta poupança por script (Priority: P3)

A equipe consegue, por scripts de banco de dados (fora do sistema), registrar contas em
qualquer um dos cinco bancos e criar contas do tipo poupança. As contas dos bancos 002 a 005
são apenas registros que simulam a existência de outros bancos para uma futura integração.

**Why this priority**: prepara a simulação de integração com outros bancos e o uso de
poupança, mas nenhuma funcionalidade do sistema depende disso nesta entrega.

**Independent Test**: executar os scripts num ambiente com a atualização aplicada e verificar
que as contas de outros bancos e a conta poupança existem, respeitam a unicidade e não afetam
cadastro, login, consulta e transferências.

**Acceptance Scenarios**:

1. **Given** o ambiente atualizado, **When** o script de contas de outros bancos é executado,
   **Then** existe ao menos uma conta registrada em cada banco de 002 a 005, com agência,
   número com dígito verificador e tipo.
2. **Given** o script de conta poupança, **When** ele é executado, **Then** existe uma conta
   do tipo poupança com dados bancários válidos.
3. **Given** os scripts já executados, **When** são executados de novo, **Then** não criam
   duplicatas nem falham.
4. **Given** contas de outros bancos registradas, **When** alguém tenta entrar ou transferir
   usando-as, **Then** nada muda: elas são registros independentes, sem usuário, login nem
   saldo, e não são acessíveis pelo sistema nesta entrega.
5. **Given** o script de conta poupança executado, **When** o titular criado por ele entra no
   sistema e consulta a carteira, **Then** vê a conta no banco 001 com tipo conta poupança.

---

### Edge Cases

- Dois cadastros simultâneos não podem receber o mesmo número de conta; como o número vem de
  um contador sequencial único, não há colisão nem nova tentativa.
- O número gerado precisa ter o dígito verificador correto também para as contas atribuídas
  na atualização das contas existentes.
- Tentativa de gravar (por script) uma conta com código de banco fora dos cinco conhecidos ou
  com tipo diferente de corrente/poupança é recusada.
- Tentativa de gravar (por script) duas contas com o mesmo banco, agência e número é recusada.
- A conta poupança criada por script pertence a um novo titular, cuja única conta é a
  poupança no banco 001; a regra de uma carteira por usuário continua valendo. O script não
  altera o tipo de contas existentes.
- Uma conta registrada de outro banco pode ter o mesmo documento de titular de um usuário do
  nosso cadastro (a mesma pessoa com conta em dois bancos); isso não cria vínculo entre eles.
- O nome do banco exibido vem do cadastro dos cinco bancos; alterar o nome simbólico de um
  banco não muda os dados das contas.
- Transferências continuam sendo feitas pelo documento do destinatário; os dados bancários não
  são usados para localizar o destinatário nesta entrega.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O sistema MUST manter um cadastro fixo de exatamente cinco bancos, cada um com
  código de 3 dígitos e nome: 001 "One Bank" (o nosso banco), 002 "Banco Aurora", 003 "Banco
  Horizonte", 004 "Banco Atlântico" e 005 "Banco Cerrado" (os quatro últimos são simbólicos).
- **FR-002**: Toda carteira MUST ter, de forma obrigatória, código do banco (um dos cinco),
  agência (4 dígitos), número da conta (8 dígitos) com dígito verificador (1 dígito) e tipo da
  conta (conta corrente ou conta poupança).
- **FR-003**: A combinação banco + agência + número da conta MUST ser única.
- **FR-004**: O dígito verificador MUST ser calculado a partir dos dígitos da agência e do
  número da conta por uma regra única e documentada, de modo que um número digitado errado em
  um dígito seja detectado.
- **FR-005**: Ao concluir um cadastro de usuário (PF ou PJ), o sistema MUST criar a carteira
  com banco 001, agência 0001, um número de conta inédito com dígito verificador e tipo conta
  corrente, na mesma operação que cria o usuário (nada é gravado se o cadastro falhar).
- **FR-006**: O cadastro MUST NOT aceitar a escolha do tipo de conta nesta entrega; o tipo é
  sempre conta corrente quando a conta é criada pelo sistema.
- **FR-007**: A atualização MUST atribuir dados bancários (banco 001, agência 0001, número
  inédito com dígito verificador, tipo conta corrente) a todas as carteiras existentes,
  preservando todos os demais dados, e só então tornar os campos obrigatórios.
- **FR-008**: Os números de conta do banco 001 MUST ser sequenciais (o próximo número
  disponível de um contador único), começando em 00000001; a atualização das contas
  existentes, os novos cadastros e o script de poupança usam o mesmo contador, de modo que
  nunca colidem, mesmo com cadastros simultâneos. Números consumidos por cadastros que falharam
  podem ficar sem uso (lacunas são aceitas).
- **FR-009**: A consulta da própria carteira MUST expor os dados bancários agrupados em um
  objeto `contaBancaria` — `banco` (`codigo` de 3 dígitos e `nome`), `agencia` (4 dígitos),
  `numero` (8 dígitos), `digito` (1 dígito) e `tipo` (`ContaCorrente` ou `ContaPoupanca`),
  todos como texto para preservar zeros à esquerda —, mantendo todos os campos atuais com o
  mesmo nome e significado (o `tipoConta` PF/PJ continua sendo o tipo do titular). Nenhuma
  outra resposta da API (dados do usuário, cadastro, comprovante e consulta de transferência,
  extrato) passa a expor dados bancários nesta entrega.
- **FR-010**: O sistema MUST fornecer scripts de banco de dados, reexecutáveis sem duplicar
  dados, para (a) registrar ao menos uma conta em cada banco de 002 a 005 e (b) criar um novo
  titular no banco 001 cuja única conta é do tipo poupança; nenhuma funcionalidade do sistema
  cria conta poupança nesta entrega.
- **FR-011**: As contas dos bancos 002 a 005 MUST ser registros independentes das carteiras —
  com banco, agência, número, dígito verificador, tipo e nome e documento do titular, sem
  usuário, senha nem saldo — e MUST NOT permitir login, consulta de carteira ou transferência
  nesta entrega.
- **FR-014**: As carteiras do nosso cadastro MUST pertencer sempre ao banco 001, e as contas
  registradas de outros bancos MUST pertencer a um dos bancos 002 a 005.
- **FR-012**: As funcionalidades existentes (cadastro, login, consulta de usuário, transferência
  por documento, extrato, limites, mensageria e notificações) MUST continuar funcionando sem
  mudança de comportamento.
- **FR-013**: Os dados bancários MUST NOT ser escritos em logs ou telemetria além do que já é
  permitido para identificadores (seguindo a mesma regra de dados sensíveis das specs
  anteriores).

### Key Entities

- **Banco**: um dos cinco bancos conhecidos; código de 3 dígitos (001 a 005) e nome. O 001 é o
  nosso banco; os demais têm nomes simbólicos e existem para simular outros bancos.
- **Carteira (conta)**: passa a ter código do banco, agência, número da conta, dígito
  verificador e tipo da conta, além do que já tem (titular, saldo, moeda, atualização). A
  combinação banco + agência + número identifica a conta de forma única.
- **Conta de outro banco (registro)**: conta registrada em um dos bancos 002 a 005 para
  simular a integração; tem banco, agência, número com dígito verificador, tipo da conta e nome
  e documento do titular. Não tem usuário, senha nem saldo, e não se liga a nenhuma carteira.
  A combinação banco + agência + número é única.
- **Tipo da conta**: conta corrente (padrão do sistema) ou conta poupança (só por script nesta
  entrega).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das contas criadas por cadastro após a entrega têm banco 001, agência 0001,
  tipo conta corrente e número com dígito verificador válido.
- **SC-002**: 100% das contas existentes antes da entrega têm dados bancários completos após a
  atualização, com saldo e histórico idênticos aos de antes.
- **SC-003**: Zero números de conta repetidos no mesmo banco e agência, inclusive com 50
  cadastros simultâneos.
- **SC-004**: O titular vê os dados bancários da própria conta na consulta da carteira em uma
  única requisição, sem passos adicionais.
- **SC-005**: Executar os scripts de outros bancos e de poupança duas vezes seguidas resulta no
  mesmo conjunto de contas que executá-los uma vez.
- **SC-006**: Todos os testes automatizados das specs anteriores continuam passando.

## Assumptions

- Agência única 0001 para o nosso banco nesta entrega; múltiplas agências ficam para o futuro.
- Número da conta com 8 dígitos e dígito verificador de 1 dígito (exibido como `12345678-9`);
  a regra de cálculo (módulo 11 é a referência) é decidida no plano e documentada.
- Os códigos 002 a 005 e seus nomes simbólicos são fictícios e não correspondem a bancos reais.
- A exibição dos dados bancários no frontend fica fora do escopo; esta entrega muda apenas o
  contrato da consulta da carteira (campos novos, compatível com os clientes atuais). A
  atualização do frontend será feita em outra task, de preferência depois que esta for
  finalizada (contrato da API já estável). Essa task também deve passar a exibir o nome do
  nosso banco como "One Bank".
- A escolha do tipo de conta no cadastro é funcionalidade futura; o modelo já suporta os dois
  tipos.
- Os dados bancários não substituem o documento como forma de identificar o destinatário de
  uma transferência; isso pode vir em entrega futura.
