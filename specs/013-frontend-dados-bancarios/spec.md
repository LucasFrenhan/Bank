# Feature Specification: Frontend — dados bancários da conta e nome One Bank

**Feature Branch**: `013-frontend-dados-bancarios`

**Created**: 2026-11-24

**Status**: Draft

**Input**: Issue #51 — "Frontend: exibir dados bancários da conta e nome One Bank". A spec
[012-dados-bancarios-carteira](../012-dados-bancarios-carteira/spec.md) passou a entregar os dados
bancários na consulta da própria carteira
([contrato 1.1.0](../012-dados-bancarios-carteira/contracts/carteira-minha.md)) e deixou a
interface para esta entrega. Aqui, a tela inicial (PF e PJ) passa a mostrar banco, agência,
conta com dígito e tipo da conta, com a ação de copiar, e a interface passa a usar o nome do
nosso banco, "One Bank", com o novo ícone "1B". Nenhuma mudança na API.

## Clarifications

### Session 2026-11-24

- Q: Incluir a ação de copiar os dados bancários (RN-04 da issue, opcional)? → A: Sim, copiando
  só os dados bancários (banco, agência, tipo e conta com dígito), sem nome nem documento do
  titular.
- Q: O que fazer com o ícone "B" da marca, agora que o nome é "One Bank"? → A: O novo ícone é
  "1B".
- Q: Onde e como o bloco "Dados da conta" aparece na tela inicial? → A: Em cartão próprio, sempre
  visível, entre o saldo e as movimentações; no desktop, ao lado do saldo; no celular, abaixo dele.
- Q: Na PJ, como organizar o cartão "Dados da conta" com o "Receber transferências" (CNPJ)? → A:
  Dois cartões empilhados ao lado do saldo, "Receber transferências" primeiro e "Dados da conta"
  abaixo; no celular: saldo, CNPJ, dados da conta.
- Q: O cartão "Dados da conta" explica que transferências entre clientes One Bank são feitas por
  documento? → A: Sim, em PF e PJ: "Para receber de clientes One Bank, informe o seu CPF." (na PJ,
  "CNPJ").

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes já autenticados (spec 004), Pessoa Física ou Pessoa Jurídica, e, na
história da marca, também visitantes nas telas públicas (entrar, criar conta, não encontrada).

### User Story 1 - Ver os dados bancários da própria conta (Priority: P1)

Uma pessoa autenticada (PF ou PJ) abre a tela inicial e vê os dados bancários da sua conta —
banco (código e nome), agência, conta com dígito e tipo —, para poder informá-los a quem precisa
deles.

**Why this priority**: é o motivo da issue; hoje os dados existem, mas o titular não os vê.

**Independent Test**: entrar com uma PF e com uma PJ recém-cadastradas e com uma conta poupança
criada por script e conferir, em desktop e mobile, que os dados exibidos são exatamente os
entregues pelo banco para aquela carteira.

**Acceptance Scenarios**:

1. **Given** uma PF autenticada, **When** abre a tela inicial, **Then** vê "Dados da conta" com
   Banco "001 – One Bank", Agência "0001", Conta "00000042-0" (número-dígito da carteira) e Tipo
   "Conta corrente".
2. **Given** uma PJ autenticada, **When** abre a tela inicial, **Then** vê os mesmos campos da sua
   conta abaixo do bloco "Receber transferências", cujo CNPJ e ação de copiar continuam como estão
   (só o nome do banco muda, conforme a US2).
3. **Given** uma conta poupança, **When** o titular abre a tela inicial, **Then** o tipo exibido é
   "Conta poupança".
4. **Given** agência, número ou dígito com zeros (à esquerda ou iguais a zero), **When** exibidos,
   **Then** todos os dígitos aparecem como entregues pelo banco (`0001`, `00000042-0`).
5. **Given** um leitor de tela, **When** percorre os dados da conta, **Then** cada valor é
   associado ao seu rótulo, e agência e conta são lidas dígito a dígito, com o dígito verificador
   identificado, e não como números por extenso.
6. **Given** os dados da conta visíveis, **When** a pessoa lê o bloco, **Then** vê "Para receber de
   clientes One Bank, informe o seu CPF." (PF) ou "…informe o seu CNPJ." (PJ).
7. **Given** a carteira carregando ou com erro, **When** a tela inicial é exibida, **Then** vale o
   comportamento atual da tela, sem dados bancários parciais.

---

### User Story 2 - Reconhecer o banco como One Bank (Priority: P2)

Visitantes e clientes veem o nome "One Bank" e o ícone "1B" em toda a interface — marca, aba do
navegador, textos e comprovante compartilhado —, coerente com o nome nos dados da conta.

**Why this priority**: sem isso a tela mostraria dois nomes para o mesmo banco.

**Independent Test**: percorrer todas as telas públicas e autenticadas, e o comprovante
compartilhado, e conferir que nenhuma mostra o nome antigo ("Bank" sem "One") nem o ícone "B".

**Acceptance Scenarios**:

1. **Given** qualquer tela que exibe a marca, pública ou autenticada, **When** aberta, **Then**
   mostra o ícone "1B" e o nome "One Bank".
2. **Given** qualquer tela, **When** se olha a aba do navegador, **Then** o título termina em
   "· One Bank" (ou é "One Bank" antes de a tela carregar) e o ícone é "1B".
3. **Given** um documento sem conta, **When** a transferência o recusa, **Then** a mensagem é
   "Nenhuma conta One Bank com esse documento."
4. **Given** um comprovante, **When** é compartilhado ou copiado, **Then** o resumo começa com
   "Comprovante de transferência — One Bank".
5. **Given** o início PJ, **When** exibido, **Then** o texto diz "Clientes com conta One Bank…".
6. **Given** um endereço inexistente, **When** aberto, **Then** o texto diz "O endereço acessado
   não existe no One Bank."

---

### User Story 3 - Copiar os dados bancários (Priority: P3)

Na tela inicial, a pessoa copia de uma vez os dados bancários para colar numa mensagem.

**Why this priority**: conveniência sobre a US1; a pessoa já consegue ler e informar os dados.

**Independent Test**: acionar "Copiar" com mouse, teclado e toque e conferir o texto colado e a
confirmação; repetir com a cópia bloqueada pelo navegador.

**Acceptance Scenarios**:

1. **Given** os dados da conta visíveis, **When** a pessoa escolhe "Copiar", **Then** a área de
   transferência recebe exatamente três linhas — `Banco 001 – One Bank`, `Agência 0001` e
   `Conta corrente 00000042-0` — com os valores da própria conta, e a tela confirma "Dados da
   conta copiados", anunciado a leitores de tela.
2. **Given** a cópia indisponível ou negada pelo navegador, **When** a pessoa escolhe "Copiar",
   **Then** aparece "Não foi possível copiar. Selecione os dados e copie manualmente.", e os dados
   continuam visíveis e selecionáveis.
3. **Given** o texto copiado, **When** conferido, **Then** não contém nome, CPF/CNPJ, saldo nem
   identificador interno.

---

### Edge Cases

- Nome de banco longo: quebra de linha, sem cortar nem rolar a tela na horizontal.
- Tipo de conta ainda desconhecido da interface (novo valor no futuro): a tela não quebra; o tipo
  é exibido e copiado como "Conta", sem texto técnico.
- Cópia repetida: cada acionamento copia de novo e a confirmação continua correta.
- Na PJ há duas ações de copiar (CNPJ e dados da conta): cada uma tem nome acessível próprio, e a
  confirmação de uma não aparece como se fosse da outra.
- "Tipo de conta" do titular (Pessoa Física/Jurídica) e tipo da conta bancária (corrente/poupança)
  aparecem com rótulos que não se confundem.
- Textos em que "bank" não é o nome exibido do banco (identificadores internos, nomes técnicos,
  chaves de armazenamento) não mudam.

## Requirements *(mandatory)*

### Functional Requirements

**Dados da conta**

- **FR-001**: A tela inicial de PF e de PJ MUST exibir, num bloco "Dados da conta", o banco (código
  e nome), a agência, a conta no formato número-dígito e o tipo ("Conta corrente" ou "Conta
  poupança") da conta da pessoa autenticada.
- **FR-001a**: O bloco MUST ser um cartão próprio, sempre visível (sem recolher nem ocultar valores),
  entre o saldo e as movimentações: ao lado do saldo no desktop e abaixo dele no celular. Na PJ, vem
  logo abaixo do cartão "Receber transferências", empilhado na mesma coluna no desktop e em seguida a
  ele no celular.
- **FR-002**: Os valores dos campos do cartão (Banco, Agência, Conta, Tipo) MUST vir da consulta da
  própria carteira, inclusive o nome do banco; nenhum MUST ser fixo na interface, guardado no
  navegador ou recalculado (em especial o dígito). Montar `número-dígito` e traduzir o tipo é
  apresentação e é permitido. O texto de apoio do FR-011 usa o nome da marca (FR-008).
- **FR-003**: Código do banco, agência, número e dígito MUST ser tratados como texto do início ao
  fim, preservando zeros.
- **FR-004**: Cada valor MUST ter rótulo associado, e agência e conta MUST ter forma acessível lida
  dígito a dígito, com o dígito verificador identificado.
- **FR-005**: Um tipo de conta desconhecido MUST ser apresentado como "Conta", sem erro.

**Copiar**

- **FR-006**: O bloco MUST ter a ação "Copiar", com nome acessível "Copiar dados da conta", que
  copia exatamente as três linhas da US3 e nada além delas.
- **FR-007**: O resultado da cópia MUST ser mostrado na tela e anunciado a leitores de tela, com as
  mensagens da US3 para sucesso e falha, e a falha MUST usar o estilo de erro, não o de sucesso; o mesmo
  vale para a cópia do CNPJ, cujos textos não mudam.

**Marca**

- **FR-008**: O nome do banco MUST ser "One Bank" em todo texto exibido ou compartilhado pela
  interface: marca, título e descrição da página, mensagens, textos das telas e resumo do
  comprovante.
- **FR-009**: O ícone da marca, o ícone da aba e o ícone de atalho no celular MUST ser "1B", branco
  sobre a cor primária, com "1" e "B" distinguíveis numa captura de 16 × 16 px (menor tamanho, na
  aba).
- **FR-010**: O ícone da marca MUST continuar decorativo: o nome acessível da marca é "One Bank".

**Escopo, acessibilidade e qualidade**

- **FR-011**: A entrega MUST NOT alterar a API, o cadastro nem a transferência, que continua por
  documento; para deixar isso claro, o bloco "Dados da conta" MUST exibir o texto "Para receber de
  clientes One Bank, informe o seu CPF." na PF e "Para receber de clientes One Bank, informe o seu
  CNPJ." na PJ, sem o número do documento. Esse texto não faz parte do que é copiado.
- **FR-012**: As telas alteradas MUST seguir a spec 004: WCAG 2.2 AA (teclado, foco visível, alvos
  ≥ 44 px, contraste ≥ 4,5:1), sem rolagem horizontal entre 320 e 1920 px.
- **FR-013**: A telemetria do navegador MUST NOT incluir os dados bancários (agência, número ou
  dígito), inclusive na ação de copiar.
- **FR-014**: A exibição (PF, PJ, corrente, poupança), a cópia (sucesso e falha) e o nome novo MUST
  ter testes de componentes; a exibição e a cópia MUST ser verificadas ponta a ponta contra a API
  real, em desktop e mobile, no CI.
- **FR-015**: O documento do protótipo MUST registrar o nome "One Bank", o ícone "1B" e o bloco
  "Dados da conta", inexistentes nas telas originais.

### Key Entities

- **Dados da conta (visão)**: banco (código e nome), agência, número, dígito e tipo da conta
  bancária, como entregues na consulta da própria carteira; só leitura.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% das contas (PF, PJ, corrente e poupança) veem na tela inicial os dados idênticos
  aos registrados, sem nenhum dígito perdido.
- **SC-002**: A pessoa encontra os dados da conta logo após entrar, sem nenhuma navegação
  adicional, e os copia com um único acionamento.
- **SC-003**: Nenhuma tela, título, mensagem ou texto compartilhado mostra o nome antigo ou o ícone
  "B".
- **SC-004**: Nenhuma violação de acessibilidade (WCAG 2.2 AA) na verificação automática das telas
  alteradas, em desktop e mobile.
- **SC-005**: Depois de exibir e copiar os dados da conta, nenhum envio de telemetria do navegador
  contém o número da conta (isolado ou como `número-dígito`) nem atributos que identifiquem a agência
  ou a conta bancária.
- **SC-006**: Todos os testes de componentes e ponta a ponta passam no CI.

## Assumptions

- A consulta da própria carteira na versão 1.1.0 (spec 012, PR #50) já está disponível e
  `contaBancaria` está sempre presente.
- Os valores não são ocultados por serem dados que o titular informa a terceiros; o visual do cartão
  segue os estilos existentes.
- Os dados da conta ficam só na tela inicial; Perfil, Extrato e Comprovante não os exibem.
- O ícone muda só no texto ("1B"); cor, formato e tamanho continuam os do ícone atual.
- As fontes do protótipo (`docs/design/fontes` e `docs/design/telas`) são registro histórico e não
  são refeitas; a mudança fica documentada em `docs/design/prototipo.md`.
- O nome do projeto e do repositório ("Bank"), os nomes técnicos e as specs anteriores não mudam.
- Receber de outros bancos e transferir por agência e conta continuam fora do escopo.
