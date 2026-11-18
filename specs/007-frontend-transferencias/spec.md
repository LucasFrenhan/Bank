# Feature Specification: Frontend de transferências, comprovante e extrato

**Feature Branch**: `007-frontend-transferencias`

**Created**: 2026-11-18

**Status**: Draft

**Input**: Issue #32 — "Frontend: telas de transferência, comprovante e extrato". Implementar no
frontend as telas Transferir, Confirmação, Comprovante e Extrato do
[protótipo](../../docs/design/prototipo.md), consumindo o contrato da spec
[006-transferencias](../006-transferencias/spec.md) (decisão FR-022 da spec 006: backend
primeiro, telas numa spec de frontend separada). Inclui a ativação de "Transferir" e "Ver
extrato" no início PF, "Últimos recebimentos" reais no início PJ, as mensagens de erro do
protótipo, desktop e mobile, WCAG 2.2 AA e testes de componentes e ponta a ponta contra a API
real.

## Clarifications

### Session 2026-11-18

- Q: O comprovante precisa ser reaberto depois (a partir de um lançamento do extrato ou em outra
  sessão), já que o contrato da spec 006 não tem consulta de transferência por identificador?
  → A: Sim, por uma nova consulta `GET /api/v1/transferencias/{id}` no backend, entregue
  pela issue #36 (task separada). Esta spec consome esse endpoint; a reabertura do comprovante
  só é concluída após o merge da #36.

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes do Bank já autenticados (spec 004). **Pessoa Física** transfere para
outras pessoas ou empresas e consulta o extrato; **Pessoa Jurídica** só recebe e consulta o
extrato (RN-06 da spec 003). Telas, textos, cores e tipografia seguem o protótipo; regras e dados
vêm do contrato da spec 006, que é a fonte da verdade.

### User Story 1 - Transferir dinheiro em três etapas (Priority: P1)

Uma Pessoa Física abre "Transferir", informa o CPF ou CNPJ de quem vai receber e vê o nome e o
tipo de conta do titular para conferir. Informa o valor e, se quiser, uma descrição; na etapa de
confirmação revê tudo, inclusive o saldo que vai sobrar, e envia. Ao final vê o comprovante.

**Why this priority**: é a ação principal da carteira PF e o motivo da spec.

**Independent Test**: com uma PF com saldo e uma PJ (CNPJ alfanumérico) cadastradas, em desktop e
em mobile, percorrer Dados → Confirmar → Comprovante, conferir o comprovante e o novo saldo no
início e no extrato das duas contas.

**Acceptance Scenarios**:

1. **Given** uma PF autenticada no início, **When** escolhe "Transferir" (no cartão de saldo ou no
   menu), **Then** abre a etapa "Dados" com o título "Transferir dinheiro" e o indicador de
   etapas Dados → Confirmar → Comprovante.
2. **Given** a etapa Dados, **When** a pessoa digita um documento, **Then** o campo aplica a
   máscara de CPF ou de CNPJ conforme o que foi digitado (CNPJ alfanumérico em maiúsculas) e,
   quando o documento fica completo, o Bank busca o titular e mostra o cartão com iniciais, nome,
   tipo de conta e documento mascarado, marcado como confirmado.
3. **Given** o destinatário confirmado, **When** a pessoa informa o valor, **Then** vê abaixo do
   campo o saldo disponível e o mínimo de R$ 0,01; a descrição opcional mostra o contador
   "N/200".
4. **Given** destinatário confirmado e valor válido, **When** escolhe "Continuar", **Then** vai à
   etapa "Confira antes de enviar" com o valor em destaque e as linhas Para, Documento, Tipo de
   conta, Descrição (quando houver) e Saldo depois (saldo atual menos o valor), além do aviso de
   que transferências não podem ser desfeitas e de que tentar de novo não cobra duas vezes.
5. **Given** a etapa de confirmação, **When** escolhe "Voltar" ou "Editar dados", **Then** volta
   à etapa Dados com tudo o que foi preenchido.
6. **Given** a etapa de confirmação, **When** escolhe "Transferir R$ {valor}", **Then** a
   transferência é enviada uma única vez, o botão mostra progresso e, ao concluir, abre o
   comprovante "Transferência concluída" com "R$ {valor} enviados para {nome}".
7. **Given** o comprovante, **When** a pessoa volta ao início, **Then** o saldo exibido já
   reflete a transferência e ela aparece em "Últimas movimentações".
8. **Given** uma conta PJ, **When** abre diretamente o endereço da tela de transferência,
   **Then** vê "Indisponível para Pessoa Jurídica — Contas PJ recebem transferências e geram
   cobranças, mas não enviam", sem formulário, com link para o início.

---

### User Story 2 - Entender e corrigir uma transferência recusada (Priority: P1)

Quando o Bank recusa a consulta do destinatário ou a transferência, a pessoa vê a mensagem do
protótipo no lugar certo, entende o motivo e consegue corrigir sem redigitar tudo.

**Why this priority**: as recusas são parte do fluxo normal (saldo, documento errado) e a issue
exige a tabela de mensagens do protótipo.

**Independent Test**: provocar, em desktop e mobile, cada recusa do contrato da spec 006 e
conferir a mensagem, o local em que aparece e que os dados digitados foram preservados.

**Acceptance Scenarios**:

1. **Given** um documento válido sem conta no Bank, **When** o titular é buscado, **Then** o campo
   mostra o erro "Nenhuma conta Bank com esse documento." e "Continuar" fica indisponível.
2. **Given** o próprio documento da pessoa, **When** o titular é buscado (ou a transferência é
   enviada), **Then** aparece o aviso "Esse documento é o seu — Não é possível transferir para a
   própria carteira." e não é possível continuar.
3. **Given** um documento com formato ou dígito verificador inválido, **When** é informado,
   **Then** o erro aparece abaixo do campo, sem busca de titular concluída.
4. **Given** um valor maior que o saldo, **When** a pessoa tenta continuar ou o Bank recusa por
   saldo insuficiente, **Then** aparece o aviso "Saldo insuficiente — Você tem R$ {saldo}
   disponíveis. Ajuste o valor para continuar.", com o saldo atualizado da carteira.
5. **Given** valor zero, com mais de 2 casas decimais ou descrição acima de 200 caracteres,
   **When** a pessoa tenta continuar, **Then** o erro aparece no campo correspondente; erros por
   campo devolvidos pelo Bank também aparecem no campo.
6. **Given** a mesma chave de envio reutilizada com dados diferentes, **When** o Bank recusa,
   **Then** aparece "Essa transferência mudou — Os dados não batem com o envio anterior. Revise e
   envie de novo." e o próximo envio usa uma nova chave.
7. **Given** uma conta PJ (ou permissão ausente) recusada pelo Bank, **When** tenta transferir,
   **Then** vê "Indisponível para Pessoa Jurídica".
8. **Given** um erro inesperado ou falha de rede ao enviar, **When** a resposta não chega ou é
   inesperada, **Then** a pessoa vê "Algo deu errado" com o código de suporte (`traceId`) quando
   houver, ou a mensagem de falta de conexão, e pode tentar de novo sem perder os dados.

---

### User Story 3 - Reenviar sem pagar duas vezes (Priority: P1)

Se a conexão cair, a página for recarregada na confirmação ou a pessoa clicar de novo, o reenvio
da mesma transferência não cobra outra vez: o Bank devolve a transferência original e a tela
avisa "Essa transferência já foi feita", com acesso ao comprovante.

**Why this priority**: integridade financeira (Princípio IV da constituição); o cliente é quem
gera e reaproveita a chave de idempotência.

**Independent Test**: enviar uma transferência, simular perda da resposta (ou recarregar a
confirmação) e reenviar; conferir um único débito no extrato e a mensagem de reenvio; duplo
clique em "Transferir" gera um único envio.

**Acceptance Scenarios**:

1. **Given** a etapa de confirmação, **When** a pessoa clica duas vezes em "Transferir", **Then**
   apenas um envio é feito.
2. **Given** um envio cuja resposta se perdeu (falha de rede), **When** a pessoa tenta de novo
   com os mesmos dados, **Then** o reenvio usa a mesma chave e, se a transferência já tinha sido
   feita, a tela mostra "Essa transferência já foi feita — Nada foi cobrado de novo." com o link
   "Ver comprovante".
3. **Given** uma transferência concluída, **When** a pessoa volta à confirmação (botão voltar do
   navegador ou recarregar) e envia de novo, **Then** vê "Essa transferência já foi feita" com o
   link para o comprovante, e o saldo não muda.
4. **Given** a pessoa voltou à etapa Dados e alterou destinatário, valor ou descrição, **When**
   envia, **Then** é uma nova transferência, com nova chave.
5. **Given** uma transferência concluída, **When** a pessoa inicia outra transferência pelo
   início ou pelo menu, **Then** o formulário começa vazio e usa uma nova chave.

---

### User Story 4 - Consultar o extrato (Priority: P2)

PF e PJ abrem o extrato e veem os lançamentos da carteira, do mais recente para o mais antigo,
com contraparte, descrição, situação e valor (entrada com "+" e ícone de seta para baixo, saída
com "−" e seta para cima), filtram por Todas / Entradas / Saídas e navegam entre páginas.

**Why this priority**: dá visibilidade ao dinheiro movimentado; depende das transferências para
ter conteúdo, mas já mostra o saldo inicial de demonstração.

**Independent Test**: com uma PF recém-criada, ver a entrada "Saldo inicial de demonstração" de
R$ 10,00; após transferências, ver entradas e saídas nas duas contas, usar os filtros e a
paginação em desktop e mobile.

**Acceptance Scenarios**:

1. **Given** uma PF ou PJ autenticada, **When** abre o extrato, **Then** vê o título "Extrato", o
   saldo atual e os lançamentos mais recentes primeiro, cada um com data/hora, contraparte
   (nome e "Entrada"/"Saída"), descrição ("Sem descrição" quando vazia), situação "Concluída" e
   valor com sinal.
2. **Given** uma PF recém-cadastrada, **When** abre o extrato, **Then** vê uma entrada "Saldo
   inicial" com a descrição "Saldo inicial de demonstração" e + R$ 10,00, sem contraparte.
3. **Given** o extrato, **When** escolhe "Entradas" ou "Saídas", **Then** a lista mostra só
   aquela direção, volta à primeira página e a contagem de páginas reflete o filtro.
4. **Given** mais de 20 lançamentos no desktop, **When** a pessoa usa "Próxima" e "Anterior",
   **Then** navega entre páginas de 20 itens e vê "Página X de Y · 20 por página"; "Anterior"
   fica indisponível na primeira página e "Próxima" na última.
5. **Given** mais de 20 lançamentos no mobile, **When** a pessoa escolhe "Carregar mais",
   **Then** os próximos itens são acrescentados ao final da lista, agrupados por dia ("Hoje",
   "Ontem", "15 de novembro"); o botão some quando não há mais itens.
6. **Given** uma carteira sem lançamentos no filtro escolhido, **When** o extrato é exibido,
   **Then** aparece "Nenhuma movimentação ainda — Quando você enviar ou receber uma
   transferência, ela aparece aqui."
7. **Given** o extrato carregando ou com erro, **When** exibido, **Then** mostra o estado de
   carregamento do protótipo ou "Algo deu errado" com o `traceId` e a opção de tentar de novo.
8. **Given** uma contraparte PF, **When** aparece no extrato, **Then** o CPF é exibido mascarado
   (`•••.982.247-••`), como entregue pelo Bank.
9. **Given** um lançamento de transferência (enviada ou recebida), **When** a pessoa o escolhe
   no extrato ou nas últimas movimentações, **Then** abre o comprovante daquela transferência,
   mesmo em outra sessão ou aba (issue #36); o lançamento "Saldo inicial" não abre comprovante.

---

### User Story 5 - Ver movimentações reais na tela inicial (Priority: P2)

O início PF passa a oferecer "Transferir" e "Ver extrato" e mostra as últimas movimentações
reais; o início PJ mostra os últimos recebimentos reais. Ambos levam ao extrato completo por
"Ver tudo".

**Why this priority**: conecta as telas novas à navegação já entregue pela spec 004.

**Independent Test**: após transferências entre uma PF e uma PJ, abrir o início das duas contas e
conferir as listas, os atalhos e a navegação por perfil em desktop e mobile.

**Acceptance Scenarios**:

1. **Given** uma PF no início, **When** a tela carrega, **Then** "Transferir" e "Ver extrato" (no
   cartão de saldo e na navegação) estão ativos, sem o selo "Em breve".
2. **Given** uma PF com lançamentos, **When** abre o início, **Then** "Últimas movimentações"
   mostra os 5 lançamentos mais recentes com nome da contraparte (ou "Saldo inicial"),
   "Transferência recebida"/"Transferência enviada" e data relativa ("Hoje, 14:32", "Ontem,
   19:47", "15 nov, 11:05"), e valor com sinal; "Ver tudo" abre o extrato.
3. **Given** uma PJ com recebimentos, **When** abre o início, **Then** "Últimos recebimentos"
   mostra as 5 entradas mais recentes com nome do pagador, descrição ("Sem descrição" quando
   vazia), data relativa e valor; "Ver tudo" abre o extrato filtrado em Entradas.
4. **Given** uma PJ, **When** usa a navegação, **Then** "Recebimentos" abre o extrato filtrado
   em Entradas; "Cobranças" continua "Em breve".
5. **Given** nenhuma movimentação, **When** o início é exibido, **Then** a lista mostra o estado
   vazio "Nenhuma movimentação ainda".

---

### User Story 6 - Mesma experiência no computador e no celular (Priority: P2)

Todas as telas desta spec seguem o layout desktop e mobile do protótipo e atendem WCAG 2.2 AA.

**Why this priority**: exigência da constituição para toda tela; transversal às demais histórias.

**Independent Test**: percorrer transferência, comprovante, extrato e início nas larguras de
referência e intermediárias, só com teclado e com leitor de tela, e rodar a verificação
automática de acessibilidade.

**Acceptance Scenarios**:

1. **Given** qualquer tela desta spec, **When** exibida em 390 × 844 e em 1440 px, **Then**
   corresponde ao layout mobile e desktop do protótipo (no desktop, o extrato em tabela; no
   mobile, em lista agrupada por dia).
2. **Given** qualquer largura entre 320 e 1920 px, **When** a tela é exibida, **Then** não há
   rolagem horizontal nem conteúdo cortado.
3. **Given** o fluxo de transferência, **When** feito só pelo teclado, **Then** todas as etapas
   são concluídas com foco visível e ordem lógica; a etapa atual, o titular encontrado, os
   erros e a conclusão são anunciados por leitores de tela.
4. **Given** o celular, **When** a pessoa digita valor ou documento, **Then** aparece o teclado
   adequado (numérico para valor e CPF, alfanumérico quando o CNPJ tem letras).

---

### Edge Cases

- Documento colado com máscara, espaços ou minúsculas: normalizado para a máscara do tipo, como
  no cadastro (spec 004).
- Documento alterado depois de o titular ser confirmado: a confirmação é descartada e o titular é
  buscado de novo; respostas atrasadas de buscas anteriores são ignoradas.
- Valor digitado com vírgula ou ponto, ou colado como "1.234,56": interpretado no formato
  brasileiro, sem arredondar; mais de 2 casas decimais é erro de campo.
- Valor que deixa o saldo exatamente em zero: permitido; "Saldo depois" mostra R$ 0,00.
- Saldo mudou entre a etapa Dados e o envio (por exemplo, recebeu ou enviou em outra aba): o
  Bank decide; se recusar por saldo, o aviso mostra o saldo atualizado.
- Descrição só com espaços: tratada como ausente; não aparece na confirmação nem no comprovante.
- Recarregar a página na etapa de confirmação: os dados e a chave de envio são mantidos na aba;
  se não houver dados, a pessoa volta à etapa Dados.
- Abrir o endereço de um comprovante inexistente ou de outra pessoa: "Comprovante não
  encontrado", com link para o extrato, sem revelar se a transferência existe.
- Sessão expirada durante o fluxo: vai ao login (spec 004) e, ao voltar, os dados da transferência
  em andamento foram descartados junto com a sessão; nenhum envio é refeito automaticamente.
- Extrato numa página além da última (por exemplo, lançamentos mudaram entre páginas): mostra a
  última página válida.
- Nome muito longo de contraparte: quebra de linha sem cortar a tela.
- Novas entradas chegando enquanto a pessoa pagina no mobile: itens já exibidos não são
  duplicados na lista.

## Requirements *(mandatory)*

### Functional Requirements

**Transferir — etapa Dados**

- **FR-001**: Apenas contas Pessoa Física MUST ver e usar a transferência; para Pessoa Jurídica a
  ação não aparece (RN-06) e o acesso direto ao endereço mostra "Indisponível para Pessoa
  Jurídica", sem formulário.
- **FR-002**: O campo "CPF ou CNPJ de quem vai receber" MUST aceitar CPF e CNPJ (numérico ou
  alfanumérico), aplicando a máscara do tipo identificado enquanto a pessoa digita, com CNPJ em
  maiúsculas.
- **FR-003**: Quando o documento estiver completo e com formato válido, o frontend MUST consultar
  o titular no Bank e exibir nome, iniciais, tipo de conta e documento mascarado; o envio só é
  permitido com o titular confirmado para o documento atual. Enquanto a busca acontece, o campo
  mostra "Buscando titular…" (anunciado a leitores de tela). Ao sair do campo com um documento
  completo, a busca acontece mesmo que o formato pareça inválido, para que o erro venha do Bank e
  apareça no campo. "Continuar" com documento incompleto mostra no campo "Informe o CPF ou CNPJ
  completo"; com a busca em andamento, aguarda o resultado.
- **FR-004**: O campo "Valor" MUST aceitar valores em reais no formato brasileiro, com mínimo
  R$ 0,01 e no máximo 2 casas decimais, mostrando o saldo disponível e o mínimo como texto de
  ajuda; valor acima do saldo conhecido MUST mostrar o aviso de saldo insuficiente antes do
  envio.
- **FR-005**: O campo "Descrição (opcional)" MUST limitar a 200 caracteres com contador visível.
- **FR-006**: Validação local MUST cobrir apenas formato e obrigatoriedade; erros por campo
  devolvidos pelo Bank MUST aparecer no campo correspondente, e os dados digitados MUST ser
  preservados em qualquer recusa.

**Transferir — Confirmação e envio**

- **FR-007**: A confirmação MUST mostrar valor, destinatário (nome, documento mascarado e tipo de
  conta), descrição quando houver e "Saldo depois", com o aviso de que transferências são
  concluídas na hora, não podem ser desfeitas e não são cobradas duas vezes ao tentar de novo.
- **FR-008**: Cada transferência MUST ser enviada com uma chave de idempotência gerada no
  navegador quando a pessoa chega à confirmação com um conjunto de dados; a mesma chave MUST ser
  reaproveitada em qualquer reenvio desses mesmos dados (novo clique, nova tentativa após falha,
  recarga da página) e MUST ser trocada quando destinatário, valor ou descrição mudarem ou uma
  nova transferência começar.
- **FR-009**: O botão de envio MUST impedir envios simultâneos (duplo clique) e indicar progresso.
- **FR-010**: Os dados da transferência em andamento e a chave MUST persistir apenas na aba e
  enquanto durar a sessão (mesmo escopo da sessão da spec 004), sendo descartados ao sair, ao
  expirar a sessão ou ao concluir e iniciar outra transferência.

**Comprovante**

- **FR-011**: Após uma transferência concluída, o frontend MUST exibir o comprovante com valor,
  pagador (nome e CPF mascarado, como devolvido pelo Bank para pagador e recebedor — spec 008,
  FR-002a), recebedor (nome e documento mascarado), descrição, data/hora no
  fuso do navegador, situação "Concluída" e identificador da transferência em fonte monoespaçada,
  além de "Voltar ao início".
- **FR-012**: Um reenvio que o Bank responde como já realizado MUST mostrar "Essa transferência já
  foi feita — Nada foi cobrado de novo." com o link "Ver comprovante", que abre o comprovante da
  transferência original.
- **FR-013**: A ação "Compartilhar" do comprovante MUST oferecer o compartilhamento nativo do
  dispositivo quando disponível e, caso contrário, copiar um resumo em texto do comprovante,
  confirmando a cópia na tela; o resumo MUST usar os documentos mascarados.
- **FR-014**: O comprovante MUST ter endereço próprio, identificado pela transferência, e poder
  ser reaberto a qualquer momento por pagador e recebedor — a partir de um lançamento de
  transferência do extrato ou do início, após recarregar a página ou em outra sessão —,
  carregando os dados pela consulta de transferência por identificador (spec 008, issue #36) quando
  o comprovante não está guardado na aba. Transferência inexistente ou de que a pessoa não
  participa mostra "Comprovante não encontrado" com link para o extrato. Para quem recebe, o
  comprovante é o mesmo, com o título "Transferência recebida".

**Extrato**

- **FR-015**: PF e PJ MUST poder abrir o extrato, que mostra o saldo atual e os lançamentos da
  carteira (mais recentes primeiro) com data/hora, contraparte (nome e direção), descrição,
  situação e valor com sinal "+"/"−", ícone de direção e cor de entrada; entrada e saída MUST NOT
  se diferenciar só pela cor.
- **FR-016**: O lançamento de abertura MUST aparecer como "Saldo inicial", com a descrição
  "Saldo inicial de demonstração" e sem contraparte; descrição vazia aparece como "Sem
  descrição".
- **FR-017**: O extrato MUST ter os filtros Todas / Entradas / Saídas aplicados pelo Bank (o total
  e as páginas refletem o filtro); trocar o filtro volta à primeira página.
- **FR-018**: O extrato MUST ser paginado em 20 itens: no desktop com "Anterior"/"Próxima" e o
  texto "Página X de Y · 20 por página"; no mobile com "Carregar mais", acrescentando itens à
  lista agrupada por dia ("Hoje", "Ontem", "{dia} de {mês}").
- **FR-019**: O filtro e a página atuais MUST fazer parte do endereço da tela, de modo que
  recarregar ou voltar no navegador preserve a visão.
- **FR-020**: O extrato MUST ter estados de carregamento, vazio ("Nenhuma movimentação ainda") e
  erro ("Algo deu errado" com `traceId` e "Tentar de novo"), conforme o protótipo.
- **FR-021**: Documentos de contraparte MUST ser exibidos como entregues pelo Bank (CPF
  mascarado); documentos de destinatário digitados pela pessoa MUST aparecer mascarados fora do
  campo de digitação (cartão do titular, confirmação, comprovante), no padrão do protótipo.

**Início e navegação**

- **FR-022**: No início PF, "Transferir" e "Ver extrato" MUST ficar ativos (cartão de saldo e
  navegação), substituindo o "Em breve" da spec 004; "Últimas movimentações" MUST mostrar os 5
  lançamentos mais recentes, com "Ver tudo" para o extrato.
- **FR-023**: No início PJ, "Últimos recebimentos" MUST mostrar as 5 entradas mais recentes, com
  "Ver tudo" para o extrato filtrado em Entradas; o item de navegação "Recebimentos" MUST abrir
  essa mesma visão; "Cobranças" continua "Em breve".
- **FR-024**: Os totais "Entradas no mês" e "Saídas no mês" do protótipo MUST NOT ser exibidos
  nesta spec (o Bank não oferece esses totais); o cartão mostra apenas a data da última
  atualização.
- **FR-025**: Após uma transferência concluída, saldo, últimas movimentações e extrato MUST
  refletir a operação na próxima exibição, sem recarregar a página.

**Erros**

- **FR-026**: Cada recusa do Bank MUST virar a mensagem do protótipo: `destinatario-nao-encontrado`
  → erro no campo do documento "Nenhuma conta Bank com esse documento."; `autotransferencia` →
  aviso "Esse documento é o seu"; `saldo-insuficiente` → aviso "Saldo insuficiente" com o saldo
  disponível; `idempotencia-divergente` → aviso "Essa transferência mudou"; `acesso-negado` →
  "Indisponível para Pessoa Jurídica"; `validacao` → erros por campo; reenvio já realizado →
  "Essa transferência já foi feita" com link; demais → "Algo deu errado" com `traceId`. Sessão
  expirada segue a spec 004.
- **FR-027**: Avisos de regra de negócio MUST usar o estilo de atenção do protótipo, erros de
  campo o estilo de erro e o reenvio o estilo informativo; nenhum texto técnico da API (`detail`,
  código HTTP) aparece como mensagem principal.

**Acessibilidade, observabilidade e qualidade**

- **FR-028**: As telas MUST atender WCAG 2.2 AA como na spec 004: operação completa por teclado,
  foco visível, rótulos associados, etapa atual do fluxo identificada, titular encontrado, erros,
  avisos e conclusão anunciados por leitores de tela, alvos de toque ≥ 44 px e contraste ≥ 4,5:1.
- **FR-029**: A telemetria do navegador (spec 004) MUST cobrir as novas telas e chamadas, sem
  incluir documento, nome, valor digitado, descrição ou chave de idempotência; endereços de tela
  não contêm dados pessoais.
- **FR-030**: Regras de apresentação (máscaras, interpretação de valor, chave de idempotência,
  agrupamento por dia, tradução de erros) e as telas MUST ter testes de componentes; os fluxos de
  transferência, reenvio, recusas e extrato MUST ser verificados ponta a ponta contra a API real,
  em desktop e mobile, no CI.
- **FR-031**: A documentação de desenvolvimento do frontend MUST descrever as novas telas e como
  preparar contas e saldo para testá-las localmente.

### Key Entities

- **Transferência em andamento**: destinatário (documento, nome, tipo de conta), valor, descrição
  e chave de envio; existe só na aba, durante a sessão, até ser concluída ou abandonada.
- **Comprovante**: transferência concluída como devolvida pelo Bank (identificador, pagador,
  recebedor, valor, descrição, situação e data/hora).
- **Lançamento do extrato (visão)**: tipo (transferência ou abertura), direção, valor, descrição,
  data/hora e, se transferência, contraparte (nome, documento como entregue, tipo de conta).
- **Filtro do extrato**: direção (Todas, Entradas, Saídas) e página.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma PF conclui uma transferência para um destinatário conhecido, do início ao
  comprovante, em menos de 1 minuto, no computador e no celular.
- **SC-002**: Em 100% dos reenvios testados (duplo clique, falha de rede simulada, recarga na
  confirmação, voltar do navegador), cada transferência gera exatamente 1 débito no extrato.
- **SC-003**: 100% dos tipos de recusa do contrato da spec 006 resultam na mensagem do protótipo,
  no local previsto, sem texto técnico e sem perda dos dados digitados.
- **SC-004**: O nome do titular aparece em até 2 segundos após o documento ficar completo, e o
  extrato mostra a primeira página em até 2 segundos, no ambiente local.
- **SC-005**: Saldo, últimas movimentações e extrato refletem uma transferência concluída em 100%
  das verificações, sem recarregar a página.
- **SC-006**: 100% das telas da spec passam nas larguras 320, 390, 768, 1440 e 1920 px sem
  rolagem horizontal; 0 violações A/AA da WCAG 2.2 na verificação automática de cada tela em
  desktop e mobile; transferência e extrato concluídos usando só o teclado.
- **SC-007**: 0 ocorrências de documento, nome, valor, descrição ou chave de idempotência na
  telemetria gerada pelos testes.
- **SC-008**: Os testes de componentes e ponta a ponta das telas desta spec passam no CI em 100%
  das execuções do pull request.

## Assumptions

- **Contrato**: consome o contrato de transferências v1.1.0 (spec 006, com as mudanças aditivas
  da spec 008: consulta por identificador, `pagador.documento` mascarado e `chaveIdempotencia`
  anulável).
- **Issue #36 (concluída)**: `GET /api/v1/transferencias/{id}` foi entregue pela spec
  [008-consulta-transferencia](../008-consulta-transferencia/spec.md); US4 cenário 9 e FR-014 foram
  concluídos na fase 10. O comprovante exibido logo após a transferência ou o reenvio continua
  usando os dados da própria resposta do envio, guardados na aba.
- **Protótipo**: telas e textos de `docs/design/prototipo.md` (capturas 03 a 08 e estados); onde
  o protótipo e o contrato divergem, vale o contrato (por exemplo, o lançamento de abertura usa a
  descrição "Saldo inicial de demonstração" em vez de "Abertura da conta").
- **Totais do mês**: fora de escopo, pois exigiriam um agregado que a API não oferece (FR-024).
- **Busca do titular**: feita automaticamente quando o documento fica completo, sem botão
  extra; a métrica de consultas da spec 006 (FR-009b) cobre a detecção de varreduras.
- **Saldo antecipado**: o aviso de saldo insuficiente antes do envio usa o saldo carregado na
  tela; o Bank continua sendo a decisão final.
- **Fora de escopo**: limites de transferência (issue #33), agendamento, favoritos/contatos,
  cobranças, estornos, exportação do extrato (PDF/CSV) e filtros por período.
- **Navegação mobile**: segue a barra inferior do protótipo (Início, Extrato, Perfil para PF;
  "Transferir" acessível pelo cartão de saldo).
- **Dados de teste**: os testes ponta a ponta criam contas pela API e, quando precisam de mais
  saldo que o de demonstração, recebem transferências de contas de apoio criadas pela API, sem
  acesso direto à base.
