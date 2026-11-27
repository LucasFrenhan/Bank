# Feature Specification: Frontend base — acesso, cadastro e carteira

**Feature Branch**: `004-frontend-base`

**Created**: 2026-11-17

**Status**: Draft

**Input**: Issue #17 — "Iniciar o frontend do Bank sobre os endpoints já entregues pela spec
003-usuarios-carteiras, seguindo o protótipo de telas (issue #15): base do projeto, tokens do
design system, layout desktop e mobile, cliente da API com tradução de erros, telas de login,
cadastro PF/PJ, início PF, início PJ e perfil, sessão com expiração, serviço no ambiente local
e CI. Fora de escopo: transferir, confirmação, comprovante, extrato e cobranças."

## Clarifications

### Session 2026-11-17

- Q: O perfil (documento mascarado, "Cliente desde") e a tela inicial PJ (CNPJ com "Copiar")
  precisam de dados que a API da spec 003 não expõe — incluir uma extensão do backend ou
  omitir? → A: Os dados do titular virão de `GET /api/v1/usuarios/me`, implementado no backend
  pela issue #18 (task separada, feita depois). Esta spec consome esse endpoint; as partes
  que dependem dele só são concluídas após o merge da #18.
- Q: O frontend deve enviar telemetria do navegador ao coletor, como o Princípio VI exige de
  toda aplicação? → A: Sim — traces das navegações e chamadas à API (propagando o
  `traceparent` para a API), erros não tratados e métricas básicas de carregamento, enviados
  ao coletor apenas fora de produção e sem dados sensíveis.
- Q: Qual nível de acessibilidade as telas devem atender? → A: WCAG 2.2 nível AA, com
  navegação completa por teclado e foco visível, rótulos e erros de formulário anunciados por
  leitores de tela e verificação automática de acessibilidade em todas as telas da spec.
- Q: Por quanto tempo a sessão sobrevive no navegador? → A: Só para a aba: sobrevive a
  recarregar a página e termina ao fechar a aba ou ao expirar (3600 s); não é compartilhada
  entre abas.

## User Scenarios & Testing *(mandatory)*

Os usuários são clientes do Bank — Pessoa Física (CPF) e Pessoa Jurídica (CNPJ, numérico ou
alfanumérico) — usando o navegador no computador ou no celular. As telas, textos, cores e
tipografia seguem o [protótipo](../../docs/design/prototipo.md); as regras de negócio vêm da
spec [003](../003-usuarios-carteiras/spec.md), cujo contrato da API é a fonte dos dados.

### User Story 1 - Criar conta pelo navegador (Priority: P1)

Uma pessoa abre o Bank, escolhe o tipo de conta (Pessoa Física ou Pessoa Jurídica), preenche
nome ou razão social, documento, e-mail e senha, e cria a conta. Ao concluir, já entra na
tela inicial com a carteira e o saldo inicial do seu perfil.

**Why this priority**: é a porta de entrada; sem cadastro não há usuário para nenhuma outra
tela.

**Independent Test**: em desktop e em mobile, criar uma conta PF e uma PJ (CNPJ alfanumérico)
e chegar à tela inicial com o saldo do perfil; provocar cada erro de cadastro e ver a
mensagem do protótipo no lugar certo.

**Acceptance Scenarios**:

1. **Given** a tela de cadastro, **When** a pessoa alterna entre Pessoa Física e Pessoa
   Jurídica, **Then** os rótulos ("Nome completo"/"Razão social", "CPF"/"CNPJ"), a máscara do
   documento e o saldo inicial informado mudam conforme o tipo, e a tela avisa que o tipo não
   pode ser alterado depois.
2. **Given** dados válidos, **When** a pessoa cria a conta, **Then** a conta é criada, a
   pessoa entra automaticamente e vê a tela inicial do seu perfil.
3. **Given** um campo inválido (por exemplo, CPF com dígito errado ou senha fraca), **When**
   a pessoa envia o formulário, **Then** cada erro aparece abaixo do campo correspondente, sem
   apagar nada do que foi digitado (inclusive a senha).
4. **Given** um e-mail ou documento já cadastrado, **When** a pessoa envia, **Then** vê a
   mensagem específica ("Este e-mail já tem conta" ou o equivalente para documento) com link
   para o login.
5. **Given** um CNPJ alfanumérico digitado em minúsculas e com máscara, **When** a pessoa
   digita, **Then** o campo exibe o valor em maiúsculas no formato `XX.XXX.XXX/XXXX-00`.

---

### User Story 2 - Entrar e sair da conta (Priority: P1)

Um cliente informa e-mail e senha e entra na conta; pode sair a qualquer momento. Se a sessão
expirar ou deixar de ser aceita, volta ao login com um aviso, e depois de entrar retorna à
tela em que estava.

**Why this priority**: todas as telas da carteira são protegidas; sem sessão o restante não
funciona.

**Independent Test**: entrar com credenciais corretas e ver a tela inicial; tentar com senha
errada e ver a mensagem única; abrir diretamente uma tela protegida sem sessão e ser levado
ao login; simular expiração e confirmar a volta ao login.

**Acceptance Scenarios**:

1. **Given** credenciais corretas, **When** a pessoa entra, **Then** vê a tela inicial do seu
   perfil (PF ou PJ).
2. **Given** e-mail inexistente ou senha incorreta, **When** a pessoa tenta entrar, **Then**
   vê "E-mail ou senha incorretos", sem indicar qual dos dois.
3. **Given** uma pessoa sem sessão, **When** abre o endereço de uma tela protegida, **Then** é
   levada ao login e, após entrar, volta para aquela tela.
4. **Given** uma sessão expirada ou recusada pela API, **When** a pessoa realiza qualquer ação,
   **Then** volta ao login com o aviso "Sua sessão expirou. Entre novamente." e os dados da
   sessão anterior são descartados.
5. **Given** uma pessoa com sessão ativa, **When** escolhe "Sair", **Then** a sessão é
   encerrada e telas protegidas deixam de ser acessíveis sem novo login.
6. **Given** uma pessoa com sessão ativa, **When** abre a tela de login ou de cadastro,
   **Then** é levada à tela inicial.

---

### User Story 3 - Ver a carteira na tela inicial (Priority: P1)

Depois de entrar, o cliente vê a tela inicial do seu perfil com o saldo disponível e a data
da última atualização. A Pessoa Física vê as ações de transferência e extrato como "Em
breve"; a Pessoa Jurídica não vê a ação de transferir e vê o aviso de que contas PJ não
enviam transferências.

**Why this priority**: é o valor central da carteira e o primeiro uso da sessão.

**Independent Test**: com uma conta PF recém-criada, ver saldo R$ 10,00; com uma PJ, ver
R$ 0,00 e nenhuma opção de transferir; recarregar a página e ver o saldo atual.

**Acceptance Scenarios**:

1. **Given** um cliente PF autenticado, **When** abre a tela inicial, **Then** vê "Olá,
   {primeiro nome}", o tipo "Pessoa Física", o saldo em reais (ex.: "R$ 10,00") e "Atualizado
   em {data e hora}".
2. **Given** um cliente PJ autenticado, **When** abre a tela inicial, **Then** vê a razão
   social, o tipo "Pessoa Jurídica", o saldo e a mensagem "Contas Pessoa Jurídica não enviam
   transferências", sem nenhuma ação de transferir (RN-06).
3. **Given** a tela inicial aberta, **When** o saldo muda na API e a pessoa recarrega a tela,
   **Then** vê o saldo atual.
4. **Given** falha de rede ou erro inesperado ao carregar a carteira, **When** a tela é
   exibida, **Then** aparece "Algo deu errado" com o código de rastreio (`traceId`) quando
   houver, e a opção de tentar de novo.
5. **Given** a carteira carregando, **When** a resposta ainda não chegou, **Then** a tela
   exibe um estado de carregamento sem mostrar saldo falso.

---

### User Story 4 - Consultar o perfil (Priority: P2)

O cliente abre o perfil e vê seus dados de conta, o tipo de conta (com o aviso de que não
pode ser alterado), o que sua conta pode fazer conforme o perfil, e a opção de sair.

**Why this priority**: complementa a navegação e torna visíveis as permissões da RN-06;
depende das US2 e US3.

**Independent Test**: abrir o perfil com contas PF e PJ e conferir dados, permissões listadas
e o botão "Sair".

**Acceptance Scenarios**:

1. **Given** um cliente PF, **When** abre o perfil, **Then** vê nome, e-mail, tipo de conta
   e as permissões "Enviar transferências para PF e PJ", "Receber transferências" e "Pagar
   cobranças de cartão de crédito".
2. **Given** um cliente PJ, **When** abre o perfil, **Then** vê razão social, e-mail, tipo de
   conta e as permissões "Receber transferências" e "Gerar cobranças de cartão de crédito".
3. **Given** o perfil, **When** a pessoa escolhe "Sair", **Then** a sessão é encerrada (US2).
4. **Given** um cliente autenticado, **When** abre o perfil, **Then** vê também o documento
   mascarado (CPF `•••.456.789-••`; CNPJ com o mesmo padrão, ocultando o início e os dígitos
   verificadores) e "Cliente desde {data}", obtidos de `GET /api/v1/usuarios/me` (issue #18).
5. **Given** um cliente PJ, **When** abre a tela inicial, **Then** vê o próprio CNPJ completo
   com máscara e a ação "Copiar", que copia o CNPJ sem máscara (issue #18).

---

### User Story 5 - Mesma experiência no computador e no celular (Priority: P2)

Toda tela desta spec funciona e é legível em computador e em celular, com a navegação
adequada a cada um (menu lateral ou superior no desktop, barra inferior no mobile), conforme o
protótipo.

**Why this priority**: exigência da constituição para toda tela; transversal às demais
histórias.

**Independent Test**: percorrer cadastro, login, início PF, início PJ e perfil nas larguras
de referência do protótipo e em larguras intermediárias, conferindo ausência de rolagem
horizontal e alvos de toque adequados.

**Acceptance Scenarios**:

1. **Given** qualquer tela da spec, **When** exibida em 390 × 844 e em 1440 px de largura,
   **Then** corresponde ao layout mobile e desktop do protótipo.
2. **Given** qualquer largura entre 320 e 1920 px, **When** a tela é exibida, **Then** não há
   rolagem horizontal nem conteúdo cortado.
3. **Given** o celular, **When** a pessoa usa a navegação, **Then** os alvos de toque têm ao
   menos 44 px e o teclado adequado aparece em cada campo (e-mail, numérico para CPF,
   alfanumérico para CNPJ).

---

### Edge Cases

- CPF ou CNPJ colado com máscara, espaços ou letras minúsculas: o campo normaliza para a
  máscara do tipo; CNPJ alfanumérico em maiúsculas.
- Troca de PF para PJ (ou o contrário) com documento já digitado: o campo é limpo, pois o
  formato muda.
- Validação local de formato (tamanho, campos obrigatórios) não substitui a da API: erros
  devolvidos pela API por campo sempre aparecem no campo correspondente.
- Erro da API para um campo que não existe na tela: exibido como mensagem geral do formulário.
- Duplo clique em "Criar conta" ou "Entrar": apenas uma requisição é enviada; o botão fica
  desabilitado com indicação de progresso.
- API fora do ar ou sem rede: mensagem "Não foi possível conectar. Verifique sua internet e
  tente de novo.", sem perder o que foi digitado.
- Resposta inesperada (500 ou formato desconhecido): "Algo deu errado" com o `traceId` quando
  houver.
- Sessão expirando com a tela aberta: na próxima ação ou ao atingir o horário de expiração, a
  pessoa volta ao login (US2, cenário 4).
- Conta PJ acessando diretamente o endereço de uma futura tela de transferência: não existe
  rota de transferência nesta spec; qualquer endereço desconhecido mostra uma página "Página
  não encontrada" com link para o início.
- Valores monetários: sempre no formato brasileiro (`R$ 1.234,56`), com algarismos tabulares;
  datas e horas no fuso do navegador, formato brasileiro.
- Nome muito longo (até 150 caracteres) ou razão social extensa: quebra de linha sem cortar a
  tela.

## Requirements *(mandatory)*

### Functional Requirements

**Base e design system**

- **FR-001**: O frontend MUST aplicar os tokens do protótipo — tipografia (Inter; algarismos
  tabulares em valores monetários; fonte monoespaçada para identificadores como `traceId`),
  cores, raios, espaçamento em múltiplos de 4 px, alvos de toque ≥ 44 px e contraste ≥ 4,5:1.
- **FR-001a**: As telas MUST atender a WCAG 2.2 nível AA: toda ação alcançável e executável
  só pelo teclado, com foco visível e ordem lógica; campos com rótulo associado; erros de
  campo e mensagens gerais anunciados por leitores de tela; estados de carregamento
  anunciados; nenhuma informação transmitida só por cor.
- **FR-002**: Toda tela desta spec MUST ter versão desktop e mobile conforme o protótipo,
  sem rolagem horizontal entre 320 e 1920 px de largura (constituição, Restrições Técnicas).
- **FR-003**: A navegação autenticada MUST seguir o protótipo por perfil: PF — Início,
  Transferir (Em breve), Extrato (Em breve), Perfil; PJ — Início, Recebimentos (Em breve),
  Cobranças (Em breve), Perfil. Itens "Em breve" são visíveis mas não navegáveis.

**Cadastro**

- **FR-004**: A tela de cadastro MUST permitir escolher Pessoa Física ou Pessoa Jurídica,
  ajustando rótulos, máscara do documento e o texto do saldo inicial ao tipo escolhido, e
  avisando que o tipo não pode ser alterado depois.
- **FR-005**: O campo de documento MUST aplicar a máscara de CPF (`000.000.000-00`) ou de CNPJ
  (`XX.XXX.XXX/XXXX-00`, aceitando letras nas 12 primeiras posições, exibidas em maiúsculas).
- **FR-006**: O frontend MUST validar localmente apenas formato e obrigatoriedade (para
  feedback imediato) e MUST exibir os erros por campo devolvidos pela API, que é a fonte das
  regras (dígitos verificadores, política de senha, unicidade).
- **FR-007**: A senha MUST ter indicador da política (8+ caracteres com maiúscula, minúscula,
  dígito e símbolo) e opção de mostrar/ocultar.
- **FR-008**: Após o cadastro bem-sucedido, a pessoa MUST entrar automaticamente com as
  credenciais informadas e ir para a tela inicial.

**Sessão**

> **Atualizado pela [spec 016](../016-sessao-refresh-token/spec.md)** (issue #57): FR-009, FR-011, FR-012 e FR-013
> foram substituídos — token de acesso de 5 min renovado sob demanda, renovação e repetição no 401, "Sair"
> no servidor e sessão compartilhada entre as abas
> ([contrato do frontend](../016-sessao-refresh-token/contracts/frontend-sessao.md)). O texto abaixo é o
> histórico desta spec.

- **FR-009**: O login MUST usar e-mail e senha e, em caso de sucesso, iniciar a sessão com o
  token emitido pela API; a sessão MUST expirar no prazo informado pela API (3600 s).
- **FR-010**: Telas da carteira e do perfil MUST exigir sessão; sem sessão, a pessoa vai ao
  login e retorna à tela pedida após entrar.
- **FR-011**: Qualquer resposta 401 da API em tela protegida, ou o fim do prazo da sessão,
  MUST encerrar a sessão e levar ao login com o aviso de sessão expirada.
- **FR-012**: "Sair" MUST descartar a sessão no navegador; o token MUST NOT ficar disponível
  depois de sair nem ser exibido em telas, endereços ou logs do navegador.
- **FR-013**: A sessão MUST ser restrita à aba: sobrevive a recarregar a página, termina ao
  fechar a aba ou ao atingir o prazo do token, e não é compartilhada com outras abas (abrir
  uma nova aba ou um link em nova aba exige o próprio login). Exceção aceita: o comando
  "Duplicar aba" do navegador copia a sessão para a aba duplicada (comportamento do
  navegador).

**Carteira e perfil**

- **FR-014**: A tela inicial MUST mostrar saldo, moeda em reais e data/hora da última
  atualização obtidos da API no momento da exibição, com estados de carregamento e de erro.
- **FR-015**: A tela inicial e a navegação PJ MUST NOT oferecer a ação de transferir (RN-06);
  PF vê "Transferir" e "Ver extrato" como "Em breve".
- **FR-016**: Blocos do protótipo que dependem de transferências (entradas e saídas do mês,
  últimas movimentações, recebimentos) MUST ser substituídos por um estado vazio informativo
  ("Suas movimentações aparecerão aqui") até a spec de transferências.
- **FR-017**: O perfil MUST mostrar nome, e-mail, documento mascarado, "Cliente desde",
  tipo de conta (não alterável) e as permissões do perfil conforme a RN-06, além da ação
  "Sair". Os dados do titular vêm de `GET /api/v1/usuarios/me` (issue #18).
- **FR-017a**: A tela inicial PJ MUST mostrar o próprio CNPJ (com máscara) e a ação "Copiar",
  que copia o valor sem máscara e confirma a cópia na tela (issue #18).

**Erros**

- **FR-018**: Cada `type` de erro da API MUST ser traduzido na mensagem do protótipo:
  `validacao` → erros por campo; `email-ja-cadastrado` e `documento-ja-cadastrado` →
  mensagens específicas com link para o login; `credenciais-invalidas` → "E-mail ou senha
  incorretos"; `nao-autenticado` → sessão expirada (FR-011); `acesso-negado` → "Você não tem
  acesso a esta área"; demais erros → "Algo deu errado" com o `traceId`.
- **FR-019**: A interface MUST NOT exibir textos técnicos da API (`detail`, códigos HTTP,
  stack) como mensagem principal; o `traceId` aparece apenas como código de suporte.

**Observabilidade (constituição, Princípio VI)**

- **FR-019a**: O frontend MUST emitir traces das navegações entre telas e das chamadas à API,
  propagando o contexto de trace (`traceparent`) para que cada chamada apareça no mesmo trace
  da API no painel de telemetria.
- **FR-019b**: O frontend MUST registrar erros não tratados e falhas de chamada à API, e
  métricas básicas de carregamento das telas, identificando-se com um nome de serviço estável
  (`bank-frontend`).
- **FR-019c**: Telemetria MUST NOT conter valores de formulário, senha, token, documento ou
  e-mail; endereços de chamadas não contêm dados pessoais.
- **FR-019d**: O envio de telemetria MUST ser configurável, ligado no ambiente local e
  desligado por padrão em produção; falha no envio MUST NOT afetar o uso das telas.

**Ambiente e qualidade**

- **FR-020**: O frontend MUST poder ser executado localmente por um comando documentado e
  como serviço do ambiente local (Compose), alcançando a API sem configuração manual.
- **FR-021**: MUST existir verificação automatizada em todo pull request que compile o
  frontend e execute seus testes, apta a ser check obrigatório da `main`.
- **FR-022**: Regras de apresentação (máscaras, formatação monetária, tradução de erros,
  controle de sessão e rotas protegidas) MUST ter testes automatizados; os fluxos de cadastro,
  login, início e perfil MUST ser verificados contra a API real em desktop e mobile.
- **FR-023**: A documentação de desenvolvimento MUST explicar como rodar, testar e acessar o
  frontend.

### Key Entities

- **Sessão**: token de acesso, momento de expiração e os dados de identidade lidos dele
  (usuário, e-mail, tipo de conta, carteira); existe só no navegador e só enquanto válida.
- **Carteira (visão)**: saldo, moeda, titular, tipo de conta e data da última atualização,
  como devolvidos pela API.
- **Mensagem de erro**: texto para a pessoa, campo associado (opcional) e código de suporte
  (`traceId`), derivados de uma resposta `ProblemDetails`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma pessoa cria uma conta PF ou PJ e chega à tela inicial em menos de 2
  minutos, no computador e no celular.
- **SC-002**: 100% das telas desta spec passam na verificação nas larguras 320, 390, 768,
  1440 e 1920 px sem rolagem horizontal e conforme o protótipo nas larguras de referência.
- **SC-003**: 100% dos tipos de erro do contrato da spec 003 resultam na mensagem definida
  pelo protótipo, sem texto técnico exposto.
- **SC-004**: Em 100% das tentativas, uma sessão expirada ou recusada leva ao login na
  próxima ação, e nenhuma tela protegida é exibida sem sessão.
- **SC-005**: A tela inicial mostra o saldo em até 2 segundos após o login no ambiente local.
- **SC-006**: Contraste de texto ≥ 4,5:1 e alvos de toque ≥ 44 px em 100% dos elementos
  interativos das telas da spec; 0 violações de nível A/AA da WCAG 2.2 na verificação
  automática de acessibilidade de cada tela, em desktop e mobile; cadastro, login e saída
  concluídos usando só o teclado.
- **SC-008**: Uma ação feita na tela (por exemplo, o login) aparece no painel de telemetria
  local como um único trace que inclui a requisição correspondente da API, em até 10 segundos,
  e 0 ocorrências de dados sensíveis são encontradas na telemetria gerada pelos testes.
- **SC-007**: Uma pessoa desenvolvedora sobe o frontend com a API, a partir de um clone limpo
  e seguindo apenas a documentação, em até 10 minutos (desconsiderando downloads).

## Assumptions

- **Base**: usa os endpoints e o contrato da spec 003 sem alterá-los; o protótipo
  (`docs/design/prototipo.md`) é a referência visual e de textos.
- **Dependência da issue #18**: `GET /api/v1/usuarios/me` (contrato proposto na issue) é
  implementado no backend por uma spec própria, feita depois. O restante desta spec não
  depende dele; US4 cenários 4–5, FR-017 (documento e "Cliente desde") e FR-017a só são
  concluídos após o merge da #18.
- **Fora de escopo**: transferir, confirmação, comprovante, extrato (issues #8/#13),
  cobranças, "Esqueci a senha" (não existe na API — o link do protótipo não é exibido),
  edição de dados cadastrais, idiomas além do português do Brasil e modo escuro.
- **Sessão**: restrita à aba (FR-013), sem "lembrar de mim"; a API não oferece refresh token
  nem cookie `HttpOnly`, então expirar significa entrar de novo. Migrar para cookie seguro
  fica para uma spec futura de segurança.
- **Entrada automática após cadastro**: feita com um login logo após o cadastro, pois a API
  de cadastro não devolve token.
- **Navegadores**: versões atuais de Chrome, Edge, Firefox e Safari (desktop e mobile).
- **Ambiente de demonstração**: dados e saldos são fictícios; a tela de login exibe o aviso
  "Ambiente de demonstração", como no protótipo.
