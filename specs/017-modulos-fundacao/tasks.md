---

description: "Lista de tarefas da feature 017-modulos-fundacao"
---

# Tasks: Fundação do monólito modular (Shared Kernel e regras de fronteira)

**Input**: Design documents from `specs/017-modulos-fundacao/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII). As regras novas têm a prova de detecção escrita
antes do avaliador e falham primeiro (não compilam ou não detectam). A US1 é uma refatoração sem
comportamento novo: a prova é a suíte existente inalterada (FR-022) mais as verificações de contrato e
modelo (research R8).

**Organization**: a fase de setup cria os projetos do kernel. A US1 move os tipos comuns para o kernel,
as US2 e US3 criam as regras e a lista de exceções sobre módulos de exemplo, e a US4 define o registro nos
hosts.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

`back/` = `src/backend/`; `sk/` = `back/src/SharedKernel/Bank.SharedKernel/`,
`ski/` = `back/src/SharedKernel/Bank.SharedKernel.Infrastructure/`, `dom/` = `back/src/Bank.Domain/`,
`app/` = `back/src/Bank.Application/`, `infra/` = `back/src/Bank.Infrastructure/`,
`api/` = `back/src/Bank.Api/`, `wrk/` = `back/src/Bank.Worker/`, `ut/` = `back/tests/Bank.UnitTests/`,
`it/` = `back/tests/Bank.IntegrationTests/`, `arq/` = `back/tests/Bank.ArchitectureTests/`,
`ex/` = `back/tests/Bank.ArchitectureTests.Exemplos/`.

---

## Phase 1: Setup

**Purpose**: linha de base para provar "sem mudança" e os dois projetos do kernel.

- [X] T001 Registrar a linha de base na `main` atual: rodar `dotnet test --solution src/backend/Bank.slnx -c Release` e anotar a contagem por projeto (unidade, integração, arquitetura) no rascunho do PR; confirmar que `npm run api:tipos` não gera diff em `src/frontend/src/api/schema.d.ts` com o Compose atual (research R8)
- [X] T002 [P] Criar `sk/Bank.SharedKernel.csproj` sem nenhum `PackageReference` nem `ProjectReference` (herda `back/Directory.Build.props`), com `InternalsVisibleTo` para `Bank.UnitTests` (research R1)
- [X] T003 [P] Criar `ski/Bank.SharedKernel.Infrastructure.csproj` com `ProjectReference` para `sk/Bank.SharedKernel.csproj` e `PackageReference` `Microsoft.EntityFrameworkCore.Relational` (versão central) — nenhuma outra dependência (research R1, R10)
- [X] T004 Incluir os dois projetos em `back/Bank.slnx` numa pasta `/src/SharedKernel/` e copiar os dois `.csproj` em `back/Dockerfile` antes do `dotnet restore` (research R9)

---

## Phase 2: Foundational (Blocking Prerequisites)

Nenhuma além do setup: as histórias dependem só dos projetos do kernel (T002–T004). A US2 e a US3
usam a US1 concluída para avaliar a arquitetura real com o kernel.

---

## Phase 3: User Story 1 - Núcleo compartilhado separado, sem mudança de comportamento (Priority: P1) 🎯 MVP

**Goal**: os tipos comuns vivem no Shared Kernel e todo o sistema os usa de lá, com comportamento idêntico.

**Independent Test**: suíte completa com a mesma contagem e resultado da T001; `has-pending-model-changes`
limpo; nenhum arquivo alterado em `infra/Persistencia/Migrations/`.

### Implementação da US1

- [X] T005 [US1] `git mv` de `dom/Comum/{Dinheiro,Erro,Resultado,IIdentificador,EventoId}.cs` para `sk/` e trocar o namespace para `Bank.SharedKernel`, sem alterar nomes nem membros (data-model, "Shared Kernel (parte pura)")
- [X] T006 [US1] `git mv` de `dom/Calendario/CalendarioBancario.cs` para `sk/Calendario/` com namespace `Bank.SharedKernel.Calendario`
- [X] T007 [US1] `git mv` de `app/Comum/Entradas.cs` para `sk/Aplicacao/Entradas.cs` com namespace `Bank.SharedKernel.Aplicacao` (`Paginacao`, `PaginaDe<T>`, `Entradas`, `ErrosIdempotencia`); conferir que o arquivo não usa nada além da BCL e de `Bank.SharedKernel`
- [X] T008 [US1] `git mv` de `infra/Persistencia/IdentificadorConverter.cs` para `ski/Persistencia/` com namespace `Bank.SharedKernel.Infrastructure.Persistencia`, trocando `internal sealed` por `public sealed` e ajustando o comentário de cabeçalho (FR-002a)
- [X] T009 [US1] Referências de projeto: `dom/Bank.Domain.csproj` → `sk/`; `infra/Bank.Infrastructure.csproj` → `ski/`; remover as pastas vazias `dom/Comum/`, `dom/Calendario/` e `app/Comum/` (FR-003: sem reexportação)
- [X] T010 [US1] Trocar em `back/src/**/*.cs` os `using` e nomes qualificados: `Bank.Domain.Comum` → `Bank.SharedKernel`, `Bank.Domain.Calendario` → `Bank.SharedKernel.Calendario`, `Bank.Application.Comum` → `Bank.SharedKernel.Aplicacao`, `Bank.Infrastructure.Persistencia.IdentificadorConverter` → `Bank.SharedKernel.Infrastructure.Persistencia`; incluir `cref` de comentários XML e `using` em `infra/Persistencia/BankDbContext.cs`
- [X] T011 [US1] Trocar os mesmos `using` em `ut/`, `it/` e `arq/`, sem tocar em asserções (FR-022); em `arq/IdentificadoresTipadosTests.cs`, usar `Bank.SharedKernel.IIdentificador` e incluir o assembly do kernel na busca de identificadores; em `arq/LayerDependencyTests.cs`, carregar também `Bank.SharedKernel` e `Bank.SharedKernel.Infrastructure` sem alterar as regras
- [X] T012 [P] [US1] Mover `ut/Domain/CalendarioBancarioTests.cs` para `ut/SharedKernel/CalendarioBancarioTests.cs` (namespace `Bank.UnitTests.SharedKernel`), sem mudar os casos
- [X] T013 [US1] `dotnet build src/backend/Bank.slnx -c Release` (avisos como erro) e suíte completa; comparar com a T001: mesma contagem e todos verdes, incluindo os testes de serialização e de schema dos contratos de mensageria (FR-021, SC-001)
- [X] T014 [US1] `dotnet ef migrations has-pending-model-changes --project src/backend/src/Bank.Infrastructure --startup-project src/backend/src/Bank.Api` sem mudanças e `git diff main -- src/backend/src/Bank.Infrastructure/Persistencia/Migrations` vazio (SC-002)

**Checkpoint**: kernel extraído; commit `refactor(sharedkernel): ...`. O cenário 2 da US1 (kernel isolado) é garantido aqui pela T002 (projeto sem nenhum `PackageReference` nem `ProjectReference`) e verificado de forma automatizada pela MOD-05 na T020.

---

## Phase 4: User Story 2 - Fronteiras entre módulos verificadas automaticamente (Priority: P1)

**Goal**: o build falha, com regra, origem e destino, em toda violação do Princípio VIII.

**Independent Test**: contra os módulos de exemplo, cada regra MOD-01…MOD-07 detecta ao menos uma
violação e os módulos conformes não geram nenhuma; a arquitetura real não tem violações.

### Módulos de exemplo e testes da US2 (antes do avaliador)

- [X] T015 [US2] Criar `ex/Bank.ArchitectureTests.Exemplos.csproj` (`FrameworkReference` `Microsoft.AspNetCore.App`, `PackageReference` `Microsoft.EntityFrameworkCore.Relational`, sem referência a projetos de produção), referenciado só por `arq/Bank.ArchitectureTests.csproj`; incluir em `back/Bank.slnx` na pasta `/tests/` (research R5)
- [X] T016 [P] [US2] Módulo conforme `Alfa` em `ex/Alfa/`: `Bank.Modulos.Alfa.Contratos` (`AlfaId` como `readonly record struct`, `IAlfaConsultas`, `AlfaResumo`, públicos), `.Domain` (`Alfa`), `.Application` (`ConsultarAlfa` que implementa `IAlfaConsultas`), `.Infrastructure` (`AlfaDbContext : DbContext`, `AlfaRepositorio`), `.Endpoints` (`AlfaEndpoints`) — tipos fora de `.Contratos` já `internal`, respeitando MOD-04
- [X] T017 [P] [US2] Módulo conforme `Beta` em `ex/Beta/`: `.Application` usa só `Bank.Modulos.Alfa.Contratos.IAlfaConsultas` e `AlfaId`
- [X] T018 [US2] Violações de exemplo em `ex/Violacoes/`, um tipo por caso, com nome que descreve a regra: `Bank.Modulos.Gama.Application.UsaDominioDeAlfa` (MOD-01), `Bank.Modulos.Gama.Infrastructure.UsaAlfaDbContext` (MOD-02), `Bank.Modulos.Gama.Application.UsaNucleoAntigo` → isca `Bank.Domain.Exemplo.TipoDoNucleoAntigo` (MOD-03), `Bank.Modulos.Gama.Domain.UsaInfraDoProprioModulo` e `Bank.Modulos.Gama.Contratos.ContratoUsaInterno` (MOD-04), iscas `Bank.SharedKernel.Exemplo.KernelUsaModulo` e `Bank.SharedKernel.Infrastructure.Exemplo.InfraComumUsaModulo` (MOD-05), `Bank.Api.Exemplo.HostUsaInterno` (MOD-06), `Bank.Modulos.Gama.Domain.UsaInfraComum` e `Bank.Modulos.Gama.Endpoints.EndpointUsaInfraComum` → isca `Bank.SharedKernel.Infrastructure.Exemplo.ConversorDeExemplo` (MOD-07) (contracts/regras-fronteira.md)
- [X] T019 [US2] `arq/Modulos/DeteccaoRegrasTests.cs`: avaliar a arquitetura dos exemplos e afirmar o conjunto exato de violações esperadas `(Regra, Origem, Destino)` — ao menos uma por regra (FR-014, SC-003), nenhuma com origem em `Alfa` ou `Beta`, e uma única regra por dependência (a mais específica: `UsaAlfaDbContext` só como MOD-02); verificar o texto da falha `MOD-0X <descrição>: <Origem> → <Destino>` (FR-015)
- [X] T020 [US2] `arq/Modulos/FronteirasTests.cs`: arquitetura real (núcleo antigo, hosts, kernel e `Bank.Modulos.*` carregados) sem violações fora da lista; e teste "regras não vazias" que garante que o carregador encontrou tipos de `Bank.SharedKernel` e `Bank.SharedKernel.Infrastructure`

### Implementação da US2

- [X] T021 [US2] `arq/Modulos/CarregadorArquitetura.cs`: `Real()` carrega os cinco assemblies atuais mais todo `Bank.SharedKernel*.dll` e `Bank.Modulos.*.dll` de `AppContext.BaseDirectory`; `Exemplos()` carrega só `Bank.ArchitectureTests.Exemplos` (research R6)
- [X] T022 [US2] `arq/Modulos/RegrasFronteira.cs`: `Violacao(Regra, Origem, Destino)` e `Avaliar(Architecture)` que percorre `Architecture.Types` e `Dependencies`→`Target`, descobre os módulos pelo prefixo `Bank.Modulos.<Nome>`, aplica MOD-02, MOD-01, MOD-03, MOD-04, MOD-05, MOD-06 e MOD-07 nessa ordem (MOD-07 com origens em `Bank.Domain`, `Bank.Application` e `.Domain`/`.Application`/`.Endpoints`/`.Contratos` dos módulos) (primeira que casar), ignora dependências do tipo para ele mesmo e colapsa duplicatas (data-model, `Violacao`)
- [X] T023 [US2] `RegrasFronteira.Descrever(IEnumerable<Violacao>)`: cabeçalho "Violações de fronteira (constituição, Princípio VIII):" e uma linha por violação ordenada por regra e origem; T019 e T020 verdes

**Checkpoint**: regras ativas; commit `test(arquitetura): ...`.

---

## Phase 5: User Story 3 - Lista de exceções conhecidas que só diminui (Priority: P2)

**Goal**: violação nova, exceção obsoleta e exceção incompleta quebram o build; a lista real é vazia.

**Independent Test**: com a lista vazia a arquitetura real passa; com listas de exemplo sobre `Gama`,
cada um dos três casos falha com a mensagem certa.

### Testes da US3

- [X] T024 [US3] Em `arq/Modulos/DeteccaoRegrasTests.cs`, casos do mecanismo com as violações de `Gama`: lista sem uma violação → "violação nova" (FR-018); lista com entrada a mais → "exceção obsoleta: remova da lista" (FR-019); entrada com `Motivo` vazio ou `Issue` fora do formato `#N` → "exceção incompleta" (SC-005); lista completa e válida → passa

### Implementação da US3

- [X] T025 [US3] `arq/Modulos/ExcecoesConhecidas.cs`: `ExcecaoConhecida(Regra, Origem, Destino, Motivo, Issue)`, `ExcecoesConhecidas.Todas = []` com comentário de exemplo de entrada, e `Comparar(violacoes, excecoes)` que devolve novas, obsoletas e incompletas por `(Regra, Origem, Destino)` (contracts/regras-fronteira.md, "Lista de exceções")
- [X] T026 [US3] `arq/Modulos/FronteirasTests.cs` passa a usar `Comparar(Avaliar(Real()), ExcecoesConhecidas.Todas)` e falha com as três listas descritas; teste próprio que valida o formato de todas as entradas de `ExcecoesConhecidas.Todas` (FR-016, FR-017)

**Checkpoint**: lista de exceções ativa; commit `test(arquitetura): ...`.

---

## Phase 6: User Story 4 - Formato de módulo e registro nos hosts (Priority: P2)

**Goal**: API e Worker registram módulos por um ponto único; o formato é demonstrado pelo exemplo `Alfa`.

**Independent Test**: `RegistroModuloTests` resolve `IAlfaConsultas` e encontra o endpoint de `Alfa`
registrados só pelo `RegistroModulos` de exemplo; API e Worker sobem com os `RegistroModulos` vazios.

### Testes da US4

- [X] T027 [P] [US4] `arq/Modulos/RegistroModuloTests.cs`: (a) `ServiceCollection` + `Bank.Exemplos.Host.RegistroModulos.AddModulos(config)` resolve `IAlfaConsultas`; (b) `WebApplication.CreateBuilder()` + `AddModulos` + `MapModulos` expõe o endpoint de `Alfa` em `EndpointDataSource`, sem servidor de teste; (c) `AddModuloAlfaWorker` registra o serviço hospedado de exemplo
- [X] T028 [P] [US4] Em `arq/Modulos/PontoDeRegistroTests.cs`, verificar a convenção do ponto de registro: em cada `Bank.Modulos.<Nome>` (fora de `.Contratos`) o único tipo público é `Bank.Modulos.<Nome>.Modulo<Nome>`; nos exemplos, os tipos públicos de `Gama` (que não tem ponto de registro) são apontados e `Alfa`/`Beta` passam (contracts/formato-modulo.md)

### Implementação da US4

- [X] T029 [US4] `ex/Alfa/ModuloAlfa.cs` (`AddModuloAlfa`, `MapModuloAlfa`, `AddModuloAlfaWorker`, este com um serviço hospedado `internal` de exemplo) e `ex/Host/RegistroModulos.cs` (`Bank.Exemplos.Host`) chamando os métodos de `Alfa`; T027 e T028 verdes
- [X] T030 [P] [US4] `api/Modulos/RegistroModulos.cs` com `AddModulos(this IServiceCollection, IConfiguration)` e `MapModulos(this IEndpointRouteBuilder)` vazios e comentados (como incluir um módulo); `api/Program.cs` chama `AddModulos` depois de `AddInfrastructure(...)` e `MapModulos()` depois de `MapRecebiveisEndpoints()`/`MapTestEndpoints()`, sem mudar a ordem existente (FR-006, FR-007)
- [X] T031 [P] [US4] `wrk/Modulos/RegistroModulos.cs` com `AddModulos(this IServiceCollection, IConfiguration)` vazio e comentado; `wrk/Program.cs` chama `AddModulos` depois do registro atual (FR-006, FR-007)

**Checkpoint**: formato e registro prontos; commit `feat(modulos): ...`.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T032 [P] `docs/desenvolvimento.md`: seção "Módulos e fronteiras" — Shared Kernel (o que entra e o que não entra), formato de módulo e registro (link para `contracts/formato-modulo.md`), regras MOD-01…MOD-07, como ler uma falha, lista de exceções (onde fica, formato, por que nunca recebe entradas, como remover) (FR-020)
- [ ] T033 Imagem e contrato: `docker compose up -d --build` com API e Worker healthy; `cd src/frontend && npm run api:tipos && git diff --exit-code src/api/schema.d.ts` sem diff (SC-002)
- [ ] T034 e2e completo (`npx playwright test` em `src/frontend`) contra o Compose recém-construído, mesmo resultado da `main`
- [ ] T035 Suíte completa do backend final e roteiro do [quickstart.md](quickstart.md) (seções 1–5); contagem = T001 + testes novos
- [ ] T036 Revisar a spec, o plano e os contratos contra o implementado (Princípio I), conferir os tipos do kernel contra a tabela do research R1 e do data-model (FR-004) e marcar as tarefas concluídas

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001–T004)**: T001 antes de qualquer mudança; T002 e T003 em paralelo; T004 depois deles
- **US1 (T005–T014)**: depende do setup; T005–T008 em sequência (mesma troca de namespaces), T009 → T010 → T011 → T013 → T014; T012 em paralelo com T010–T011
- **US2 (T015–T023)**: depende da US1 (a arquitetura real inclui o kernel); T015 → T016/T017 em paralelo → T018 → T019/T020 → T021 → T022 → T023
- **US3 (T024–T026)**: depende da US2 (usa `Violacao` e os exemplos)
- **US4 (T027–T031)**: T027–T029 dependem de T015–T016; T030 e T031 só dependem da US1 e podem andar em paralelo com a US2
- **Polish (T032–T036)**: depois de todas as histórias

### Parallel Opportunities

- Setup: T002 e T003
- US1: T012 junto de T010–T011
- US2: T016 e T017
- US4: T030 e T031 em paralelo com a US2; T027 e T028 juntos
- Polish: T032 junto de T033

## Parallel Example: User Story 2

```text
# Depois de T015:
T016 ex/Alfa/  (módulo conforme)
T017 ex/Beta/  (módulo conforme)

# Depois de T018, os testes falham primeiro (sem avaliador):
T019 arq/Modulos/DeteccaoRegrasTests.cs
T020 arq/Modulos/FronteirasTests.cs

# Em paralelo, sem conflito de arquivos:
T030 api/Modulos/RegistroModulos.cs
T031 wrk/Modulos/RegistroModulos.cs
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Setup (T001–T004)
2. US1 (T005–T014)
3. **Parar e validar**: suíte igual à linha de base, modelo e migrations sem mudança

### Incremental Delivery

1. US1 → kernel extraído (MVP)
2. US2 → regras ativas → US3 → lista de exceções
3. US4 → formato e registro nos hosts
4. Polish → documentação, contrato OpenAPI, e2e, quickstart

Um commit por passo coerente (Conventional Commits), na branch `017-modulos-fundacao`.
