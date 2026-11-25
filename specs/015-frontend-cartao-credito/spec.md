# Feature Specification: Frontend de cartão de crédito, cobranças, faturas e recebíveis

**Feature Branch**: `015-frontend-cartao-credito`

**Created**: 2026-11-24

**Status**: Draft

**Input**: Issue #54 ("Frontend: cartão de crédito, cobranças, faturas e recebíveis"). Telas para a API
entregue pela [spec 014](../014-cartao-credito-cobrancas/spec.md) (issue #22, PR #53). A Pessoa Física
(PF) solicita e usa o cartão, paga cobranças e paga faturas; a Pessoa Jurídica (PJ) emite e acompanha
cobranças e recebe ou antecipa os recebíveis. Regras e dados vêm do
[contrato da spec 014](../014-cartao-credito-cobrancas/contracts/cartoes-cobrancas-api.md), que é a
fonte da verdade. A única mudança na API, aditiva, é o código curto da cobrança, pedido pelo responsável
para deixar o link amigável; a issue previa não mudar a API.

## Clarifications

### Session 2026-11-24

- Q: Como definir o visual das telas, que não existem no protótipo? → A: Um protótipo navegável das
  telas novas é feito e aprovado pelo responsável antes do plano.
- Q: Onde as telas entram no menu? → A: Um item por perfil. A PF ganha "Cartão", com as faturas dentro;
  a PJ ativa "Cobranças", com abas Cobranças e Recebíveis. A barra do celular tem 4 itens nos dois perfis.
- Q: Como a PF informa os dados do cartão no pagamento? → A: Formulário com nome, número, validade e CVV,
  mais "Usar meu cartão", que preenche nome, número e validade. O CVV é sempre digitado.
- Q: Como exibir e ocultar os dados do cartão? → A: Por um ícone de olho no próprio cartão, que muda conforme
  o estado: olho aberto com os dados visíveis e olho riscado com os dados ocultos.
- Q: Como deixar o link da cobrança amigável? → A: Cada cobrança ganha um código curto de 8 caracteres
  fáceis de ler e ditar (ex.: `7K3F-9QXM`), usado no link (`/c/7K3F-9QXM`). Exige uma mudança pequena e
  aditiva na API, feita nesta spec, inclusive o código das cobranças já existentes.
- Q: A PJ vê a cobrança mudar para "Paga" sozinha? → A: Sim. O detalhe de cobrança pendente se atualiza a
  cada 5 segundos com a aba visível e anuncia o pagamento.
- Q: O início da PF avisa sobre fatura para pagar? → A: Sim, só quando há fatura disponível (valor e
  vencimento) ou vencida (valor do dia e cartão bloqueado), com link para a fatura.
- Q: O que vai na mensagem ao copiar ou compartilhar a cobrança? → A: "Copiar link" copia só o link;
  "Compartilhar" envia texto com lojista, valor, descrição e link.

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes One Bank autenticados. Todas as telas existem em desktop e mobile e usam o
design system atual, com textos em português.

### User Story 1 - Solicitar e ver o cartão (Priority: P1)

A PF abre "Cartão". Se ainda não tem um, escolhe a bandeira e o solicita. Depois vê o cartão, os limites
e, quando quiser, exibe nome, número, validade e CVV num cartão desenhado na tela.

**Why this priority**: sem cartão a PF não usa nenhuma das demais telas.

**Independent Test**: com uma PF nova, solicitar o cartão Visa, conferir limites, exibir e ocultar os
dados e verificar que nada ficou guardado no navegador.

**Acceptance Scenarios**:

1. **Given** uma PF sem cartão, **When** abre "Cartão", **Then** vê a explicação, as opções Visa e
   Mastercard e o aviso de que a bandeira não pode ser trocada e de que há um cartão por CPF.
2. **Given** a bandeira escolhida, **When** a PF confirma a solicitação, **Then** vê o cartão emitido,
   com limite total e disponível de R$ 5.000,00.
3. **Given** nenhuma bandeira escolhida (nenhuma vem marcada), **When** a PF tenta solicitar, **Then** vê o erro no campo e
   nada é enviado.
4. **Given** uma PF com cartão, **When** abre "Cartão", **Then** vê o cartão com os dados ocultos
   (bandeira, nome, 4 últimos dígitos e validade), limite total, limite disponível, limite usado, o
   total de compras que ainda não entraram na fatura e a fatura atual, com o caminho para as faturas.
5. **Given** os dados ocultos (olho riscado), **When** a PF aciona o ícone de olho, **Then** o cartão na
   tela mostra nome, número completo, validade e CVV, sem pedir senha, e o ícone vira o olho aberto;
   acioná-lo de novo oculta os dados.
6. **Given** os dados exibidos, **When** a PF os oculta, sai da tela, troca de aba do navegador ou passa
   1 minuto, **Then** eles deixam de ser exibidos.
7. **Given** os dados exibidos, **When** a PF escolhe "Copiar número", **Then** só o número é copiado e
   a tela confirma.
8. **Given** duas abas abertas, **When** a PF solicita nas duas, **Then** a segunda informa que o cartão
   já existe e o mostra.
9. **Given** uma PJ, **When** abre o endereço de "Cartão", **Then** vê que a área é só para PF, com link
   para o início.

---

### User Story 2 - Emitir e acompanhar cobranças (Priority: P1)

A PJ abre "Cobranças", emite uma cobrança, compartilha o link com o cliente, acompanha a situação e
cancela as que ainda estão pendentes.

**Why this priority**: sem cobrança emitida não há o que pagar.

**Independent Test**: com uma PJ, emitir uma cobrança, copiar o link, vê-la na lista com filtro, abrir o
detalhe e cancelá-la.

**Acceptance Scenarios**:

1. **Given** uma PJ sem cobranças, **When** abre "Cobranças", **Then** vê o estado vazio com a ação
   "Nova cobrança".
2. **Given** a tela de nova cobrança, **When** a PJ informa valor e descrição válidos, **Then** a
   cobrança é emitida e aparece o detalhe com o código curto e o link para compartilhar.
3. **Given** valor ou descrição inválidos, **When** a PJ envia, **Then** vê o erro em cada campo e o que
   digitou é mantido.
4. **Given** uma cobrança pendente, **When** a PJ escolhe "Copiar link", **Then** o endereço curto de
   pagamento é copiado; no celular com suporte, há também "Compartilhar", que envia um texto com lojista,
   valor, descrição e link.
5. **Given** cobranças de várias situações, **When** a PJ filtra por Pendentes, Pagas ou Canceladas,
   **Then** vê só as da situação, das mais novas para as mais antigas, em páginas (desktop) ou com
   "Carregar mais" (mobile).
6. **Given** uma cobrança paga, **When** a PJ abre o detalhe, **Then** vê bandeira, final do cartão,
   código de autorização, data do pagamento e o link para os recebíveis; nunca a identidade do pagador.
7. **Given** uma cobrança com recusa, **When** a PJ abre o detalhe, **Then** vê o motivo e o momento da
   última recusa.
8. **Given** uma cobrança pendente, **When** a PJ escolhe "Cancelar cobrança" e confirma, **Then** ela
   fica cancelada e deixa de oferecer o link.
9. **Given** uma cobrança emitida antes desta entrega, **When** a PJ abre o detalhe, **Then** ela também
   tem código e link.
10. **Given** uma cobrança que acabou de ser paga, **When** a PJ tenta cancelá-la, **Then** vê que não é
   mais possível e o detalhe mostra a situação atual.
11. **Given** o detalhe de uma cobrança pendente aberto e visível, **When** o cliente paga, **Then** em até
    10 segundos a tela mostra "Paga", sem ação da PJ, e a mudança é anunciada.

---

### User Story 3 - Pagar uma cobrança com o cartão (Priority: P1)

A PF abre o link da cobrança, confere lojista, valor e descrição, informa os dados do cartão e paga. Vê a
aprovação com o comprovante ou o motivo da recusa, sem risco de pagar duas vezes.

**Why this priority**: é o fluxo central da issue.

**Independent Test**: abrir o link de uma cobrança pendente, pagar com "Usar meu cartão" + CVV e conferir
o comprovante e o novo limite; repetir com CVV errado e com valor acima do limite.

**Acceptance Scenarios**:

1. **Given** o link de uma cobrança pendente, **When** uma PF o abre, **Then** vê lojista (nome e CNPJ),
   valor, descrição e o formulário do cartão.
2. **Given** uma pessoa não autenticada, **When** abre o link, **Then** entra na conta e volta à mesma
   cobrança.
3. **Given** o formulário, **When** a PF escolhe "Usar meu cartão", **Then** nome, número e validade são
   preenchidos, o foco vai para o CVV, que fica vazio, e o botão de pagar mostra o valor.
4. **Given** dados com formato inválido, **When** a PF tenta pagar, **Then** vê o erro em cada campo e
   nada é enviado.
5. **Given** dados corretos e limite, **When** a PF paga, **Then** vê o comprovante com lojista, valor,
   data, bandeira, final do cartão e código de autorização.
6. **Given** dados que não são do seu cartão, **When** paga, **Then** vê "Cartão não autorizado" com a
   orientação de conferir os dados, sem dizer qual está errado, e o CVV é limpo.
7. **Given** uma fatura vencida, **When** paga, **Then** vê "Cartão bloqueado por fatura vencida" com
   link para as faturas.
8. **Given** valor acima do limite disponível, **When** paga, **Then** vê "Limite insuficiente" com o
   limite disponível atual.
9. **Given** uma falha de conexão ou um duplo clique, **When** a PF tenta de novo com os mesmos dados,
   **Then** há um único pagamento e ela vê o comprovante.
10. **Given** uma cobrança já paga por ela, **When** a PF abre o link, **Then** vê o comprovante; se paga
    por outra pessoa ou cancelada, **Then** vê que não está mais disponível, sem formulário.
11. **Given** uma PF sem cartão, **When** abre o link, **Then** vê a cobrança e a ação "Solicitar cartão".
12. **Given** uma PJ que não é a emissora, **When** abre o link, **Then** vê os dados da cobrança e que só
    contas PF podem pagar; a emissora vê o detalhe da US2.
13. **Given** um código inválido ou inexistente, **When** alguém abre o endereço, **Then** vê "Cobrança não
    encontrada".
14. **Given** o código dito ou escrito em minúsculas, sem hífen ou com espaços, **When** a PF o informa na
    tela do cartão ou no endereço, **Then** abre a mesma cobrança.

---

### User Story 4 - Consultar faturas (Priority: P2)

A PF vê a fatura atual e as anteriores, por mês, com cada compra, o nome do estabelecimento e o valor.

**Why this priority**: dá visibilidade ao que foi comprado e prepara o pagamento.

**Independent Test**: com uma PF com faturas em várias situações, navegar entre os meses e conferir
total, datas, situação e lançamentos.

**Acceptance Scenarios**:

1. **Given** uma PF com cartão, **When** abre as faturas, **Then** vê a lista por mês, da mais nova para a
   mais antiga, com situação, total e vencimento, incluindo a do ciclo atual.
2. **Given** uma fatura, **When** a abre, **Then** vê período, fechamento, vencimento, situação, total e os
   lançamentos com estabelecimento, descrição, data e valor.
3. **Given** a fatura aberta sem lançamentos, **When** a abre, **Then** vê total R$ 0,00 e que as compras
   aparecem no próximo dia útil.
4. **Given** uma fatura fechada, **When** a abre, **Then** vê que ela será liberada para pagamento após o
   processamento, sem ação de pagar.
5. **Given** uma fatura vencida, **When** a abre, **Then** vê dias de atraso, juros e valor a pagar hoje,
   separados do total das compras, e o aviso de que o cartão está bloqueado.
6. **Given** um mês sem fatura, **When** abre o endereço, **Then** vê "Fatura não encontrada".
7. **Given** uma fatura disponível, **When** a PF abre o início, **Then** vê o aviso com valor, vencimento e
   link para a fatura; se vencida, o aviso traz o valor do dia e que o cartão está bloqueado.
8. **Given** nenhuma fatura a pagar ou uma PF sem cartão, **When** abre o início, **Then** não há aviso.

---

### User Story 5 - Pagar a fatura (Priority: P2)

A PF paga o total da fatura com o saldo da carteira.

**Why this priority**: libera o limite e encerra o ciclo.

**Independent Test**: com fatura disponível e saldo, pagar e conferir situação, saldo, extrato e limite.

**Acceptance Scenarios**:

1. **Given** fatura disponível ou vencida, **When** a PF escolhe "Pagar fatura", **Then** vê a confirmação
   com o valor a pagar (com os juros, se houver), o saldo atual e o saldo depois.
2. **Given** a confirmação, **When** confirma, **Then** a fatura fica "Paga", com data e valor pago, e o
   limite disponível aumenta no valor das compras.
3. **Given** saldo menor que o valor, **When** abre a confirmação, **Then** vê "Saldo insuficiente" com o
   saldo e o botão de pagar indisponível; se o saldo mudar depois, a recusa do Bank é mostrada igual.
4. **Given** falha de conexão ou duplo clique, **When** tenta de novo, **Then** há um único débito.
5. **Given** a fatura paga em outra aba, **When** tenta pagar, **Then** vê a situação atualizada.

---

### User Story 6 - Receber e antecipar (Priority: P2)

A PJ vê os seus recebíveis, com data prevista e situação, e antecipa um item sabendo quanto vai receber.

**Why this priority**: mostra quando o dinheiro entra e permite a antecipação.

**Independent Test**: com uma cobrança paga de R$ 299,90, ver o recebível, antecipar e conferir taxa de
R$ 6,00, líquido de R$ 293,90 e o novo saldo.

**Acceptance Scenarios**:

1. **Given** uma PJ, **When** abre a aba "Recebíveis", **Then** vê os itens com descrição, valor, data
   prevista e situação, com filtro por situação, em páginas (desktop) ou "Carregar mais" (mobile).
2. **Given** um recebível pendente, **When** escolhe "Antecipar", **Then** vê valor, taxa de 2%, líquido,
   a data em que receberia o valor integral e o aviso de que a antecipação não pode ser desfeita.
3. **Given** a confirmação, **When** confirma, **Then** o item fica "Antecipado" com taxa, líquido e data.
4. **Given** um recebível liquidado, antecipado ou previsto para hoje, **When** o vê, **Then** não há a
   ação de antecipar; se a tela estava desatualizada, a recusa é explicada e a situação atualizada.
5. **Given** falha de conexão ou duplo clique, **When** tenta de novo, **Then** há um único crédito.

---

### User Story 7 - Navegar e usar em qualquer tela (Priority: P2)

As telas novas entram no menu de cada perfil e funcionam em desktop e celular, por teclado e leitor de
tela.

**Why this priority**: exigência da constituição para toda tela; transversal às demais.

**Independent Test**: percorrer todas as telas em 320, 390, 768, 1024, 1440 e 1920 px, por teclado e com a
verificação automática de acessibilidade.

**Acceptance Scenarios**:

1. **Given** uma PF, **When** usa o menu, **Then** vê "Cartão"; **Given** uma PJ, **Then** "Cobranças" está
   ativo, sem "Em breve".
2. **Given** qualquer tela nova, **When** exibida de 320 a 1920 px, **Then** não há rolagem horizontal nem
   conteúdo cortado, e o layout corresponde ao protótipo aprovado.
3. **Given** os fluxos de solicitar, pagar, antecipar e cancelar, **When** feitos só pelo teclado, **Then**
   são concluídos com foco visível e ordem lógica, e erros e conclusões são anunciados.
4. **Given** o cartão com dados exibidos, **When** lido por leitor de tela, **Then** número e CVV são lidos
   dígito a dígito, com rótulo, e a mudança entre oculto e exibido é anunciada.
5. **Given** o perfil, **When** a pessoa o abre, **Then** as permissões listadas incluem as novas áreas.

### Edge Cases

- **Voltar à cobrança depois de recarregar**: a chave do pagamento é mantida na aba, mas os dados do cartão
  precisam ser informados de novo.
- **Recusa**: o número e a validade digitados são mantidos na tela; o CVV é sempre limpo.
- **Cobrança paga ou cancelada enquanto a PF preenchia**: o pagamento é recusado e a tela mostra a situação.
- **Dados alterados após uma tentativa sem resposta**: se o Bank responder que a chave foi usada com outros
  dados, a tela recarrega a cobrança: se já paga pela pessoa, mostra o comprovante; senão, pede para conferir e
  o próximo envio usa uma nova chave.
- **Falha de conexão ao emitir cobrança**: a emissão não tem chave de idempotência; a tela mantém o que foi
  digitado e orienta a conferir a lista, com link, antes de enviar de novo.
- **Voltar pelo navegador à tela de pagamento**: os dados do cartão digitados não reaparecem.
- **Virada do dia com a confirmação da fatura aberta**: o valor cobrado é o do Bank no momento; a tela
  mostra o valor pago de fato.
- **Sessão expirada**: vai ao login e volta à tela; os dados do cartão digitados são descartados.
- **Erro ao exibir os dados**: aparece a mensagem com o código de suporte e o cartão continua oculto.
- **Falha na atualização automática do detalhe**: a tela mantém o que já mostrava e tenta de novo na próxima
  vez, sem exibir erro; com a confirmação de cancelamento aberta, o pagamento recebido a substitui.
- **Copiar ou compartilhar indisponível no navegador**: o link fica selecionável para cópia manual.
- **Valor máximo e descrição de 200 caracteres**: quebram linha sem estourar o layout.
- **Código com minúsculas, espaços, sem hífen ou endereço com barra no fim**: abre normalmente.
- **Caracteres parecidos digitados por engano** (O por 0, I ou L por 1): o código não usa esses caracteres;
  o que não existe é tratado como não encontrado.
- **Colar o link ou o código da cobrança na tela do cartão**: abre a cobrança; texto inválido mostra erro no
  campo.
- **Tentar adivinhar códigos**: exige estar autenticado, e o código é aleatório; quem acerta vê só o que já
  veria pelo link.
- **Nome impresso com até 150 caracteres**: é reduzido visualmente no cartão, e o texto completo continua
  disponível para leitores de tela.
- **Situação ou motivo desconhecido vindo da API**: mostra o texto genérico, sem quebrar a tela.

## Requirements *(mandatory)*

### Functional Requirements

**Navegação e acesso**

- **FR-001**: O menu da PF MUST ganhar "Cartão" e o da PJ MUST ativar "Cobranças", em desktop e mobile, com
  4 itens na barra do celular.
- **FR-002**: Cartão e faturas MUST ser acessíveis só à PF; lista e emissão de cobranças e recebíveis, só à
  PJ. Outro perfil vê "Área indisponível para o seu tipo de conta", com link para o início.
- **FR-003**: O endereço de uma cobrança MUST usar o código curto e ser o mesmo para todos: a emissora vê o
  detalhe; a PF, o pagamento ou o comprovante; outra PJ, os dados sem ação. Quem não está autenticado volta
  à cobrança depois de entrar.
- **FR-004**: As permissões do perfil MUST citar as novas áreas.

**Cartão**

- **FR-005**: A solicitação MUST exigir a escolha da bandeira e avisar, antes de confirmar, que ela é
  definitiva e que há um cartão por CPF.
- **FR-006**: A tela do cartão MUST mostrar bandeira, nome impresso, 4 últimos dígitos, validade, limite
  total, disponível e usado, compras ainda não lançadas e a fatura do ciclo atual (mês, situação, total,
  fechamento e vencimento), com links para ela e para a lista. Se a fatura não carregar, só essa parte mostra
  o erro.
- **FR-007**: O ícone de olho no cartão MUST indicar o estado (aberto = visíveis, riscado = ocultos), ter
  nome acessível com a ação ("Exibir dados do cartão" / "Ocultar dados do cartão") e, ao exibir, buscar os dados
  no momento do pedido e mostrá-los no cartão desenhado na tela, sem confirmar a senha. Eles MUST sumir ao
  ocultar, sair da tela, esconder a aba ou após 1 minuto.
- **FR-008**: Número completo e CVV MUST NOT ser guardados em armazenamento do navegador, endereço,
  histórico, título, logs ou telemetria, nem sobreviver a recarregar a página. O CVV nunca é copiado nem
  preenchido pelo sistema.
- **FR-009**: A PF MUST poder abrir uma cobrança informando o link ou o código na tela do cartão, com
  maiúsculas ou minúsculas, com ou sem hífen e espaços.

**Cobranças (PJ)**

- **FR-009a**: Toda cobrança MUST ter um código único e imutável de 8 caracteres, aleatório, formado por
  letras maiúsculas e números sem os que se confundem (0, 1, I, L, O e U), exibido como `XXXX-XXXX`. As
  cobranças existentes MUST receber o seu na atualização, sem mudar nenhum outro dado.
- **FR-009b**: A API MUST devolver o código em toda consulta de cobrança e permitir consultar uma cobrança
  pelo código, com as mesmas regras de visibilidade da consulta pelo identificador. Os campos e endereços
  atuais MUST continuar iguais. Código malformado ou inexistente MUST receber a mesma resposta de cobrança
  não encontrada.
- **FR-010**: A PJ MUST emitir cobrança com valor e descrição, com as validações da API refletidas nos
  campos, contador de 200 caracteres e valor no formato brasileiro.
- **FR-011**: A lista MUST ter filtro por situação, refletido no endereço, e paginação.
- **FR-012**: O detalhe MUST mostrar situação, valor, descrição, datas, pagamento (bandeira, final, código)
  e última recusa. Só enquanto pendente, MUST mostrar o código e oferecer copiar o link, compartilhar (texto
  com lojista, valor, descrição e link, só onde o aparelho permite) e cancelar, com confirmação; e, com a aba
  visível, MUST se atualizar a cada 5 segundos e anunciar a mudança.

**Pagamento (PF)**

- **FR-013**: O pagamento MUST mostrar lojista, valor e descrição antes do formulário, e o botão MUST trazer
  o valor.
- **FR-014**: "Usar meu cartão" MUST preencher nome, número e validade e levar o foco ao CVV. Os dados
  buscados MUST seguir o FR-008 e o número preenchido MUST ser exibido.
- **FR-015**: Antes de enviar, a tela MUST validar titular, número (16 dígitos e dígito verificador),
  validade (formato e não vencida) e CVV (3 dígitos).
- **FR-016**: Cada tentativa MUST usar uma chave de idempotência, reaproveitada ao repetir após falha de
  conexão com os mesmos dados e mantida na aba ao recarregar; mudar qualquer dado do cartão ou receber uma
  recusa não gera segundo pagamento.
- **FR-017**: Cada recusa MUST ter título, orientação e próxima ação próprios (conferir dados, ir às faturas,
  ver limite).
- **FR-018**: O comprovante MUST poder ser reaberto pelo pagador no endereço da cobrança.

**Faturas (PF)**

- **FR-019**: A PF MUST ver a lista de faturas por mês e o detalhe com período, fechamento, vencimento,
  situação, total, lançamentos e, se vencida, dias de atraso, juros e valor a pagar.
- **FR-019a**: O início da PF MUST avisar da fatura disponível ou vencida, com link. Com mais de uma, mostra a
  mais antiga. Sem cartão, sem fatura a pagar ou se a consulta falhar, o início fica como hoje.
- **FR-020**: O pagamento MUST pedir confirmação com valor, saldo e saldo depois, usar chave de idempotência
  e atualizar fatura e limite ao concluir.

**Recebíveis (PJ)**

- **FR-021**: A PJ MUST ver os recebíveis com valor, taxa, líquido, data prevista e situação, com filtro
  refletido no endereço e paginação.
- **FR-022**: Antecipar MUST pedir confirmação com valor, taxa de 2%, líquido e data prevista, usar chave de
  idempotência e mostrar o resultado devolvido pelo Bank.

**Geral**

- **FR-023**: Toda tela MUST ter estados de carregamento, vazio, erro com código de suporte e "Tentar de
  novo", sem perder o que foi digitado (exceto o CVV).
- **FR-024**: Valores MUST ser calculados em centavos inteiros e datas de regra (fechamento, vencimento,
  data da compra, data prevista) exibidas como dia, sem conversão de fuso.
- **FR-025**: As telas MUST corresponder ao protótipo aprovado e atender a WCAG 2.2 AA, de 320 a 1920 px.
- **FR-026**: Fora o código da cobrança (FR-009a e FR-009b), nenhuma mudança na API; fora o menu, o
  aviso no início (FR-019a) e as permissões do perfil, nenhuma mudança nas telas existentes.

### Key Entities

- **Cartão (visão)**: bandeira, nome impresso, final, validade, limites e compras a lançar.
- **Dados do cartão (temporários)**: número e CVV, só em memória e só enquanto exibidos ou digitados.
- **Código da cobrança**: 8 caracteres, único, imutável e aleatório; forma o link curto.
- **Cobrança (visão)**: código, lojista, valor, descrição, situação, datas, pagamento e última recusa.
- **Tentativa de operação**: chave de idempotência de um pagamento de cobrança, de fatura ou de uma
  antecipação, guardada na aba sem dados do cartão.
- **Fatura (visão)**: mês, datas, situação, total, juros, valor a pagar e lançamentos.
- **Recebível (visão)**: descrição, valores, data prevista e situação.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma PF sem cartão o solicita e vê os dados em até 1 minuto.
- **SC-002**: Uma PF com cartão paga uma cobrança, do link ao comprovante, em até 1 minuto.
- **SC-003**: Uma PJ emite uma cobrança e copia o link em até 1 minuto.
- **SC-003a**: O link da cobrança tem no máximo 12 caracteres depois do endereço do site, e 100% das
  cobranças, novas e antigas, têm código único.
- **SC-004**: Duplo clique, recarga ou falha de conexão nunca geram pagamento, débito ou crédito em dobro.
- **SC-005**: Nenhuma ocorrência de número completo ou CVV no armazenamento do navegador, no endereço, no
  histórico e na telemetria, após percorrer todos os fluxos.
- **SC-006**: 100% das recusas do contrato têm mensagem e próxima ação próprias.
- **SC-007**: Nenhuma violação A/AA na verificação automática, em todas as telas e larguras.
- **SC-008**: Todos os testes automatizados anteriores continuam passando; só mudam as verificações do que esta
  spec altera ("Cobranças" como "Em breve" e a lista exata de campos da cobrança).

## Assumptions

- **Protótipo**: aprovado pelo responsável em 2026-11-24
  (<https://claude.ai/artifact/9mhoKhZ8E9guBLsVE53oEY>, privado); as fontes do protótipo e as
  capturas das telas prontas entram em `docs/design`.
- **Bandeiras**: nomes em texto ("Visa", "Mastercard"), sem os logotipos oficiais.
- **Link da cobrança**: gerado pelo próprio One Bank, sem encurtador externo, para não expor os links a
  terceiros; não há envio por e-mail, QR Code nem prazo.
- **Contrato**: o da spec 014 passa a 1.1.0, aditivo.
- **Protótipo da solicitação**: mostra Visa já marcada e o limite fixo de R$ 5.000,00; vale esta spec: nenhuma
  bandeira marcada e nenhum limite antes da emissão, porque ele é configurável.
- **Início**: só ganha o aviso de fatura da PF; o acesso às telas novas é pelo menu.
- **Notificações e recibo em PDF**: fora do escopo.
- **Perfil da empresa**: a PJ não vê quem pagou, como define a spec 014.
- **Taxa de 2%**: o valor mostrado antes de confirmar é calculado na tela com a regra da spec 014; vale o
  devolvido pela API.
- **Dependências**: API da spec 014 na `main`; sessão (spec 004); extrato e design system (specs 007 e 013).
