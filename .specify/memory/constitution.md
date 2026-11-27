# Constituição do projeto Bank

## Core Principles

### I. Specification-First (SDD) — NÃO NEGOCIÁVEL

- Nenhum código de funcionalidade ou de infraestrutura com comportamento em execução pode
  ser escrito sem uma especificação aprovada em `specs/NNN-nome-curto/`.
- O ciclo obrigatório é `/speckit-specify` → (`/speckit-clarify`) → `/speckit-plan` →
  `/speckit-tasks` → `/speckit-implement`. O `plan.md` MUST passar pelo
  "Constitution Check" antes da implementação.
- `specs/` é documentação viva: quando a implementação diverge da spec, a spec, o plano e
  os contratos MUST ser atualizados no mesmo pull request que altera o código.
- Toda spec MUST estar vinculada a uma issue do GitHub.

Racional: a especificação é a fonte da verdade; o código é a sua materialização
rastreável.

### II. Isolamento da Camada de Aplicação

- Endpoints Minimal API são exclusivamente camada de transporte: recebem e validam o
  formato da requisição, chamam um serviço de aplicação e traduzem o resultado em resposta
  HTTP.
- Endpoints MUST NOT injetar, referenciar ou usar `DbContext`, `DbSet`, repositórios,
  clientes de mensageria ou qualquer tipo da camada de infraestrutura.
- Endpoints MUST NOT conter regras de negócio. Orquestração de casos de uso fica na camada
  de Application (serviços); invariantes e regras ficam no Domain.
- Direção das dependências: `Api → Application → Domain`; `Infrastructure` implementa
  abstrações declaradas em `Application`/`Domain`. `Domain` MUST NOT depender de nenhuma
  outra camada nem de pacotes de infraestrutura.
- Essas regras MUST ser verificadas por testes de arquitetura automatizados que falham o
  build quando violadas.
- As camadas valem dentro de cada módulo (Princípio VIII): o `Domain`, a `Application` e a
  `Infrastructure` de um módulo seguem a mesma direção de dependências. `Bank.Api` e
  `Bank.Worker` são as raízes de composição: registram os módulos e expõem os endpoints e
  consumidores, sem conhecer os tipos internos deles.

Racional: manter regras de negócio testáveis isoladamente e impedir que detalhes de
persistência vazem para o contrato HTTP. A separação por camadas organiza o código de um
contexto; a separação entre contextos é tratada pelo Princípio VIII.

### III. Erros Padronizados (ProblemDetails / RFC 7807)

- Toda resposta de erro (4xx e 5xx) MUST usar o corpo `ProblemDetails` com
  `Content-Type: application/problem+json`, conforme RFC 7807 (e compatível com sua
  sucessora, RFC 9457).
- Falhas de validação de entrada MUST retornar HTTP 400 com `ValidationProblemDetails`,
  incluindo o dicionário `errors` por campo.
- Violações de regra de negócio MUST retornar um status semântico (por exemplo 404, 409 ou
  422) com um `type` estável e documentado que identifique o erro; consumidores dependem
  de `type`, nunca do texto de `detail`.
- Toda resposta de erro MUST incluir a extensão `traceId` para correlação com a
  telemetria.
- Erros 5xx MUST NOT expor stack trace, mensagens de exceção, SQL ou dados sensíveis.
  Exceções não tratadas são convertidas por um handler global.
- Os serviços de aplicação comunicam falhas esperadas por resultados tipados; exceções
  ficam reservadas a falhas inesperadas.

Racional: um único formato de erro, previsível e documentado, para todos os consumidores
(frontend, integrações e testes).

### IV. Integridade Financeira

- Valores monetários MUST usar `decimal` no C# e `numeric` com precisão e escala
  explícitas no PostgreSQL; `float`/`double` são proibidos para dinheiro. Todo valor
  monetário carrega sua moeda (ISO 4217).
- Operações que movimentam saldo MUST ser atômicas (uma única transação) e idempotentes:
  comandos expostos via API aceitam uma chave de idempotência (`Idempotency-Key`), e
  repetições devolvem o resultado original sem duplicar efeitos.
- Lançamentos financeiros são imutáveis: correções e estornos são novos lançamentos
  compensatórios; registros existentes MUST NOT ser alterados nem apagados.
- Atualizações concorrentes do mesmo saldo MUST usar controle de concorrência
  (otimista ou bloqueio explícito) definido no plano da spec.
- Eventos derivados de uma operação financeira MUST ser publicados de forma consistente
  com a transação relacional (Transactional Outbox ou equivalente).
- Quando uma operação financeira atravessa módulos (por exemplo, pagar uma cobrança debita
  em Contas e liquida em Cobranças), a atomicidade MUST ser mantida por chamada síncrona ao
  contrato público do outro módulo dentro da mesma transação. Sagas e compensações MUST
  NOT substituir a transação para movimentar saldo enquanto os módulos compartilharem o
  banco.

Racional: em um sistema bancário, perda de precisão, duplicidade ou histórico mutável são
defeitos de correção, não de qualidade. A modularização não pode trocar um commit ACID por
estados intermediários justamente onde o dinheiro se move.

### V. Contratos Explícitos e Versionados

- A API HTTP MUST ter documento OpenAPI gerado nativamente pelo .NET e publicado em
  ambientes não produtivos com a UI do Scalar (`/scalar/v1`).
- Contratos HTTP e de mensageria são contratos de negócio: toda alteração MUST preservar
  compatibilidade retroativa ou introduzir uma nova versão explícita; consumidores não
  podem ser quebrados silenciosamente.
- Eventos de mensageria são nomeados no passado (por exemplo `transferencia.concluida`) e
  carregam versão de schema.
- Os contratos de cada spec ficam em `specs/NNN-nome-curto/contracts/`.
- O contrato público de um módulo (Princípio VIII) é um contrato interno ao processo: pode
  mudar sem versão nova, desde que todos os módulos consumidores sejam ajustados no mesmo
  pull request e as specs afetadas registrem a mudança. Eventos de domínio trocados entre
  módulos pelo RabbitMQ seguem as regras de mensageria acima.

Racional: frontend, backend e integrações evoluem de forma independente apenas se os
contratos forem estáveis e explícitos. Entre módulos do mesmo processo, o compilador já
impede a quebra silenciosa; versionar seria custo sem benefício.

### VI. Observabilidade Nativa

- Toda aplicação MUST emitir traces, métricas e logs via OpenTelemetry, exportando por
  OTLP para o coletor do ambiente.
- Logs MUST ser estruturados (templates com propriedades nomeadas, nunca concatenação de
  strings) e correlacionados ao trace ativo.
- Dados sensíveis (documentos pessoais, números completos de conta ou cartão,
  credenciais, tokens) MUST NOT aparecer em logs, traces ou métricas; quando necessários
  para diagnóstico, são mascarados.
- Novos casos de uso relevantes MUST expor métricas de negócio ou de operação definidas no
  plano da spec.

Racional: operações financeiras exigem diagnóstico ponta a ponta sem violar a privacidade
dos clientes.

### VII. Qualidade Verificável

- Toda regra de domínio e todo serviço de aplicação MUST ter testes unitários.
- Todo endpoint MUST ter testes de integração cobrindo o caminho feliz e os erros
  documentados (incluindo o formato `ProblemDetails`).
- Todo endpoint protegido MUST ter testes de integração para as respostas 401 (sem token
  ou token inválido) e 403 (token válido sem as claims ou policies exigidas).
- Testes de integração usam dependências reais em contêiner (Testcontainers para
  PostgreSQL e RabbitMQ); bancos em memória não substituem o PostgreSQL.
- Contratos de mensageria MUST ter testes de serialização e conformidade de schema.
- O pipeline de CI MUST executar build, testes e testes de arquitetura; um PR com falha
  não pode ser mesclado.
- As regras do Princípio VIII MUST ser testes de arquitetura (ArchUnitNET). Violações já
  existentes ficam numa lista de exceções conhecidas, versionada junto aos testes, com o
  motivo e a issue que as remove. A lista só diminui: um PR MUST NOT acrescentar exceções.

Racional: a qualidade é demonstrada por verificações automatizadas, não presumida pela
revisão manual. A lista de exceções torna a dívida da migração visível e impede que ela
cresça.

### VIII. Módulos com Fronteiras Explícitas

- O backend é um monólito modular: cada contexto de negócio é um módulo com `Domain`,
  `Application` e `Infrastructure` próprios e um contrato público (interfaces, DTOs e
  eventos que outros módulos podem usar). Todo o resto do módulo é `internal`; testes do
  próprio módulo podem acessar o interno.
- Um módulo MUST referenciar de outro módulo apenas o contrato público. Referência a tipos
  de `Domain`, `Application` ou `Infrastructure` de outro módulo é proibida.
- Cada módulo é dono dos próprios dados: um schema próprio no PostgreSQL, com mapeamento e
  migrations na `Infrastructure` do módulo. Tabelas de outro módulo MUST NOT ser lidas nem
  escritas diretamente; entre módulos não há chave estrangeira, join nem navegação do EF
  Core. Referências a dados de outro módulo guardam apenas o identificador tipado.
- A comunicação entre módulos é feita por:
  - chamada síncrona ao contrato público, inclusive na mesma transação quando o
    Princípio IV exigir atomicidade;
  - eventos de domínio, para efeitos que podem acontecer depois (notificação, auditoria),
    publicados pelo outbox.
- O Shared Kernel é mínimo: dinheiro, identificadores tipados, calendário e as abstrações
  técnicas comuns (por exemplo resultado tipado e unidade de trabalho). Ele MUST NOT
  conter regras de um contexto específico. Incluir algo nele exige justificativa no
  `plan.md` da spec.
- Um módulo existe mesmo que fique pequeno: o critério é o contexto de negócio, não o
  tamanho.

Racional: as camadas sozinhas não verificam fronteiras entre contextos. Hoje `Domain` e
`Application` reúnem todos os contextos separados só por pasta, e `lancamentos` tem
chave estrangeira para transferências, faturas e recebíveis. Com fronteiras explícitas,
cada contexto pode ser entendido, testado e, se um dia for necessário, extraído como
serviço sem reescrita.

## Restrições Técnicas e Estrutura do Repositório

Estrutura obrigatória do repositório:

```text
/
├── CONSTITUTION.md        # ponteiro para esta constituição
├── specs/                 # documentação viva: uma pasta por spec (NNN-nome-curto/)
├── src/
│   ├── backend/           # solução .NET 10
│   │   ├── src/
│   │   │   ├── Bank.Api/        # raiz de composição HTTP (endpoints)
│   │   │   ├── Bank.Worker/     # raiz de composição de processos em segundo plano
│   │   │   ├── Modulos/<Nome>/  # um módulo por contexto (Domain, Application,
│   │   │   │                    # Infrastructure e contrato público)
│   │   │   └── SharedKernel/    # núcleo compartilhado mínimo (Princípio VIII)
│   │   └── tests/               # unitários, integração e arquitetura
│   └── frontend/          # aplicação React
└── .specify/              # configuração do Spec Kit (constituição, templates, scripts)
```

### Arquitetura do Backend: Monólito Modular

Decisão: o backend é um monólito modular (Princípio VIII), implantado como dois processos
(API e Worker) sobre um único PostgreSQL.

Por que não microsserviços agora:

- Uma transferência debita, credita, grava o lançamento e o evento do outbox num único
  commit ACID. Com serviços e bancos separados, esse commit vira uma saga (compensações,
  estados intermediários e reconciliação) justamente nas operações financeiras.
- Não há motivo concreto de escala nem de deploy independente que pague esse custo.

Por que não manter só as camadas:

- As fronteiras entre contextos não são verificadas: qualquer caso de uso alcança
  qualquer entidade, e o acoplamento cresce sem aparecer em revisão.
- `lancamentos` já conhece todos os outros contextos por chave estrangeira, o que inverte
  a dependência e impede separar os dados depois.

Objetivo declarado: mostrar um caminho possível de migração para microsserviços. Com
contextos isolados em código, dados e comunicação, extrair um módulo passa a ser uma
mudança de implantação (trocar a chamada síncrona por um contrato remoto e a transação
compartilhada por uma saga), não uma reescrita. A extração em si está fora do escopo desta
emenda.

Período de transição: a migração é incremental. Os projetos atuais
(`Bank.Domain`, `Bank.Application` e `Bank.Infrastructure`) formam o núcleo antigo e
convivem com os módulos até serem esvaziados; cada etapa entra em `main` por pull request
com o CI verde. Código novo de um contexto que já tem módulo MUST ficar no módulo; o
núcleo antigo só recebe correções. As violações do Princípio VIII que existirem durante a
transição ficam na lista de exceções conhecidas (Princípio VII).

Stack:

- Backend: .NET 10, C#, Minimal APIs, com o código e os testes sob `src/backend/`.
- Persistência: PostgreSQL via Entity Framework Core (provedor Npgsql); o schema evolui
  exclusivamente por migrations versionadas.
- SQL puro: consultas e comandos de produção usam LINQ/EF Core por padrão. Quando for
  necessário SQL puro (consulta nativa, stored procedure, function ou view), ele MUST ser
  escrito primeiro em um arquivo `.sql` próprio, versionado na pasta `Persistencia/Sql/` da
  `Infrastructure` do módulo dono dos dados (no núcleo antigo,
  `src/backend/src/Bank.Infrastructure/Persistencia/Sql/`), e MUST NOT aparecer como string
  no código C#. O SQL de um módulo só toca o schema desse módulo:
  - `Sql/Procedures/` — procedures, functions e views, criadas e alteradas exclusivamente
    por migrations que leem o arquivo `.sql` correspondente;
  - `Sql/Consultas/` — consultas nativas executadas em tempo de execução, carregadas como
    recurso embutido do assembly da Infrastructure correspondente.
  Cada arquivo tem um nome que descreve a operação e um comentário de cabeçalho com a spec
  de origem. Fora do escopo: expressões declaradas no mapeamento do EF Core (por exemplo,
  `HasCheckConstraint`) e SQL usado apenas para preparar ou verificar dados em testes.
- Autenticação e autorização: ASP.NET Core Identity e JWT Bearer, com validação de claims
  e policies.
- Mensageria: RabbitMQ por meio do `RabbitMQ.Client` nativo; frameworks de abstração como
  MassTransit e Wolverine não são permitidos.
- O endpoint Minimal API atua exclusivamente como camada de roteamento e transporte. Ele
  não acessa o banco diretamente nem contém regras de negócio.
- Regras de negócio residem em serviços de aplicação ou no domínio.
- Identificadores tipados: entidades e raízes de agregado do domínio MUST ter
  identificadores tipados (`readonly record struct` que implementa
  `IIdentificador<TSelf>`), nunca `Guid` cru. A regra vale também para referências entre
  agregados (por exemplo `Carteira.UsuarioId`) e para as assinaturas das abstrações e dos
  casos de uso da Application. Novos identificadores são gerados como UUIDv7 por
  `Novo(DateTimeOffset)`.
  - Nas fronteiras (contratos HTTP, eventos de mensageria e SQL puro), o identificador é
    serializado como `Guid`/UUID; os contratos externos não expõem os tipos do domínio.
  - A conversão para o EF Core fica na Infrastructure (`IdentificadorConverter`); o
    `Domain` não depende de conversores nem de pacotes de persistência.
  - Value objects sem identidade própria não têm identificador.
  - A regra MUST ser verificada por teste de arquitetura.
- Documentação de API: OpenAPI nativo do .NET + Scalar.
- Observabilidade: OpenTelemetry (traces, métricas e logs) exportando via OTLP.
- Frontend: React com TypeScript, sob `src/frontend/`. Toda tela MUST ter versão desktop e
  mobile (layout responsivo) especificada e validada.
- Ambiente local: Docker Compose com healthchecks para toda a infraestrutura.
- Toda nova dependência (NuGet ou npm) MUST ter justificativa escrita no `plan.md` da spec
  que a introduz.
- Segredos MUST NOT ser versionados; configuração sensível vem de variáveis de ambiente
  ou user-secrets.

Convenções:

- Specs, planos, documentação e mensagens de erro voltadas ao usuário são escritos em
  português.
- Conceitos de domínio usam a linguagem ubíqua em português (por exemplo `Conta`,
  `Transferencia`, `Lancamento`); termos técnicos de framework mantêm o nome original
  (`Service`, `Repository`, `Endpoint`).

## Fluxo de Desenvolvimento e Branching

- `main` é protegida e sempre deve estar em estado implantável. Commits diretos em `main`
  são proibidos; toda mudança entra por pull request.
- **Uma branch por spec**: o nome da branch MUST ser idêntico ao nome da pasta da spec,
  no formato `NNN-nome-curto` (numeração sequencial do Spec Kit), por exemplo a branch
  `003-setup-api` para `specs/003-setup-api/`. A branch é criada a partir de `main`
  atualizada, antes de `/speckit-specify`.
- Todos os artefatos da spec (`spec.md`, `plan.md`, `tasks.md`, `contracts/`) e toda a sua
  implementação ficam na mesma branch e chegam a `main` no mesmo pull request.
- Mudanças sem spec (emendas desta constituição, documentação, CI, manutenção) usam a
  branch `<tipo>/<issue>-nome-curto`, por exemplo `docs/1-constituicao`.
- Commits seguem Conventional Commits: `tipo(escopo): descrição`, com
  `tipo` em `feat`, `fix`, `docs`, `test`, `refactor`, `build`, `ci` ou `chore`. Como o
  histórico da branch é preservado em `main`, todo commit da branch MUST seguir esse
  formato e representar um passo coerente; commits de rascunho são reorganizados antes
  do merge.
- O pull request MUST referenciar a issue (`Closes #N`), passar no CI e ser revisado
  quanto à conformidade com esta constituição. O merge é feito por merge commit
  (sem squash nem rebase), preservando o histórico da spec, e a branch é removida após o
  merge.

## Governance

Esta constituição prevalece sobre práticas, planos e decisões conflitantes. O texto
canônico é `.specify/memory/constitution.md`; o arquivo `CONSTITUTION.md` na raiz apenas
aponta para ele.

Emendas exigem uma proposta explícita (issue), a descrição do impacto e a atualização
deste arquivo em branch e pull request próprios, separados de commits de implementação.
O versionamento segue Semantic Versioning: MAJOR para remoção ou redefinição incompatível
de princípios; MINOR para novos princípios ou seções e expansões normativas relevantes;
PATCH para esclarecimentos e correções de redação.

Cada pull request MUST ser avaliado quanto à conformidade com esta constituição.
Violações são corrigidas antes do merge ou justificadas explicitamente na seção
"Complexity Tracking" do `plan.md`, com a alternativa mais simples rejeitada e o motivo.
A constituição é revisada sempre que arquitetura, contratos, segurança ou plataforma
mudarem materialmente.

**Version**: 1.3.0 | **Ratified**: 2026-11-16 | **Last Amended**: 2026-11-27
