---

description: "Lista de tarefas da feature 001-infra-local"
---

# Tasks: Ambiente de Infraestrutura Local

**Input**: Design documents from `specs/001-infra-local/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/ambiente-local.md](contracts/ambiente-local.md),
[quickstart.md](quickstart.md)

**Tests**: a spec não pede testes de código. A verificação automatizada definida no plano
(R8) é o workflow de CI que sobe o ambiente e exige todos os serviços `healthy`; os demais
cenários são validados pelo [quickstart.md](quickstart.md).

**Organization**: tarefas agrupadas por história de usuário da spec (US1–US4).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida (US1, US2, US3, US4)

## Path Conventions

Arquivos na raiz do repositório: `docker-compose.yml`, `.env.example`, `README.md` e
`.github/workflows/infra-local.yml` (ver [plan.md](plan.md#source-code-repository-root)).
Quase todas as tarefas editam o `docker-compose.yml`, portanto são sequenciais.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: arquivos base do ambiente

- [ ] T001 [P] Criar `.env.example` na raiz com todas as variáveis e padrões de
  [data-model.md](data-model.md#configuração-local): `POSTGRES_USER=bank`,
  `POSTGRES_PASSWORD=bank_dev_password`, `POSTGRES_DB=bank`, `POSTGRES_PORT=5432`,
  `RABBITMQ_USER=bank`, `RABBITMQ_PASSWORD=bank_dev_password`, `RABBITMQ_VHOST=bank`,
  `RABBITMQ_AMQP_PORT=5672`, `RABBITMQ_MANAGEMENT_PORT=15672`,
  `ASPIRE_DASHBOARD_PORT=18888`, `OTLP_GRPC_PORT=4317`, `OTLP_HTTP_PORT=4318`, com
  comentário de que os valores são "exclusivamente para desenvolvimento local" (FR-010)
- [ ] T002 [P] Criar `docker-compose.yml` na raiz com `name: bank`, os volumes nomeados
  de nível superior `postgres-data` e `rabbitmq-data`, e comentário de cabeçalho apontando
  para `specs/001-infra-local/contracts/ambiente-local.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: verificação automatizada que acompanha todas as histórias (Princípio VII, R8)

- [ ] T003 [P] Criar `.github/workflows/infra-local.yml`: gatilho em `pull_request` e
  `push` para `main` com filtro de paths (`docker-compose.yml`, `.env.example`,
  `.github/workflows/infra-local.yml`); passos `docker compose config -q`,
  `docker compose up -d --wait --wait-timeout 120`, `docker compose ps`; em falha,
  `docker compose logs`; sempre ao final, `docker compose down -v`

**Checkpoint**: arquivos base e CI prontos — as histórias podem começar

---

## Phase 3: User Story 1 - Subir toda a infraestrutura com um único comando (Priority: P1) 🎯 MVP

**Goal**: PostgreSQL e RabbitMQ (com Management) no ar com um único comando.

**Independent Test**: [quickstart.md — Cenário 1](quickstart.md#cenário-1--subida-com-um-comando-us1-fr-001003-sc-001-sc-002).

- [ ] T004 [US1] Adicionar o serviço `postgres` em `docker-compose.yml`: imagem
  `postgres:18.7-alpine` (tag fixa, FR-012); `POSTGRES_USER`, `POSTGRES_PASSWORD` e
  `POSTGRES_DB` lidos do `.env` com padrão (`${POSTGRES_USER:-bank}` etc.); porta
  `127.0.0.1:${POSTGRES_PORT:-5432}:5432`; volume `postgres-data:/var/lib/postgresql`
  (**não** `/var/lib/postgresql/data` — mudança do PostgreSQL 18, R5)
- [ ] T005 [US1] Adicionar o serviço `rabbitmq` em `docker-compose.yml`: imagem
  `rabbitmq:4.3.7-management-alpine`; `hostname: rabbitmq` fixo ("Obrigatório e fixo para
  o RabbitMQ", R5); `RABBITMQ_DEFAULT_USER`, `RABBITMQ_DEFAULT_PASS` e
  `RABBITMQ_DEFAULT_VHOST` a partir de `RABBITMQ_USER`, `RABBITMQ_PASSWORD` e
  `RABBITMQ_VHOST` com padrão; portas `127.0.0.1:${RABBITMQ_AMQP_PORT:-5672}:5672` e
  `127.0.0.1:${RABBITMQ_MANAGEMENT_PORT:-15672}:15672`; volume
  `rabbitmq-data:/var/lib/rabbitmq` (depende de T004: mesmo arquivo)
- [ ] T006 [US1] Executar o Cenário 1 do `quickstart.md` (sem `.env` e com `.env` copiado
  do exemplo) e confirmar banco `bank` e vhost `bank` acessíveis com as credenciais do
  contrato

**Checkpoint**: MVP — banco e broker utilizáveis para desenvolvimento

---

## Phase 4: User Story 2 - Saber quando o ambiente está realmente pronto (Priority: P2)

**Goal**: healthcheck em todo serviço e subida que só conclui com todos `healthy`.

**Independent Test**: [quickstart.md — Cenário 2](quickstart.md#cenário-2--saúde-dos-serviços-us2-fr-005-sc-003).

- [ ] T007 [US2] Adicionar healthcheck ao `postgres` em `docker-compose.yml`:
  `pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}`, `interval: 5s`, `timeout: 5s`,
  `retries: 12`, `start_period: 10s` (R3)
- [ ] T008 [US2] Adicionar healthcheck ao `rabbitmq` em `docker-compose.yml`:
  `rabbitmq-diagnostics -q check_port_connectivity`, `interval: 5s`, `timeout: 5s`,
  `retries: 12`, `start_period: 30s` (R3)
- [ ] T009 [US2] Executar o Cenário 2 do `quickstart.md` (incluindo `stop_app`/`start_app`
  do RabbitMQ) e medir o tempo de `docker compose up -d --wait` com imagens em cache
  (SC-002: ≤ 60s)

**Checkpoint**: US1 + US2 — `up --wait` confiável; o CI (T003) passa a ser significativo

---

## Phase 5: User Story 3 - Visualizar a telemetria das aplicações (Priority: P3)

**Goal**: Aspire Dashboard recebendo traces, métricas e logs via OTLP.

**Independent Test**: [quickstart.md — Cenário 3](quickstart.md#cenário-3--telemetria-us3-fr-004-sc-004).

- [ ] T010 [US3] Inspecionar a imagem `mcr.microsoft.com/dotnet/aspire-dashboard:13.6.5`
  para descobrir ferramentas disponíveis para healthcheck (`curl`, `wget`, `bash`, `sh`)
  e registrar o resultado em `specs/001-infra-local/research.md` (seção R3)
- [ ] T011 [US3] Adicionar o serviço `aspire-dashboard` em `docker-compose.yml`: imagem
  `mcr.microsoft.com/dotnet/aspire-dashboard:13.6.5`;
  `DASHBOARD__FRONTEND__AUTHMODE=Unsecured` e `DASHBOARD__OTLP__AUTHMODE=Unsecured`;
  portas `127.0.0.1:${ASPIRE_DASHBOARD_PORT:-18888}:18888`,
  `127.0.0.1:${OTLP_GRPC_PORT:-4317}:18889` e `127.0.0.1:${OTLP_HTTP_PORT:-4318}:18890`;
  sem volume e sem `depends_on` (R1, R4, R7)
- [ ] T012 [US3] Adicionar healthcheck ao `aspire-dashboard` em `docker-compose.yml`
  conforme o resultado de T010 (ordem de preferência do R3); se não houver ferramenta
  possível, registrar a solução adotada e a alternativa rejeitada em
  `specs/001-infra-local/plan.md` (Complexity Tracking)
- [ ] T013 [US3] Executar o Cenário 3 do `quickstart.md` para traces, logs e métricas via
  OTLP/HTTP e confirmar visualização na UI em ≤ 10s (SC-004)

**Checkpoint**: telemetria pronta para a API (issue #3)

---

## Phase 6: User Story 4 - Parar, reiniciar e reiniciar do zero (Priority: P3)

**Goal**: dados do banco e do broker persistem entre reinícios; reset os descarta.

**Independent Test**: [quickstart.md — Cenário 4](quickstart.md#cenário-4--persistência-e-reset-us4-fr-007-fr-008-sc-005).

- [ ] T014 [US4] Executar o Cenário 4 do `quickstart.md`: tabela e fila `smoke` sobrevivem
  a `docker compose down` + `up`, recriação de contêiner (`up -d --force-recreate --wait`)
  incluída, e desaparecem após `docker compose down -v`; corrigir montagens em
  `docker-compose.yml` se algum dado se perder

**Checkpoint**: todas as histórias funcionais e verificadas

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: documentação, casos de borda e entrega

- [ ] T015 [P] Adicionar a seção "Ambiente local" ao `README.md` (FR-011): pré-requisitos,
  `cp .env.example .env` (opcional), subir (`docker compose up -d --wait`), ver estado,
  parar, reset (`docker compose down -v`), tabela de serviços com portas, URLs e
  credenciais padrão, e link para `specs/001-infra-local/contracts/ambiente-local.md`;
  remover do status do README a menção de que as instruções da issue #2 estão pendentes
- [ ] T016 Executar o Cenário 5 do `quickstart.md` (sem `.env`, porta ocupada com
  sobrescrita no `.env`, subida repetida, dashboard parado)
- [ ] T017 Conferir que `docker compose config` não contém tags flutuantes nem `latest`
  (FR-012) e que `.env` não aparece em `git status` (FR-010); sincronizar
  `specs/001-infra-local/contracts/ambiente-local.md` e `research.md` com qualquer ajuste
  feito na implementação
- [ ] T018 Fazer push da branch `001-infra-local`, confirmar o workflow `infra-local` verde
  e abrir o PR para `main` com `Closes #2`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências
- **Foundational (Phase 2)**: pode rodar junto com a Phase 1; o workflow só passa quando
  US1 e US2 estiverem prontas
- **US1 (Phase 3)**: depende de T002
- **US2 (Phase 4)**: depende de US1 (adiciona healthcheck aos serviços criados na US1)
- **US3 (Phase 5)**: depende apenas de T002; independente de US1/US2 em comportamento,
  mas sequencial na edição do `docker-compose.yml`
- **US4 (Phase 6)**: depende de US1 (os volumes são definidos em T002/T004/T005)
- **Polish (Phase 7)**: depende de todas as histórias

### User Story Dependencies

```text
T001 ─┐
T002 ─┼─→ US1 (T004→T005→T006) ─→ US2 (T007→T008→T009) ─┐
T003 ─┘        │                                          ├─→ Polish (T015..T018)
               └─→ US4 (T014)                             │
T002 ──→ US3 (T010→T011→T012→T013) ───────────────────────┘
```

### Within Each User Story

- Edições no `docker-compose.yml` são sequenciais (mesmo arquivo)
- A tarefa de validação (quickstart) fecha cada história

### Parallel Opportunities

- T001, T002 e T003 (arquivos diferentes)
- T010 (inspeção da imagem, sem editar arquivos do repo) pode rodar durante a US1/US2
- T015 (README) pode ser escrito em paralelo a qualquer história, a partir do contrato

---

## Parallel Example: Setup + Foundational

```text
Task: "T001 Criar .env.example na raiz"
Task: "T002 Criar docker-compose.yml com name: bank e volumes"
Task: "T003 Criar .github/workflows/infra-local.yml"
```

## Parallel Example: User Story 3

```text
Task: "T010 Inspecionar a imagem do Aspire Dashboard"   # enquanto a US1/US2 é feita
Task: "T015 Seção Ambiente local no README.md"          # independente do compose
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 (T001–T002) e Phase 2 (T003)
2. US1 (T004–T006)
3. **Validar**: Cenário 1 do quickstart — já é possível desenvolver contra banco e broker

### Incremental Delivery

1. Setup + Foundational → base pronta
2. US1 → banco e broker no ar (MVP)
3. US2 → healthchecks; CI verde
4. US3 → telemetria pronta para a issue #3
5. US4 → persistência e reset comprovados
6. Polish → README, casos de borda e PR

### Commits

Cada fase fecha com um commit Conventional Commits na branch (o histórico é preservado no
merge commit), por exemplo:

- `build(infra-local): adiciona compose base e .env.example`
- `ci(infra-local): valida ambiente local no GitHub Actions`
- `build(infra-local): adiciona postgres e rabbitmq`
- `build(infra-local): adiciona healthchecks`
- `build(infra-local): adiciona aspire dashboard`
- `docs(infra-local): documenta ambiente local no README`

---

## Notes

- [P] = arquivos diferentes, sem dependências pendentes
- [USn] mapeia a tarefa à história da spec para rastreabilidade
- Marcar cada tarefa como `[x]` neste arquivo ao concluí-la
- Requer Docker Desktop em execução para T006, T009, T010, T013, T014 e T016
