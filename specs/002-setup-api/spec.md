# Feature Specification: Setup da API, Documentação Interativa e Observabilidade

**Feature Branch**: `002-setup-api`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Issue #3 — "Inicializar o projeto Minimal API, configurar geração nativa de
OpenAPI do .NET, expor a UI do Scalar para documentação interativa e plugar os pacotes do
OpenTelemetry (Traces, Metrics e Logs) exportando via OTLP. Entregável: API rodando,
acessível via /scalar/v1 e enviando telemetria básica ao coletor."

## Clarifications

### Session 2026-11-16

- Q: Escopo do esqueleto da solução — quatro camadas e projetos de teste, ou só a API? →
  A: Solução completa: Api, Application, Domain, Infrastructure e projetos de teste
  unitário, de integração e de arquitetura.

## User Scenarios & Testing *(mandatory)*

Os usuários desta funcionalidade são as pessoas desenvolvedoras do Bank e os futuros
consumidores da API (frontend e integrações). Ela estabelece a base sobre a qual todas as
funcionalidades de negócio serão construídas; nenhuma regra de negócio é entregue aqui.

### User Story 1 - Executar a API localmente (Priority: P1)

Uma pessoa desenvolvedora, com o ambiente local da spec 001 no ar, executa a API e
confirma que ela está respondendo por meio de um endpoint de saúde.

**Why this priority**: sem uma API executável não há onde implementar nenhuma
funcionalidade; é o esqueleto de todas as specs seguintes.

**Independent Test**: a partir de um clone limpo, executar o comando documentado e
consultar o endpoint de saúde, que deve responder com sucesso.

**Acceptance Scenarios**:

1. **Given** um clone do repositório com o SDK exigido instalado, **When** a pessoa executa
   o comando documentado para rodar a API, **Then** a API inicia e escuta em um endereço
   local documentado.
2. **Given** a API em execução, **When** o endpoint de saúde é consultado, **Then** ele
   responde com sucesso e sem exigir autenticação.
3. **Given** o ambiente local da spec 001, **When** a API é iniciada junto com ele pelo
   comando único de subida do ambiente, **Then** ela só inicia depois que os serviços de
   que depende estão saudáveis e reporta seu próprio estado de saúde.

---

### User Story 2 - Explorar o contrato da API na documentação interativa (Priority: P1)

Uma pessoa consumidora da API abre a documentação interativa em `/scalar/v1`, vê todos os
endpoints com seus formatos de requisição, resposta e erro, e consegue executar
requisições de teste a partir dela.

**Why this priority**: é o entregável explícito da issue e o mecanismo pelo qual o
Princípio V (contratos explícitos) será cumprido por todas as specs seguintes.

**Independent Test**: com a API em execução em ambiente de desenvolvimento, abrir
`/scalar/v1`, localizar o endpoint de saúde e executá-lo pela própria interface.

**Acceptance Scenarios**:

1. **Given** a API em execução em ambiente de desenvolvimento, **When** a pessoa acessa
   `/scalar/v1`, **Then** a documentação interativa é exibida com todos os endpoints
   publicados.
2. **Given** a documentação aberta, **When** a pessoa executa um endpoint pela interface,
   **Then** recebe a resposta real da API.
3. **Given** a API em execução, **When** o documento do contrato é solicitado, **Then** é
   retornado um documento OpenAPI válido descrevendo os endpoints, incluindo o formato
   padronizado de erro.
4. **Given** a API configurada como ambiente de produção, **When** `/scalar/v1` ou o
   documento OpenAPI são solicitados, **Then** eles não estão disponíveis.

---

### User Story 3 - Diagnosticar a API pela telemetria (Priority: P2)

A pessoa desenvolvedora faz requisições à API e, no Aspire Dashboard do ambiente local,
vê os traces de cada requisição, as métricas da API e os logs estruturados correlacionados
ao trace que os gerou.

**Why this priority**: o Princípio VI exige observabilidade nativa; estabelecer o padrão
agora evita retrofit em todas as funcionalidades futuras. Depende da US1.

**Independent Test**: com API e ambiente local no ar, fazer uma requisição e localizar no
dashboard o trace, os logs dessa requisição (vinculados ao trace) e as métricas da API.

**Acceptance Scenarios**:

1. **Given** API e coletor em execução, **When** uma requisição é feita, **Then** um trace
   com o método, a rota e o status da resposta aparece no dashboard identificado pelo nome
   do serviço da API.
2. **Given** uma requisição que gera logs, **When** os logs são consultados no dashboard,
   **Then** estão estruturados (propriedades nomeadas) e vinculados ao trace da requisição.
3. **Given** a API em execução, **When** as métricas são consultadas no dashboard, **Then**
   aparecem métricas de requisições HTTP (contagem e duração) e de runtime.
4. **Given** o coletor fora do ar, **When** a API recebe requisições, **Then** ela continua
   respondendo normalmente (a telemetria é descartada sem afetar as respostas).

---

### User Story 4 - Erros padronizados desde o primeiro endpoint (Priority: P2)

Um consumidor que chama uma rota inexistente, envia uma requisição inválida ou provoca uma
falha inesperada recebe sempre uma resposta de erro no formato padronizado, com um
identificador de correlação e sem detalhes internos.

**Why this priority**: o Princípio III exige `ProblemDetails` em toda resposta de erro;
como esta spec cria o pipeline HTTP, é aqui que o padrão nasce para todos os endpoints.

**Independent Test**: chamar uma rota inexistente e forçar uma falha não tratada em um
endpoint de teste; ambas as respostas devem estar no formato padronizado com `traceId`.

**Acceptance Scenarios**:

1. **Given** a API em execução, **When** uma rota inexistente é chamada, **Then** a resposta
   é 404 no formato padronizado de erro, com `traceId`.
2. **Given** uma falha não tratada durante uma requisição, **When** a resposta é gerada,
   **Then** ela é 500 no formato padronizado, com `traceId`, sem stack trace, mensagem de
   exceção ou dados internos.
3. **Given** um erro retornado ao consumidor, **When** o `traceId` é procurado no
   dashboard, **Then** o trace correspondente é encontrado.

---

### User Story 5 - Base arquitetural verificada automaticamente (Priority: P3)

A pessoa desenvolvedora encontra a solução já organizada nas camadas exigidas pela
constituição e conta com verificações automatizadas que impedem violações de camada e
quebras de build antes do merge.

**Why this priority**: o Princípio II exige testes de arquitetura e o Princípio VII exige
CI com build e testes; sem essa base, as specs de negócio começariam sem as proteções que
a constituição torna obrigatórias.

**Independent Test**: introduzir, em uma branch de teste, uma referência proibida entre
camadas (por exemplo, o endpoint usando a camada de persistência) e verificar que o
build/teste falha localmente e no CI.

**Acceptance Scenarios**:

1. **Given** a solução, **When** suas camadas são inspecionadas, **Then** existem as camadas
   de API, Application, Domain e Infrastructure e os projetos de teste, conforme a
   constituição: projetos de API, Application, Domain e Infrastructure, e projetos de teste
   unitário, de integração e de arquitetura (Application, Domain e Infrastructure podem
   nascer sem código de negócio).
2. **Given** uma violação da direção de dependências entre camadas, **When** os testes são
   executados, **Then** os testes de arquitetura falham apontando a violação.
3. **Given** um pull request, **When** o CI é executado, **Then** ele compila a solução e
   executa todos os testes, falhando o PR se algum falhar.

---

### Edge Cases

- A porta da API já está ocupada: a falha de inicialização identifica a porta, que pode
  ser alterada por configuração local sem editar arquivos versionados.
- O coletor de telemetria não está disponível na inicialização ou cai depois: a API sobe e
  responde normalmente.
- A API é executada fora do Compose (pelo SDK) com o ambiente local no ar: a telemetria
  chega ao mesmo coletor pelas portas publicadas no host.
- Requisição com corpo malformado (JSON inválido) a um endpoint: resposta 400 no formato
  padronizado.
- Método HTTP não suportado em uma rota existente: resposta 405 no formato padronizado.
- Rota de saúde e documentação não geram ruído excessivo de telemetria que esconda as
  requisições de negócio.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O repositório MUST conter a solução do backend sob `src/backend/`, executável
  por um comando documentado, usando a plataforma fixada pela constituição (.NET 10, Minimal
  APIs), com a versão do SDK fixada no repositório.
- **FR-002**: A API MUST expor um endpoint de saúde público (sem autenticação) que indique
  se a aplicação está em execução.
- **FR-003**: A API MUST gerar o documento OpenAPI nativamente a partir dos endpoints, sem
  documento escrito à mão.
- **FR-004**: A API MUST expor a documentação interativa em `/scalar/v1`, baseada no
  documento OpenAPI gerado, permitindo executar requisições a partir dela.
- **FR-005**: O documento OpenAPI e a documentação interativa MUST estar disponíveis apenas
  em ambientes não produtivos (constituição, Princípio V).
- **FR-006**: A API MUST exportar traces, métricas e logs via OTLP para o endpoint
  configurado, identificando-se com um nome de serviço estável.
- **FR-007**: Os traces MUST cobrir as requisições HTTP recebidas, e a instrumentação de
  chamadas HTTP de saída MUST ficar configurada; as métricas MUST incluir métricas de
  requisições HTTP e de runtime.
- **FR-008**: Os logs MUST ser estruturados e correlacionados ao trace ativo; dados
  sensíveis MUST NOT ser registrados (constituição, Princípio VI).
- **FR-009**: Falhas no envio de telemetria MUST NOT afetar o atendimento das requisições.
- **FR-010**: Toda resposta de erro (4xx e 5xx) produzida pelo pipeline da API MUST usar o
  formato `ProblemDetails` (`application/problem+json`) com a extensão `traceId`; respostas
  5xx MUST NOT expor detalhes internos (constituição, Princípio III).
- **FR-011**: O endereço do coletor, o nome do ambiente e a porta da API MUST ser
  configuráveis sem alterar arquivos versionados; os padrões MUST apontar para o ambiente
  local da spec 001.
- **FR-012**: A API MUST poder ser executada como serviço do ambiente local da spec 001,
  dependendo dos serviços de infraestrutura com condição de saúde e expondo seu próprio
  healthcheck (cumpre a comprovação de dependência adiada pela spec 001).
- **FR-013**: A solução MUST estar organizada nas camadas definidas pela constituição, com
  testes de arquitetura automatizados que falham quando a direção de dependências é
  violada.
- **FR-014**: MUST existir um pipeline de CI que compile a solução e execute todos os
  testes (unitários, de integração e de arquitetura) em todo pull request, sem filtro de
  paths, apto a ser marcado como check obrigatório da `main`.
- **FR-015**: Os endpoints desta spec MUST ter testes de integração cobrindo o caminho
  feliz e os erros documentados (constituição, Princípio VII).
- **FR-016**: Toda dependência adicionada MUST ter justificativa no plano (constituição,
  Restrições Técnicas).

### Key Entities

Não há entidades de negócio nesta feature.

- **Configuração da API**: ambiente (desenvolvimento/produção), porta, endpoint do coletor
  OTLP, nome do serviço.
- **Resposta de erro padronizada**: tipo, título, status, detalhe e `traceId`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma pessoa desenvolvedora coloca a API em execução, a partir de um clone
  limpo e seguindo apenas a documentação do projeto (`docs/desenvolvimento.md`), em até 10 minutos (desconsiderando downloads).
- **SC-002**: 100% dos endpoints publicados aparecem na documentação interativa e podem ser
  executados a partir dela.
- **SC-003**: Uma requisição feita à API tem trace, logs correlacionados e métricas
  visíveis no dashboard em até 10 segundos.
- **SC-004**: 100% das respostas de erro observadas nos testes estão no formato padronizado
  com `traceId`, e nenhuma resposta 5xx contém stack trace ou mensagem de exceção.
- **SC-005**: Uma violação de camada introduzida intencionalmente é detectada pelos testes
  automatizados em 100% das tentativas.
- **SC-006**: Com o coletor fora do ar, 100% das requisições continuam sendo atendidas.

## Assumptions

- O ambiente local da spec 001 (PostgreSQL, RabbitMQ e Aspire Dashboard) é a base de
  execução; nesta spec a API ainda não usa banco de dados nem mensageria.
- Autenticação e autorização (ASP.NET Core Identity + JWT) ficam para uma spec própria; o
  endpoint de saúde e a documentação (em ambientes não produtivos) são públicos.
- Não há endpoints de negócio; os únicos endpoints publicados são os de saúde e
  documentação. Um eventual endpoint usado apenas para testar o tratamento de falhas não é
  publicado no contrato.
- Versionamento de rotas de negócio (por exemplo `/v1/...`) será decidido pela primeira
  spec de negócio; o `v1` em `/scalar/v1` identifica o documento OpenAPI.
- A API não faz chamadas HTTP de saída nesta spec; a verificação de spans de saída
  (FR-007) acontece na primeira spec que integrar um serviço externo.
- Produção, deploy, TLS e autenticação da documentação estão fora de escopo.
- O frontend não faz parte desta spec.
