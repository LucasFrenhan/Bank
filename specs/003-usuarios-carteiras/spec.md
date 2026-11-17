# Feature Specification: Usuários (PF/PJ), Carteiras e Autenticação

**Feature Branch**: `003-usuarios-carteiras`

**Created**: 2026-11-17

**Status**: Draft

**Input**: Issue #5 — "Modelagem de Usuários (PF/PJ), Carteiras e Autenticação: especificar
o domínio de identidade, perfis de usuário (Pessoa Física e Pessoa Jurídica),
provisionamento atômico de carteiras digitais e autenticação via JWT, estabelecendo o
contrato antes da implementação." Implementação acompanhada pela issue #6.

## Clarifications

### Session 2026-11-17

- Q: O CNPJ deve aceitar apenas o formato numérico ou também o novo CNPJ alfanumérico da
  Receita Federal? → A: Deve suportar o CNPJ alfanumérico (IN RFB nº 2.229/2024), com a
  validação de dígitos verificadores correspondente, mantendo compatibilidade total com os
  CNPJs numéricos existentes.
- Q: Como conciliar o saldo inicial de demonstração com a exigência de lançamentos
  imutáveis (Princípio IV)? → A: Os dados de carteiras existem só em ambientes não
  produtivos (desenvolvimento e testes). Toda carga/saldo de demonstração ocorre apenas fora
  de `Production`; em `Production` as carteiras nascem com R$ 0,00. A spec de lançamentos
  (issue #8) criará, na inicialização da aplicação e apenas fora de `Production`, o lançamento
  de abertura das carteiras com saldo sem lançamento correspondente.

## User Scenarios & Testing *(mandatory)*

Os usuários desta funcionalidade são clientes do Bank de dois perfis — **Pessoa Física**
(identificada por CPF) e **Pessoa Jurídica** (identificada por CNPJ) — e os consumidores da
API (frontend e futuras specs, como o motor de transferências da issue #8), que dependem da
identidade autenticada e da carteira de cada usuário.

### User Story 1 - Abrir conta e receber uma carteira (Priority: P1)

Uma pessoa (física ou jurídica) informa nome completo ou razão social, e-mail, documento
(CPF ou CNPJ), tipo de conta e senha. O sistema valida os dados, cria o cadastro e, na mesma
operação, cria a carteira digital do usuário com o saldo inicial de demonstração do seu
perfil.

**Why this priority**: sem cadastro e carteira não existe usuário, login nem movimentação;
é a porta de entrada de todas as funcionalidades financeiras.

**Independent Test**: enviar um cadastro válido de PF e outro de PJ e verificar que ambos
são criados, cada um com exatamente uma carteira e o saldo inicial correto; enviar
cadastros inválidos e duplicados e verificar as respostas de erro padronizadas.

**Acceptance Scenarios**:

1. **Given** um CPF válido ainda não cadastrado, **When** a pessoa se cadastra como Pessoa
   Física, **Then** o cadastro é criado (201) com o identificador do usuário, o
   identificador da carteira e saldo inicial de R$ 10,00.
2. **Given** um CNPJ válido ainda não cadastrado, **When** a empresa se cadastra como Pessoa
   Jurídica, **Then** o cadastro é criado (201) com saldo inicial de R$ 0,00.
3. **Given** um e-mail já cadastrado (em qualquer combinação de maiúsculas/minúsculas),
   **When** um novo cadastro o utiliza, **Then** a resposta é 409 no formato padronizado de
   erro e nenhum usuário ou carteira é criado.
4. **Given** um documento já cadastrado (com ou sem máscara), **When** um novo cadastro o
   utiliza, **Then** a resposta é 409 no formato padronizado e nada é criado.
5. **Given** um CPF ou CNPJ com dígitos verificadores inválidos, tamanho incompatível com o
   tipo de conta, senha fora da política ou tipo de conta desconhecido, **When** o cadastro
   é enviado, **Then** a resposta é 400 com a lista de erros por campo e nada é criado.
6. **Given** uma falha durante a criação da carteira, **When** o cadastro é processado,
   **Then** o usuário também não é criado (nenhum usuário fica sem carteira).

---

### User Story 2 - Entrar no sistema e obter credencial de acesso (Priority: P1)

Um usuário cadastrado informa e-mail e senha e recebe um token de acesso com validade
limitada que identifica o usuário, seu perfil e sua carteira, para ser usado nas chamadas
autenticadas.

**Why this priority**: toda operação sobre a carteira (consulta, transferências,
cobranças) exige um usuário autenticado; sem login, o cadastro não tem utilidade.

**Independent Test**: cadastrar um usuário, fazer login com as credenciais corretas e
verificar o token (tipo, validade e informações de identidade); tentar login com senha
errada e com e-mail inexistente e verificar a mesma resposta 401.

**Acceptance Scenarios**:

1. **Given** um usuário cadastrado, **When** ele faz login com e-mail e senha corretos,
   **Then** recebe 200 com um token do tipo `Bearer`, validade de 3600 segundos e as
   informações de identificador do usuário, e-mail, tipo de conta e identificador da
   carteira.
2. **Given** um usuário cadastrado, **When** ele faz login com o e-mail em maiúsculas,
   **Then** o login é aceito (e-mail não diferencia maiúsculas de minúsculas).
3. **Given** uma senha incorreta ou um e-mail não cadastrado, **When** o login é tentado,
   **Then** a resposta é 401 no formato padronizado, idêntica nos dois casos (sem revelar
   se o e-mail existe).
4. **Given** um corpo de login sem e-mail ou sem senha, **When** é enviado, **Then** a
   resposta é 400 com erros por campo.

---

### User Story 3 - Consultar a própria carteira (Priority: P2)

Um usuário autenticado consulta sua carteira e vê o identificador, o titular, o tipo de
conta, o saldo atual e a data da última movimentação.

**Why this priority**: demonstra o vínculo usuário–carteira e o saldo, e é o primeiro
endpoint protegido do sistema; depende das US1 e US2.

**Independent Test**: com o token de um usuário recém-cadastrado, consultar a carteira e
verificar o saldo inicial do perfil; sem token, ou com token inválido/expirado, verificar
a resposta 401.

**Acceptance Scenarios**:

1. **Given** um usuário autenticado, **When** consulta sua carteira, **Then** recebe 200 com
   os dados da **sua** carteira (nunca a de outro usuário) e o saldo atual.
2. **Given** uma requisição sem token, com token adulterado ou expirado, **When** a carteira
   é consultada, **Then** a resposta é 401 no formato padronizado.
3. **Given** um token válido cujas informações de identidade exigidas estão ausentes,
   **When** a carteira é consultada, **Then** a resposta é 403 no formato padronizado.

---

### User Story 4 - Permissões por perfil disponíveis para as próximas funcionalidades (Priority: P3)

As regras de permissão por perfil (RN-06) ficam definidas e disponíveis como políticas de
acesso nomeadas, para que as funcionalidades de transferência e de cobrança as apliquem sem
redefinir quem pode fazer o quê.

**Why this priority**: os endpoints que exercem essas permissões pertencem a outras specs
(issue #8 e posteriores); aqui garante-se apenas que a regra existe, é única e é testável.

**Independent Test**: verificar, por testes da regra de domínio, que o perfil PF possui as
permissões de transferir, receber e pagar cobranças, e que o perfil PJ possui as de receber
e gerar cobranças, mas não a de transferir.

**Acceptance Scenarios**:

1. **Given** um usuário PF, **When** suas permissões são avaliadas, **Then** ele pode
   realizar transferências, receber transferências e pagar cobranças de cartão.
2. **Given** um usuário PJ, **When** suas permissões são avaliadas, **Then** ele pode
   receber transferências e gerar cobranças de cartão, e **não** pode realizar
   transferências P2P.

---

### Edge Cases

**Validação de CPF (RN-03)**

- CPF válido com e sem máscara (`529.982.247-25` e `52998224725`): aceito e armazenado só com
  dígitos.
- CPF com todos os dígitos iguais (`00000000000`, `11111111111`, …, `99999999999`): rejeitado,
  embora satisfaça o cálculo dos dígitos verificadores.
- CPF com primeiro dígito verificador errado; com segundo dígito verificador errado:
  rejeitado.
- CPF com dígito verificador cujo cálculo resulta em resto < 2 (dígito `0`): aceito quando
  correto (ex.: `12345678909`).
- CPF com 10 ou 12 dígitos, com letras ou com caracteres além de `.` e `-`: rejeitado.
- CPF com zeros à esquerda (ex.: `01234567890`): zeros preservados; nunca tratado como
  número.

**Validação de CNPJ (RN-03)**

- CNPJ numérico válido com e sem máscara (`11.222.333/0001-81` e `11222333000181`): aceito e
  armazenado sem máscara.
- CNPJ alfanumérico válido com e sem máscara (`12.ABC.345/01DE-35` e `12ABC34501DE35`):
  aceito e armazenado sem máscara.
- CNPJ alfanumérico em minúsculas (`12abc34501de35`): aceito e armazenado em maiúsculas
  (`12ABC34501DE35`); para a unicidade, `12abc34501de35` e `12ABC34501DE35` são o mesmo
  documento.
- CNPJ alfanumérico com primeiro ou segundo dígito verificador errado (ex.:
  `12ABC34501DE36`): rejeitado.
- CNPJ numérico com primeiro ou segundo dígito verificador errado: rejeitado.
- CNPJ com letra em uma das duas posições de dígito verificador (ex.: `12ABC34501DE3A`):
  rejeitado.
- CNPJ com os 14 caracteres iguais (`00000000000000`, `AAAAAAAAAAAAAA`, …): rejeitado.
- CNPJ com 13 ou 15 caracteres, com letras acentuadas, `Ç` ou caracteres além de `.`, `/` e
  `-`: rejeitado.
- CPF continua exclusivamente numérico: CPF com letras é rejeitado.

**Compatibilidade documento × tipo de conta (RN-01/RN-03)**

- CPF válido enviado com tipo `PessoaJuridica`, ou CNPJ válido com tipo `PessoaFisica`:
  400 com erro no campo `documento`.
- Tipo de conta ausente, vazio, com grafia diferente (ex.: `pessoafisica`, `PF`) ou
  numérico: 400 com erro no campo `tipoConta`.

**Cadastro e unicidade (RN-02/RN-04)**

- Dois cadastros simultâneos com o mesmo e-mail ou documento: exatamente um é criado; o
  outro recebe 409 (a unicidade é garantida pela base, não apenas por consulta prévia).
- E-mail com espaços nas extremidades ou letras maiúsculas: normalizado (sem espaços, em
  minúsculas) antes da validação de unicidade e do armazenamento.
- Nome com mais de 150 caracteres, vazio ou só com espaços: 400.
- E-mail com formato inválido ou com mais de 150 caracteres: 400.
- Corpo de requisição malformado (JSON inválido) ou vazio: 400 no formato padronizado.
- Campos adicionais desconhecidos no corpo (ex.: `saldo`): ignorados; o saldo inicial
  nunca é definido pelo cliente.

**Login e token**

- Token expirado há 1 segundo: 401.
- Token assinado com outra chave ou com algoritmo diferente do esperado: 401.
- Usuário cujo token ainda é válido consulta a carteira: recebe sempre o saldo atual, não
  o do momento do login.
- Token válido de um usuário que não existe mais na base: 404 `carteira-nao-encontrada`.

## Requirements *(mandatory)*

### Functional Requirements

**Perfis e cadastro**

- **FR-001**: O sistema MUST suportar exclusivamente dois tipos de conta: `PessoaFisica` e
  `PessoaJuridica` (RN-01).
- **FR-002**: O tipo de conta MUST ser imutável após o cadastro; nenhuma operação desta ou
  de futuras specs pode alterá-lo (RN-01).
- **FR-003**: O cadastro MUST exigir nome completo (ou razão social), e-mail, documento,
  tipo de conta e senha, conforme o contrato de registro.
- **FR-004**: Contas `PessoaFisica` MUST exigir CPF válido: 11 dígitos, dígitos
  verificadores corretos e não composto por um único dígito repetido (RN-03).
- **FR-005**: Contas `PessoaJuridica` MUST exigir CNPJ válido, numérico ou alfanumérico
  (IN RFB nº 2.229/2024) (RN-03):
  - 14 caracteres: as 12 primeiras posições (raiz e ordem) são letras `A`–`Z` ou dígitos
    `0`–`9`; as 2 últimas (dígitos verificadores) são sempre dígitos `0`–`9`;
  - cada caractere vale o seu código ASCII menos 48 (`0`–`9` → 0–9; `A` → 17, `B` → 18, …,
    `Z` → 42);
  - 1º dígito verificador: soma dos valores das 12 primeiras posições multiplicados pelos
    pesos `5,4,3,2,9,8,7,6,5,4,3,2`; 2º dígito verificador: soma dos valores das 13
    primeiras posições multiplicados pelos pesos `6,5,4,3,2,9,8,7,6,5,4,3,2`; em ambos, se o
    resto da divisão da soma por 11 for menor que 2 o dígito é `0`, senão é `11 − resto`;
  - não composto por um único caractere repetido nas 14 posições.

  Para CNPJs só com dígitos, a regra coincide com o algoritmo numérico tradicional, de modo
  que todo CNPJ numérico válido continua válido.
- **FR-006**: O documento MUST ser aceito com ou sem máscara (CPF `000.000.000-00`; CNPJ
  `XX.XXX.XXX/XXXX-00`) e em letras maiúsculas ou minúsculas. Ele MUST ser armazenado e
  devolvido sem máscara e com letras em maiúsculas, preservando zeros à esquerda. A
  unicidade (FR-008) MUST considerar esse formato normalizado.
- **FR-007**: O e-mail MUST ser normalizado (sem espaços nas extremidades, em minúsculas)
  antes da verificação de unicidade, do armazenamento e do login.
- **FR-008**: E-mail e documento MUST ser únicos na base; violações MUST retornar 409 com
  um `type` de erro estável e distinto para e-mail duplicado e para documento duplicado
  (RN-02). A unicidade MUST ser garantida mesmo sob cadastros concorrentes.
- **FR-009**: A senha MUST atender à política: mínimo de 8 e máximo de 128 caracteres,
  com ao menos uma letra maiúscula, uma minúscula, um dígito e um caractere especial.
- **FR-010**: A senha MUST ser armazenada apenas como hash seguro com sal, nunca em texto
  puro nem em forma reversível, e MUST NOT ser devolvida em nenhuma resposta.
- **FR-011**: Falhas de validação do cadastro MUST retornar 400 com a lista de erros por
  campo, reportando todos os campos inválidos em uma única resposta.

**Carteira**

- **FR-012**: Todo usuário MUST ter exatamente uma carteira (1:1), criada na mesma
  operação atômica do cadastro; se qualquer parte falhar, nem usuário nem carteira são
  criados (RN-04).
- **FR-013**: Fora de `Production`, o saldo inicial de demonstração MUST ser R$ 10,00 para
  `PessoaFisica` e R$ 0,00 para `PessoaJuridica`, com ambos os valores configuráveis sem
  alterar código (RN-05). Em `Production`, o saldo inicial MUST ser R$ 0,00 para os dois
  perfis, e a API MUST recusar subir se a configuração definir saldo inicial diferente de
  zero nesse ambiente. A configuração MUST rejeitar valores negativos na inicialização.
- **FR-014**: O saldo da carteira MUST nunca ser negativo, garantido também pela base de
  dados.
- **FR-015**: Valores monetários MUST ter precisão exata de duas casas decimais e estar
  denominados em reais (BRL); a moeda MUST ser registrada com a carteira (constituição,
  Princípio IV).
- **FR-016**: A carteira MUST registrar a data/hora (UTC) da última movimentação; na
  criação, ela é igual à data/hora de criação.

**Autenticação**

- **FR-017**: O login MUST validar e-mail e senha e, em caso de sucesso, devolver um token
  de acesso do tipo `Bearer` com validade de 3600 segundos (configurável).
- **FR-018**: O token MUST conter as informações: identificador do usuário (`sub`),
  `email`, `tipo_conta` e `carteira_id`.
- **FR-019**: Credenciais inválidas MUST retornar 401 com resposta idêntica para e-mail
  inexistente e senha incorreta, e com tempo de resposta que não permita distinguir os
  dois casos.
- **FR-020**: Endpoints protegidos MUST rejeitar com 401 requisições sem token, com token
  expirado, adulterado ou assinado com chave/algoritmo diferente, e com 403 tokens válidos
  sem as informações exigidas (constituição, Princípio VII).
- **FR-021**: A chave de assinatura dos tokens MUST vir de configuração externa ao código
  versionado.

**Consulta da carteira**

- **FR-022**: Um usuário autenticado MUST poder consultar apenas a própria carteira,
  identificada a partir do token, recebendo identificador da carteira, identificador do
  usuário, nome do titular, tipo de conta, saldo atual e data da última movimentação. Se o
  usuário do token não existir mais na base, a resposta MUST ser 404 com `type`
  `carteira-nao-encontrada`.

**Permissões por perfil**

- **FR-023**: As permissões por perfil (RN-06) MUST ser definidas uma única vez no domínio
  e expostas como políticas de acesso nomeadas: PF — transferir, receber transferências,
  pagar cobranças de cartão; PJ — receber transferências, gerar cobranças de cartão.
  Endpoints de transferência e cobrança (outras specs) MUST usar essas políticas.

**Transversais**

- **FR-024**: Todas as respostas de erro desta feature MUST seguir o formato
  `ProblemDetails` (`application/problem+json`) com `traceId`, e cada erro de regra de
  negócio MUST ter um `type` estável documentado no contrato (constituição, Princípio III).
- **FR-025**: Senhas, tokens e documentos completos MUST NOT aparecer em logs, traces ou
  métricas; quando necessários para diagnóstico, documentos são mascarados (constituição,
  Princípio VI).
- **FR-026**: Os endpoints `POST /api/v1/autenticacao/registrar`,
  `POST /api/v1/autenticacao/login` e `GET /api/v1/carteiras/minha` MUST seguir o contrato
  `contracts/usuarios-carteiras-api.md` (fonte de verdade; parte da issue #5 e registra os
  desvios: saldo alinhado a FR-013, campo `moeda`, 404 de FR-022) e ser publicados no
  documento OpenAPI.
- **FR-027**: O sistema MUST expor métricas de negócio para cadastros concluídos (por
  tipo de conta) e tentativas de login (sucesso/falha).

### Key Entities

- **Usuário**: titular de uma conta. Atributos: identificador (ordenável por tempo), nome
  completo ou razão social (até 150 caracteres), e-mail único normalizado (até 150),
  documento único sem máscara (CPF: 11 dígitos; CNPJ: 14 caracteres alfanuméricos em
  maiúsculas, com os 2 últimos sempre dígitos), tipo de conta imutável,
  credencial protegida por hash e data/hora de criação em UTC.
- **Carteira**: conta de saldo digital de um usuário. Atributos: identificador, titular
  (exatamente um usuário; cada usuário tem exatamente uma carteira), saldo não negativo com
  duas casas decimais, moeda (BRL) e data/hora da última movimentação em UTC.
- **Tipo de conta**: `PessoaFisica` ou `PessoaJuridica`; determina o documento exigido, o
  saldo inicial e as permissões.
- **Token de acesso**: credencial temporária emitida no login, portadora de identificador
  do usuário, e-mail, tipo de conta e identificador da carteira.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma pessoa conclui o cadastro e o primeiro login em menos de 2 minutos usando
  a documentação interativa da API.
- **SC-002**: 100% dos usuários cadastrados possuem exatamente uma carteira, inclusive
  quando falhas são injetadas durante o cadastro (nenhum usuário sem carteira, nenhuma
  carteira órfã).
- **SC-003**: 100% dos cenários de borda de CPF e CNPJ listados nesta spec produzem o
  resultado esperado nos testes automatizados.
- **SC-004**: Em 50 tentativas concorrentes de cadastro com o mesmo e-mail ou documento,
  exatamente 1 é aceita e as demais recebem 409.
- **SC-005**: 100% das respostas de erro desta feature observadas nos testes estão no
  formato padronizado com `traceId` e `type` documentado.
- **SC-006**: Nenhuma senha, token ou documento completo é encontrado nos logs, traces e
  métricas gerados pelos testes da feature.
- **SC-007**: 95% das requisições de cadastro, login e consulta de carteira são respondidas
  em menos de 1 segundo no ambiente local.

## Assumptions

- **Saldo inicial**: RN-05 (PF R$ 10,00; PJ R$ 0,00) é a fonte de verdade; os exemplos de
  resposta da issue #5 com `1000.00` são inconsistentes e serão corrigidos nos contratos
  desta spec.
- **Arquivo da spec**: a issue cita `specs/01-usuarios-e-carteiras.md`; segue-se a convenção
  do repositório e da constituição (`specs/003-usuarios-carteiras/`).
- **CNPJ**: suporta o CNPJ alfanumérico da Receita Federal (IN RFB nº 2.229/2024) e os CNPJs
  numéricos existentes (ver Clarifications e FR-005). A coluna `documento` mantém até 14
  caracteres, mas passa a guardar letras maiúsculas além de dígitos. Isso diverge do
  "apenas números" da issue #5 e será refletido no modelo de dados e nos contratos.
- **Política de senha**: adotada a política de FR-009 por ser compatível com o exemplo da
  issue (`SenhaForte@123`) e com os padrões usuais; não está definida na issue.
- **Fora de escopo**: refresh token, logout/revogação de token, recuperação e troca de
  senha, confirmação de e-mail, bloqueio por tentativas de login, alteração de dados
  cadastrais, exclusão de conta, múltiplas carteiras e frontend.
- **Saldo inicial não é lançamento financeiro (por enquanto)**: o saldo de demonstração é
  atribuído na criação da carteira e só existe fora de `Production` (ver Clarifications);
  os dados de carteiras atuais são apenas de desenvolvimento e testes. O histórico de
  lançamentos e a idempotência de movimentações (constituição, Princípio IV) pertencem à
  spec de transferências (issue #8), que MUST criar, na inicialização da aplicação e apenas
  fora de `Production`, o lançamento de abertura das carteiras existentes, de modo que o
  saldo passe a coincidir com a soma dos lançamentos. O cadastro em si é protegido contra
  duplicidade pela unicidade de e-mail e documento.
- **Permissões**: esta spec define as políticas (FR-023) e as testa no domínio; a aplicação
  delas a endpoints acontece nas specs de transferência e cobrança.
- **Base existente**: reutiliza a solução, o pipeline de erros padronizados, a
  documentação interativa e a observabilidade da spec 002, e o PostgreSQL do ambiente local
  da spec 001.
