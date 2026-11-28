# Feature Specification: Fundação do monólito modular (Shared Kernel e regras de fronteira)

**Feature Branch**: `017-modulos-fundacao`

**Created**: 2026-11-27

**Status**: Draft

**Input**: Issue #60 — "Monólito modular: fundação (Shared Kernel e regras de arquitetura por
módulo)". Preparar a base da modularização: o Shared Kernel e as regras de arquitetura por
módulo, ainda sem mover nenhum contexto de negócio. Depende da #59 (constituição v1.3.0,
Princípio VIII). Faz parte da #58.

## Contexto

O backend é hoje um monólito em camadas: os contextos de negócio (Identidade, Contas,
Transferências, Cartões, Cobranças, Notificações) convivem nos mesmos projetos, separados só
por pasta. A constituição v1.3.0 adotou o monólito modular (Princípio VIII) e exige que as
fronteiras entre módulos sejam verificadas automaticamente, com uma lista de exceções
conhecidas que só diminui (Princípio VII).

Esta spec não move nenhum contexto. Ela entrega o que todos os módulos vão usar: o núcleo
compartilhado, o formato de um módulo, a forma como os hosts (API e Worker) registram um
módulo e as verificações automáticas que impedem novas violações de fronteira. As pessoas
que usam o Bank (clientes PF e PJ) não percebem nenhuma diferença.

Os "usuários" desta spec são as pessoas que desenvolvem e revisam o backend.

## Clarifications

### Session 2026-11-27

- Q: Enquanto nenhum contexto virou módulo, o que entra na lista de exceções conhecidas? →
  A: Só violações das regras de módulo e do núcleo compartilhado; a lista começa vazia e,
  pela constituição (Princípio VII), nenhum PR acrescenta entradas (ajustado no plan). As
  dependências entre contextos do núcleo antigo não são verificadas (seguem mapeadas na
  #58).
- Q: A spec cria algum módulo real ou só o formato? → A: Só o formato, demonstrado por
  módulos de exemplo num projeto usado pelos testes; nenhum módulo real em `Modulos/`.
- Q: Os identificadores tipados de cada contexto vão para o núcleo compartilhado ou ficam
  com o contexto dono? → A: Ficam no contexto dono; o núcleo compartilhado recebe só a
  abstração de identificador e o identificador de evento. Na extração, cada identificador
  entra no contrato público do módulo dono.
- Q: Onde ficam as conversões de persistência dos identificadores tipados? → A: Numa parte
  de infraestrutura comum do núcleo compartilhado, separada da parte pura; a
  infraestrutura de cada módulo a usa, e o domínio e o núcleo puro seguem sem dependência
  de persistência.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Núcleo compartilhado separado, sem mudança de comportamento (Priority: P1)

Quem desenvolve encontra os conceitos comuns a todos os contextos (dinheiro, abstração de
identificador tipado, resultado e erro tipados, calendário bancário e entradas comuns como
paginação) num
núcleo compartilhado próprio, fora dos projetos de um contexto específico. Todo o resto do
sistema passa a usá-los a partir desse núcleo, e nada muda para quem usa o banco.

**Why this priority**: todos os módulos dependem do núcleo compartilhado; sem ele, o primeiro
módulo extraído teria que referenciar o núcleo antigo, o que a constituição proíbe.

**Independent Test**: rodar toda a suíte existente (unidade, integração, arquitetura e e2e)
depois da extração; os resultados são os mesmos de antes, e o núcleo compartilhado não
depende de nenhum contexto.

**Acceptance Scenarios**:

1. **Given** o backend com a extração concluída, **When** a suíte completa roda, **Then**
   todos os testes que passavam antes continuam passando, sem alterar asserções de
   comportamento.
2. **Given** o núcleo compartilhado, **When** a verificação de arquitetura roda, **Then** ele
   não depende de nenhum contexto de negócio, do núcleo antigo, dos hosts nem de
   frameworks de persistência, web ou mensageria.
3. **Given** o contrato HTTP publicado (documento OpenAPI), os eventos de mensageria e as
   migrations, **When** comparados com os da `main` antes da mudança, **Then** são
   idênticos.

---

### User Story 2 - Fronteiras entre módulos verificadas automaticamente (Priority: P1)

Quem desenvolve recebe uma falha de build, com mensagem clara, sempre que um código cruza uma
fronteira proibida pelo Princípio VIII: um módulo usando algo de outro módulo que não seja o
contrato público, um módulo usando a persistência (contexto de dados ou repositórios) de
outro, ou um módulo dependendo do núcleo antigo.

**Why this priority**: é a garantia que justifica a modularização; sem ela, as fronteiras
voltam a ser só convenção.

**Independent Test**: introduzir, num cenário de teste, cada tipo de violação e confirmar que
a verificação falha apontando a regra e o tipo infrator; remover a violação e confirmar que
volta a passar.

**Acceptance Scenarios**:

1. **Given** um módulo que referencia um tipo interno (domínio, aplicação ou infraestrutura)
   de outro módulo, **When** a verificação roda, **Then** ela falha e identifica os dois
   tipos.
2. **Given** um módulo que usa o contexto de dados ou um repositório de outro módulo,
   **When** a verificação roda, **Then** ela falha.
3. **Given** um módulo que depende do núcleo antigo, **When** a verificação roda, **Then**
   ela falha; **Given** o núcleo antigo dependendo do contrato público de um módulo,
   **Then** ela passa.
4. **Given** um módulo que referencia só o contrato público de outro, **When** a verificação
   roda, **Then** ela passa.

---

### User Story 3 - Lista de exceções conhecidas que só diminui (Priority: P2)

Quem revisa vê, num único lugar versionado, cada violação de fronteira que já existe, com o
motivo e a issue que vai removê-la. Uma violação nova quebra o build; uma exceção da lista
que deixou de acontecer também quebra o build até ser retirada da lista, para que a lista
nunca fique desatualizada nem cresça.

**Why this priority**: torna visível a dívida da migração e permite ligar as regras já,
mesmo com o código atual ainda acoplado.

**Independent Test**: com a lista atual (vazia, já que nenhum contexto é módulo ainda), a
verificação passa; com os módulos de exemplo, uma violação fora da lista faz falhar, e uma
entrada da lista sem violação correspondente também faz falhar.

**Acceptance Scenarios**:

1. **Given** a lista de exceções e o código atual, **When** a verificação roda, **Then** ela
   passa.
2. **Given** uma violação nova que não está na lista, **When** a verificação roda, **Then**
   ela falha e mostra a violação.
3. **Given** uma exceção listada que não acontece mais no código, **When** a verificação
   roda, **Then** ela falha pedindo a remoção da exceção da lista.
4. **Given** uma entrada da lista, **When** alguém a lê, **Then** encontra a regra violada,
   os tipos envolvidos, o motivo e a issue que a remove.

---

### User Story 4 - Formato de módulo e registro nos hosts (Priority: P2)

Quem for extrair o primeiro contexto (issues seguintes da #58) encontra definidos o formato
de um módulo (domínio, aplicação, infraestrutura e contrato público) e uma forma única de a
API e o Worker registrarem os serviços, os endpoints e os processos de um módulo, sem que os
hosts conheçam os tipos internos dele.

**Why this priority**: evita que cada extração invente seu próprio formato; depende das
regras (US2) para ser verificável.

**Independent Test**: módulos de exemplo, num projeto usado só pelos testes, seguem o
formato, são registrados por um host de teste pelo ponto de entrada único e passam nas
regras da US2; nenhum módulo real é criado nesta spec.

**Acceptance Scenarios**:

1. **Given** o formato de módulo documentado, **When** um módulo segue esse formato, **Then**
   ele é registrado na API e no Worker por um ponto de entrada único por host, e as regras
   da US2 se aplicam a ele sem configuração extra.
2. **Given** um host que registra um módulo, **When** a verificação de arquitetura roda,
   **Then** o host só usa o ponto de registro e o contrato público do módulo.

---

### Edge Cases

- A verificação roda sem nenhum módulo real: as regras não podem passar "por vazio"; deve
  haver prova de que cada regra detecta a violação que descreve.
- Um tipo do núcleo compartilhado precisa de algo de um contexto: não entra no núcleo; fica
  no contexto e o motivo é registrado.
- Um conceito usado por só dois contextos (por exemplo, uma regra de cartão usada por
  cobranças): não é comum; fica no contexto dono e o outro usa o contrato público.
- Conversões de persistência dos identificadores tipados: ficam na infraestrutura comum do
  núcleo compartilhado, nunca na parte pura nem no domínio (constituição, Restrições
  Técnicas).
- Nomes de tipos que mudam de lugar aparecem em dados persistidos ou em mensagens (por
  exemplo, nome do tipo serializado): o formato externo não pode mudar.
- Uma regra de fronteira com falso positivo: a própria regra é corrigida, com um exemplo de
  teste cobrindo o caso; nunca vira exceção conhecida nem desliga a regra (constituição,
  Princípio VII).

## Requirements *(mandatory)*

### Functional Requirements

**Núcleo compartilhado (RN-01)**

- **FR-001**: O sistema MUST ter um núcleo compartilhado próprio, separado dos contextos de
  negócio, contendo dinheiro, a abstração de identificador tipado e o identificador de
  evento, resultado e erro tipados, calendário bancário e as entradas comuns da aplicação
  (paginação, leitura de chave de idempotência, identificadores e filtros). Os
  identificadores tipados de cada contexto (usuário, carteira, cartão, cobrança etc.) MUST
  permanecer no contexto dono; na extração de um módulo, eles passam a fazer parte do
  contrato público desse módulo. A abstração de unidade de trabalho fica no núcleo antigo
  nesta spec, porque o resultado dela enumera duplicidades de contextos específicos
  (cartão, cobrança); ela será generalizada e movida quando o primeiro caso de uso entre
  módulos precisar dela (research R2).
- **FR-002**: O núcleo compartilhado MUST NOT depender de nenhum contexto de negócio, do
  núcleo antigo nem dos hosts. Ele tem duas partes: a parte pura (FR-001), sem frameworks
  de persistência, web, mensageria ou telemetria; e a infraestrutura comum, que contém só
  as conversões de persistência dos identificadores tipados e depende apenas da parte pura
  e do framework de persistência.
- **FR-002a**: A infraestrutura de um módulo e o núcleo antigo MUST usar as conversões da
  infraestrutura comum do núcleo compartilhado; domínio e aplicação MUST NOT referenciá-la.
- **FR-003**: Todo código que usava esses conceitos MUST passar a usá-los a partir do núcleo
  compartilhado, sem cópias nem reexportações paralelas no núcleo antigo.
- **FR-004**: Só MUST entrar no núcleo compartilhado o que é usado por todos ou quase todos
  os contextos e não contém regra de um contexto; cada item que entra tem justificativa
  registrada no plano.

**Formato de módulo e registro (RN-02)**

- **FR-005**: O formato de um módulo MUST ser definido e documentado: domínio, aplicação,
  infraestrutura e contrato público, sob `Modulos/<Nome>`, com tudo fora do contrato
  público inacessível a outros módulos.
- **FR-006**: A API e o Worker MUST registrar cada módulo por um ponto de entrada único por
  host (serviços; endpoints na API; processos em segundo plano e consumidores no Worker),
  sem referenciar tipos internos do módulo.
- **FR-007**: O registro de módulos MUST conviver com o registro atual do núcleo antigo
  durante a transição, sem alterar a ordem nem o comportamento do que já existe.
- **FR-008**: Esta spec MUST NOT criar módulo real em `Modulos/`. O formato e o registro
  nos hosts MUST ser demonstrados por módulos de exemplo num projeto usado só pelos testes,
  os mesmos que provam a detecção de cada regra (FR-014); o primeiro módulo real nasce na
  extração seguinte da #58.

**Regras de fronteira (RN-03)**

- **FR-009**: A verificação de arquitetura MUST falhar quando um módulo referenciar de outro
  módulo qualquer tipo fora do contrato público.
- **FR-010**: A verificação MUST falhar quando um módulo usar o contexto de dados, os
  repositórios ou o mapeamento de persistência de outro módulo.
- **FR-011**: A verificação MUST falhar quando um módulo depender do núcleo antigo; o núcleo
  antigo PODE depender do contrato público de um módulo.
- **FR-012**: A verificação MUST falhar quando o núcleo compartilhado depender de qualquer
  módulo ou do núcleo antigo.
- **FR-013**: As regras atuais de camadas (Princípio II) MUST continuar valendo e passar a
  valer também dentro de cada módulo.
- **FR-014**: Cada regra MUST ter prova automatizada de que detecta a violação que descreve
  (não pode passar por ausência de tipos).
- **FR-015**: A mensagem de falha MUST identificar a regra, o tipo de origem e o tipo de
  destino.

**Exceções conhecidas (RN-04)**

- **FR-016**: As violações de fronteira existentes MUST ficar numa lista explícita e
  versionada junto aos testes, cada entrada com regra, tipos envolvidos, motivo e issue que
  a remove.
- **FR-017**: As regras de fronteira valem para os módulos (`Modulos/`) e para o núcleo
  compartilhado; as dependências entre contextos dentro do núcleo antigo não são
  verificadas nem listadas como exceção (continuam mapeadas na #58). Como nenhum contexto é
  módulo ainda, a lista MUST começar vazia. Conforme a constituição (Princípio VII: "um PR
  MUST NOT acrescentar exceções"), nenhum PR acrescenta entradas: cada extração resolve no
  próprio PR as violações que revelar. O mecanismo (FR-016, FR-018, FR-019) fica pronto e
  verificado mesmo com a lista vazia.
- **FR-018**: Uma violação fora da lista MUST quebrar o build.
- **FR-019**: Uma entrada da lista que não corresponda mais a uma violação real MUST quebrar
  o build até ser removida, para que a lista só diminua.
- **FR-020**: A lista MUST ser documentada (onde fica, como ler uma entrada, como remover) no
  guia de desenvolvimento.

**Sem mudança de comportamento (RN-05)**

- **FR-021**: O contrato HTTP (documento OpenAPI), os contratos e a serialização dos eventos
  de mensageria, as migrations e o schema do banco MUST permanecer idênticos.
- **FR-022**: Os testes existentes de unidade, integração, arquitetura e e2e MUST continuar
  passando; mudanças neles se limitam a referências (namespaces e projetos), sem alterar
  asserções de comportamento.

### Key Entities *(include if feature involves data)*

Não há entidades de negócio novas nem alteração de dados. Os elementos desta spec são
estruturais:

- **Núcleo compartilhado** (Shared Kernel): conceitos comuns a todos os contextos, sem
  regra de um contexto específico.
- **Módulo**: um contexto de negócio com domínio, aplicação, infraestrutura e contrato
  público; dono dos próprios dados.
- **Contrato público de módulo**: o que outros módulos e os hosts podem usar (interfaces,
  DTOs, eventos e o ponto de registro).
- **Núcleo antigo**: os projetos em camadas atuais, que convivem com os módulos até serem
  esvaziados.
- **Exceção conhecida**: violação existente e aceita temporariamente; regra, tipos
  envolvidos, motivo e issue que a remove.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% dos testes que passavam antes da mudança continuam passando, sem
  alteração de asserções de comportamento.
- **SC-002**: O contrato HTTP publicado, os eventos e as migrations têm diferença zero em
  relação à `main` anterior à mudança.
- **SC-003**: Cada regra de fronteira tem ao menos um caso de violação comprovadamente
  detectado (100% das regras com prova de detecção).
- **SC-004**: Uma violação nova de fronteira é barrada no CI em 100% das tentativas; nenhuma
  entrada da lista de exceções fica sem violação correspondente.
- **SC-005**: Cada entrada da lista de exceções tem motivo e issue de remoção (100%).
- **SC-006**: Quem for extrair o primeiro contexto registra um módulo novo, em código, com uma
  linha no ponto de registro de cada host; as referências de build (projeto, solução e imagem)
  seguem o contrato do formato de módulo.

## Assumptions

- O formato de módulo e o nome do núcleo compartilhado seguem a constituição v1.3.0:
  `src/backend/src/Modulos/<Nome>` e `src/backend/src/SharedKernel/`.
- Resultado e erro tipados e as entradas comuns da aplicação entram no núcleo compartilhado
  como "abstrações técnicas comuns", como a constituição permite; a unidade de trabalho fica
  para depois (FR-001); as conversões de
  persistência dos identificadores ficam na infraestrutura comum do núcleo compartilhado
  (FR-002), fora do domínio.
- A ordem e o conteúdo dos módulos seguem a #58 (Identidade, Contas, Transferências,
  Cartões, Cobranças, Notificações); esta spec não move nenhum deles.
- A verificação usa a ferramenta de testes de arquitetura já adotada no projeto, que roda no
  CI junto com os demais testes.
- Não há mudança visível para clientes; frontend e e2e não mudam, mas o e2e roda no CI como
  prova de não regressão.
- Fora de escopo: mover qualquer contexto, separar schemas ou remover FKs (specs seguintes
  da #58), extrair serviços.
