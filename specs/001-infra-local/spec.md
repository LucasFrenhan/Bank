# Feature Specification: Ambiente de Infraestrutura Local

**Feature Branch**: `001-infra-local`

**Created**: 2026-11-16

**Status**: Draft

**Input**: Issue #2 — "Configurar docker-compose.yml com PostgreSQL, RabbitMQ (com plugin de
Management), e um coletor de observabilidade (ex: Aspire Dashboard ou Jaeger + Prometheus)
para receber dados do OpenTelemetry. Entregável: Ambiente subindo com docker compose up e
healthchecks configurados."

## User Scenarios & Testing *(mandatory)*

Os usuários desta funcionalidade são as pessoas desenvolvedoras do projeto Bank, que
precisam de toda a infraestrutura de apoio (banco de dados, mensageria e observabilidade)
rodando na própria máquina para desenvolver e testar o backend.

### User Story 1 - Subir toda a infraestrutura com um único comando (Priority: P1)

Uma pessoa desenvolvedora acabou de clonar o repositório. Com apenas o runtime de
contêineres instalado, ela executa um único comando na raiz do projeto e obtém o banco de
dados relacional e o message broker em execução, prontos para receber conexões.

**Why this priority**: sem banco de dados e mensageria nenhuma funcionalidade do backend
pode ser desenvolvida ou testada localmente; é a base de todas as specs seguintes.

**Independent Test**: em uma máquina limpa, clonar o repositório, executar o comando de
subida e verificar que o banco aceita conexões e que o broker aceita conexões e publica
mensagens, sem nenhum passo manual adicional.

**Acceptance Scenarios**:

1. **Given** um clone novo do repositório e o runtime de contêineres instalado, **When** a
   pessoa executa o comando de subida, **Then** o banco de dados e o message broker ficam
   em execução e acessíveis em portas documentadas.
2. **Given** o ambiente em execução, **When** a pessoa conecta ao banco com as credenciais
   documentadas, **Then** a conexão é aceita e o banco de dados do projeto já existe.
3. **Given** o ambiente em execução, **When** a pessoa acessa a interface de gerenciamento
   do broker com as credenciais documentadas, **Then** consegue visualizar filas,
   exchanges e conexões.

---

### User Story 2 - Saber quando o ambiente está realmente pronto (Priority: P2)

A pessoa desenvolvedora (ou um script, ou a futura API) precisa saber se cada serviço de
infraestrutura está de fato pronto para uso, e não apenas iniciado, para evitar falhas de
conexão na inicialização.

**Why this priority**: serviços que "subiram" mas ainda não aceitam conexões causam erros
intermitentes difíceis de diagnosticar; a verificação de saúde é parte explícita do
entregável da issue.

**Independent Test**: subir o ambiente e consultar o estado de cada serviço; todos devem
reportar "saudável" somente quando aceitam conexões. Parar o processo interno de um
serviço e verificar que ele passa a reportar "não saudável".

**Acceptance Scenarios**:

1. **Given** o comando de subida acabou de ser executado, **When** a pessoa consulta o
   estado dos serviços, **Then** cada serviço reporta "iniciando" até estar pronto e
   "saudável" quando aceita conexões.
2. **Given** um serviço que depende de outro, **When** o ambiente sobe, **Then** ele só é
   iniciado depois que suas dependências reportam "saudável".
3. **Given** um serviço que deixa de responder, **When** a verificação de saúde é
   executada, **Then** o serviço passa a reportar "não saudável".

---

### User Story 3 - Visualizar a telemetria das aplicações (Priority: P3)

A pessoa desenvolvedora executa uma aplicação local que emite traces, métricas e logs no
padrão OpenTelemetry e consegue visualizá-los em uma interface web local, para diagnosticar
o comportamento do sistema.

**Why this priority**: a constituição exige observabilidade nativa (Princípio VI); o
coletor precisa existir antes do setup da API (issue #3), que enviará a telemetria. Porém
é possível desenvolver contra banco e broker sem ele.

**Independent Test**: com o ambiente em execução, enviar um trace, uma métrica e um log de
teste ao endpoint de recebimento documentado e localizar cada um na interface web.

**Acceptance Scenarios**:

1. **Given** o ambiente em execução, **When** uma aplicação envia traces ao endpoint de
   telemetria documentado, **Then** os traces aparecem na interface web.
2. **Given** o ambiente em execução, **When** uma aplicação envia métricas e logs ao mesmo
   endpoint, **Then** métricas e logs aparecem na interface web, correlacionáveis ao trace.

---

### User Story 4 - Parar, reiniciar e reiniciar do zero (Priority: P3)

A pessoa desenvolvedora para o ambiente ao fim do dia e o retoma depois sem perder os
dados, ou opta por descartar todos os dados e recomeçar de um estado limpo.

**Why this priority**: preservar dados entre sessões evita retrabalho; poder zerar o
ambiente é essencial para reproduzir cenários e testar migrations do zero.

**Independent Test**: criar um dado no banco e uma fila no broker, parar e subir o
ambiente, verificar que ambos persistem; executar o procedimento de reset e verificar que
ambos deixam de existir.

**Acceptance Scenarios**:

1. **Given** dados gravados no banco e no broker, **When** o ambiente é parado e iniciado
   novamente, **Then** os dados continuam disponíveis.
2. **Given** dados gravados, **When** a pessoa executa o procedimento de reset documentado,
   **Then** o ambiente volta ao estado inicial, sem dados anteriores.

---

### Edge Cases

- Uma porta exigida já está ocupada por outro processo na máquina: a subida falha com
  mensagem que identifica o serviço e a porta, e a porta pode ser alterada por
  configuração local sem editar arquivos versionados.
- O arquivo de configuração local (credenciais e portas) não existe: o ambiente sobe com
  valores padrão de desenvolvimento documentados.
- Uma imagem de contêiner não pode ser baixada (sem rede): a falha indica qual serviço não
  pôde ser iniciado.
- O comando de subida é executado com o ambiente já em execução: nenhum dado é perdido e
  nenhum serviço é duplicado.
- O coletor de telemetria está fora do ar: banco e broker continuam operando normalmente
  (o coletor não é dependência deles).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: O ambiente MUST subir por inteiro com um único comando executado na raiz do
  repositório, sem passos manuais prévios além de ter o runtime de contêineres instalado.
- **FR-002**: O ambiente MUST disponibilizar o banco de dados relacional definido na
  constituição (PostgreSQL), com o banco de dados do projeto criado automaticamente na
  primeira subida.
- **FR-003**: O ambiente MUST disponibilizar o message broker definido na constituição
  (RabbitMQ), com a interface web de gerenciamento habilitada.
- **FR-004**: O ambiente MUST disponibilizar um coletor de telemetria que receba traces,
  métricas e logs no protocolo OTLP (gRPC e HTTP) e permita visualizar os três sinais em
  uma interface web local.
- **FR-005**: Todo serviço do ambiente MUST ter verificação de saúde que reporte "saudável"
  somente quando o serviço aceita conexões ou requisições.
- **FR-006**: Serviços com dependências MUST iniciar somente após suas dependências
  reportarem "saudável".
- **FR-007**: Os dados do banco e do broker MUST persistir entre paradas e reinícios do
  ambiente.
- **FR-008**: MUST existir um procedimento documentado para descartar todos os dados e
  recriar o ambiente do zero.
- **FR-009**: Credenciais, nomes e portas expostas MUST ser configuráveis por um arquivo de
  configuração local não versionado; o repositório MUST conter um arquivo de exemplo
  versionado com valores padrão de desenvolvimento.
- **FR-010**: Nenhum segredo real MUST ser versionado; os valores padrão do exemplo são
  exclusivamente para desenvolvimento local.
- **FR-011**: A documentação do projeto MUST descrever como subir, verificar a saúde,
  parar e resetar o ambiente, além de listar cada serviço com sua porta, URL de interface
  web e credenciais padrão.
- **FR-012**: As versões dos serviços MUST ser fixadas explicitamente (sem tags flutuantes
  como `latest`), para que todas as máquinas executem o mesmo ambiente.

### Key Entities

- **Serviço de infraestrutura**: componente executado no ambiente local (banco de dados,
  message broker, coletor de telemetria). Atributos: nome, versão fixada, portas expostas,
  verificação de saúde, dependências.
- **Configuração local**: conjunto de variáveis (credenciais, nomes, portas) lido na
  subida; possui um exemplo versionado e uma cópia local não versionada.
- **Volume de dados**: armazenamento persistente associado ao banco e ao broker, que
  sobrevive a reinícios e é descartado pelo procedimento de reset.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uma pessoa desenvolvedora sem conhecimento prévio do projeto coloca o
  ambiente em execução seguindo apenas a documentação em até 10 minutos (desconsiderando o
  tempo de download das imagens).
- **SC-002**: Com as imagens já baixadas, todos os serviços reportam "saudável" em até 60
  segundos após o comando de subida.
- **SC-003**: 100% dos serviços do ambiente possuem verificação de saúde.
- **SC-004**: Um trace, uma métrica e um log de teste enviados ao endpoint de telemetria
  ficam visíveis na interface web em até 10 segundos.
- **SC-005**: Dados criados antes de um reinício estão 100% disponíveis após o reinício; o
  procedimento de reset remove 100% deles.

## Assumptions

- As pessoas desenvolvedoras usam Windows, macOS ou Linux com Docker (Docker Desktop ou
  Docker Engine com o plugin Compose v2) instalado.
- PostgreSQL e RabbitMQ não são escolhas desta spec: são restrições já fixadas pela
  constituição. A escolha do coletor de telemetria (por exemplo Aspire Dashboard ou
  Jaeger + Prometheus) fica para o plano, desde que atenda ao FR-004 (traces, métricas e
  logs visíveis).
- A API (issue #3) e o frontend não fazem parte deste ambiente; eles serão adicionados ao
  Compose, ou executados fora dele, pelas suas próprias specs.
- O ambiente é exclusivamente para desenvolvimento local; produção, alta disponibilidade,
  TLS e backup estão fora de escopo.
- Os testes automatizados de integração usam seus próprios contêineres efêmeros
  (Testcontainers, conforme a constituição) e não dependem deste ambiente.
- Não há dados de semente (seed) de negócio; o schema será criado pelas migrations das
  specs futuras.
