---

description: "Lista de tarefas da feature 005-usuarios-me"
---

# Tasks: Dados do titular autenticado

**Input**: Design documents from `specs/005-usuarios-me/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/usuarios-me-api.md](contracts/usuarios-me-api.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (FR-008; constituição, Princípio VII). Testes escritos primeiro e
falhando antes da implementação.

**Organization**: uma única história (US1); sem fase de setup (nenhuma dependência nova).

## Format: `[ID] [P?] [Story] Description`

## Path Conventions

Backend em `src/backend/` (abreviado `backend/`).

---

## Phase 1: Foundational

**Purpose**: erro de domínio e extração da leitura do `sub`, usados pela história

- [X] T001 [P] Adicionar `ErrosUsuario.NaoEncontrado` em `backend/src/Bank.Domain/Usuarios/ErrosUsuario.cs`:
  código `usuario-nao-encontrado`, mensagem "Usuário não encontrado para o token informado.",
  `TipoErro.NaoEncontrado` (data-model)
- [X] T002 [P] Adicionar o título "Usuário não encontrado" para `usuario-nao-encontrado` em
  `backend/src/Bank.Api/ErrorHandling/ResultadoHttpExtensions.cs` (contrato, tabela de erros)
- [X] T003 [P] Criar `backend/src/Bank.Api/Seguranca/ClaimsPrincipalExtensions.cs` com
  `TentarObterUsuarioId(this ClaimsPrincipal, out Guid)` (lê `sub`; falso se ausente ou não-GUID)
  e usá-lo em `backend/src/Bank.Api/Endpoints/CarteirasEndpoints.cs` (R2); os testes de
  `/carteiras/minha` da spec 003 continuam verdes

**Checkpoint**: base pronta

---

## Phase 2: User Story 1 - Consultar os próprios dados cadastrais (Priority: P1) 🎯 MVP

**Goal**: `GET /api/v1/usuarios/me` devolve os dados do titular do token, com 401/403/404
padronizados

**Independent Test**: contas PF e PJ (CNPJ alfanumérico) consultam os próprios dados; erros
conforme o contrato (quickstart cenários 1–2)

### Testes (escrever primeiro, devem falhar)

- [X] T004 [P] [US1] Criar `backend/tests/Bank.UnitTests/Application/ObterTitularTests.cs`:
  usuário existente → `Resultado` com os campos da consulta; inexistente →
  `ErrosUsuario.NaoEncontrado`
- [X] T005 [P] [US1] Criar `backend/tests/Bank.IntegrationTests/UsuariosMeTests.cs`
  (`BankApiFactory`, `Dados`, `ApiBank`, `TokensDeTeste`):
  - 200 PF: `usuarioId` e `nomeCompleto` do cadastro, e-mail em minúsculas, CPF com 11 dígitos,
    `tipoConta` `PessoaFisica`, `criadoEm` com offset zero; cabeçalho `Cache-Control: no-store`;
  - 200 PJ com CNPJ alfanumérico enviado em minúsculas e mascarado → `documento` com 14
    caracteres em maiúsculas, `tipoConta` `PessoaJuridica`;
  - dois usuários: cada token recebe só os próprios dados;
  - nome alterado direto na base após o login → resposta com o valor atual;
  - 401 `nao-autenticado` com `WWW-Authenticate: Bearer` para: sem token, expirado há 1 s,
    adulterado, outra chave, `alg: none`, HS512, emissor e audiência errados;
  - 403 `acesso-negado` para token válido sem `sub`, sem `tipo_conta`, sem `carteira_id`, e com
    `sub` não-GUID;
  - 404 `usuario-nao-encontrado` para token válido de usuário removido da base;
  - todas as respostas de erro `application/problem+json` com `traceId`

### Implementação

- [X] T006 [P] [US1] Criar `backend/src/Bank.Application/Abstracoes/IUsuarioConsultas.cs` com
  `ObterTitularAsync(Guid usuarioId, CancellationToken)` → `DadosTitular?` (`UsuarioId`,
  `NomeCompleto`, `Email`, `Documento`, `TipoConta`, `CriadoEm`) — data-model
- [X] T007 [US1] Criar `backend/src/Bank.Application/Usuarios/ObterTitular.cs` (devolve
  `Resultado<DadosTitular>`; não encontrado → `ErrosUsuario.NaoEncontrado`; sem logs com e-mail
  ou documento) e registrá-lo em `backend/src/Bank.Application/DependencyInjection.cs` (faz
  T004 passar)
- [X] T008 [US1] Criar `backend/src/Bank.Infrastructure/Persistencia/UsuarioConsultas.cs`
  (projeção `AsNoTracking` de `usuarios` para `DadosTitular`, sem `senha_hash` nem carteira) e
  registrá-lo em `backend/src/Bank.Infrastructure/DependencyInjection.cs`
- [X] T009 [US1] Criar `backend/src/Bank.Api/Endpoints/UsuariosEndpoints.cs` com
  `GET /api/v1/usuarios/me` (`RequireAuthorization(PoliticaUsuarioAutenticado)`, `sub` via
  `TentarObterUsuarioId` → senão `Forbid`, `Cache-Control: no-store` na resposta 200,
  `Produces<TitularResposta>(200)`, `ProducesProblem` 401/403/404, tag `Usuários`) e mapeá-lo em
  `backend/src/Bank.Api/Program.cs` (faz T005 passar)

**Checkpoint**: endpoint funcional e testado

---

## Phase 3: Polish & Cross-Cutting Concerns

- [X] T010 [P] Estender `backend/tests/Bank.IntegrationTests/OpenApiTests.cs`: caminho
  `/api/v1/usuarios/me` com respostas 200/401/403/404 e `security` `Bearer`
- [X] T011 [P] Estender `backend/tests/Bank.IntegrationTests/DadosSensiveisTelemetriaTests.cs`:
  a consulta a `/usuarios/me` não deixa e-mail nem documento em logs ou spans (FR-006, SC-003)
- [X] T012 [P] Atualizar `docs/desenvolvimento.md` (seção da spec 003) com o endpoint e link
  para o contrato desta spec
- [X] T013 Executar `dotnet build`/`dotnet test` em Release e os cenários do
  [quickstart.md](quickstart.md) no Compose (incluindo p95 de 100 consultas — SC-004);
  registrar resultados e divergências neste arquivo

### Registro da validação (2026-11-17)

- **Build/testes** (`-c Release`): 0 avisos; **232 testes** aprovados (19 novos: 2 unitários de
  `ObterTitular` e 17 de integração de `/usuarios/me`; OpenAPI e dados sensíveis estendidos).
- **Compose**: cadastro PJ com CNPJ alfanumérico gerado na hora (enviado em minúsculas) →
  `GET /api/v1/usuarios/me` 200 com `documento` em maiúsculas sem máscara, `criadoEm` em UTC e
  `Cache-Control: no-store`; sem token → 401 `nao-autenticado`.
- **SC-004**: 100 consultas sequenciais, p95 0,004 s (máx. 0,018 s).
- **SC-005**: verificado no PR da Fase 8 da spec 004 (issue #17).

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → Phase 3
- T001 antes de T004/T007; T003 antes de T009; T006 antes de T007 e T008
- Paralelo: T001–T003; T004–T006; T010–T012

## Implementation Strategy

Entrega única (MVP = US1). Commits por fase (`feat(usuarios-me): ...`, `test(usuarios-me): ...`)
e PR com `Closes #18`. Em seguida, Fase 8 da spec 004 (`npm run api:tipos` e T049–T052) em PR
com `Closes #17`.
