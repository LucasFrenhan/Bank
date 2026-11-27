---

description: "Lista de tarefas da feature 016-sessao-refresh-token"
---

# Tasks: Sessão renovável com refresh token rotativo e logout no servidor

**Input**: Design documents from `specs/016-sessao-refresh-token/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII). Em cada fase os testes vêm antes da implementação e
devem falhar primeiro. Backend: unitários com `FakeTimeProvider` e integração com PostgreSQL em contêiner.
Frontend: Vitest + MSW e Playwright + axe contra a API do Compose.

**Organization**: a fundação cria a sessão no login (domínio, persistência, cookie e token em memória). Cada
história acrescenta uma regra do ciclo de vida: renovar (US1), sair (US2), reuso (US3) e limites com aviso
(US4).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend: `back/` = `src/backend/`; `dom/` = `back/src/Bank.Domain/`, `app/` = `back/src/Bank.Application/`,
`infra/` = `back/src/Bank.Infrastructure/`, `api/` = `back/src/Bank.Api/`, `wrk/` = `back/src/Bank.Worker/`,
`ut/` = `back/tests/Bank.UnitTests/`, `it/` = `back/tests/Bank.IntegrationTests/`. SQL em
`infra/Persistencia/Sql/`, com cabeçalho `-- Origem: specs/016-sessao-refresh-token (...)`.
Frontend: `front/` = `src/frontend/`; `src/` = `front/src/`; `e2e/` = `front/e2e/`; `comp/` =
`src/design/componentes/`. Comandos `npm` rodam em `front/`.

---

## Phase 1: Setup

**Purpose**: ponto de partida e configuração; nenhuma dependência nova

- [ ] T001 Registrar a contagem atual de testes do backend, do frontend (Vitest) e e2e, para comparar no fim
  (nenhum teste anterior pode sumir sem substituto)
- [ ] T002 [P] Configuração: em `api/appsettings.json`, `Jwt:ExpiracaoSegundos` 3600 → 300 e seção `Sessao`
  (`InatividadeSegundos` 900, `LimiteAbsolutoHoras` 8, `ToleranciaReusoSegundos` 30, `RetencaoDias` 7); em
  `wrk/appsettings.json`, `Sessao` e `LimpezaSessoes` (`Habilitada` true, `IntervaloMinutos` 60), com
  `LimpezaSessoes:Habilitada` false em `wrk/appsettings.Testing.json`; em `docker-compose.yml` (api e worker) e
  `.env.example`, `JWT_EXPIRACAO_SEGUNDOS`, `SESSAO_INATIVIDADE_SEGUNDOS`, `SESSAO_TOLERANCIA_REUSO_SEGUNDOS`, `SESSAO_LIMITE_ABSOLUTO_HORAS`
  (contracts/autenticacao-sessao-api.md, "Configuração")
- [ ] T002a [P] Prazos do e2e: `.env.e2e.example` na raiz com `JWT_EXPIRACAO_SEGUNDOS=10`,
  `SESSAO_INATIVIDADE_SEGUNDOS=25` e `SESSAO_TOLERANCIA_REUSO_SEGUNDOS=5`; no job `frontend-e2e` de
  `.github/workflows/ci.yml`, as mesmas variáveis no `env` do passo `docker compose up`; em
  `docs/desenvolvimento.md`, como subir o Compose com esses prazos para rodar o e2e localmente. Os e2e leem os
  prazos da resposta do login (não fixam os números)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: o login cria a sessão no servidor, entrega o cookie e os prazos; o frontend guarda o token só em
memória

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Domínio e configuração — testes

- [ ] T003 [P] `ut/Domain/SessaoTests.cs`: `Sessao.Iniciar` cria sessão com `ExpiraEm = IniciadaEm + Absoluto`,
  `UltimaRenovacaoEm = IniciadaEm` e uma credencial ativa com
  `ExpiraEm = min(EmitidaEm + Inatividade, Sessao.ExpiraEm)`; `Revogar` é idempotente (a primeira revogação e
  o primeiro motivo valem); `PrazoInatividade` e `PrazoAbsoluto` em segundos restantes, nunca negativos
- [ ] T004 [P] `ut/Domain/HashCredencialTests.cs`: aceita exatamente 32 bytes, recusa outros tamanhos;
  igualdade por bytes; `ToString()` não expõe os bytes
- [ ] T005 [P] `ut/Application/SessaoOptionsTests.cs`: validação recusa valores ≤ 0, inatividade > limite
  absoluto e tolerância ≥ inatividade, com mensagem que identifica a chave (modelo de `CartoesOptionsTests`)
- [ ] T006 [P] `ut/Infrastructure/GeradorCredencialAleatoriaTests.cs`: valor Base64Url de 43 caracteres sem
  `=`, `+` ou `/`; 1.000 valores distintos; `CalcularHash(valor)` = SHA-256 do valor e igual ao hash gerado
  junto; valor malformado não lança (devolve hash que não existe)

### Domínio e configuração — implementação

- [ ] T007 [P] Identificadores `dom/Sessoes/SessaoId.cs` e `dom/Sessoes/CredencialRenovacaoId.cs`
  (`readonly record struct` + `IIdentificador<TSelf>`, `Novo(agora)` UUIDv7), no modelo de `UsuarioId`
- [ ] T008 [P] `dom/Sessoes/HashCredencial.cs` ("32 bytes (SHA-256)... nunca guarda o valor original") e
  `dom/Sessoes/MotivoRevogacao.cs` (`Logout`, `Reuso`, `NovoLogin`)
- [ ] T009 `dom/Sessoes/LimitesSessao.cs`, `dom/Sessoes/CredencialRenovacao.cs` e `dom/Sessoes/Sessao.cs` com
  `Iniciar`, `Revogar`, `PrazoInatividade`, `PrazoAbsoluto` e a coleção de credenciais (data-model.md);
  invariante "no máximo uma credencial com `RotacionadaEm` nulo por sessão"
- [ ] T010 [P] `app/Autenticacao/SessaoOptions.cs` + validador registrado com `ValidateOnStart`, convertendo
  para `LimitesSessao`
- [ ] T011 [P] Abstrações `app/Abstracoes/ISessaoRepositorio.cs` (obter por hash com bloqueio da sessão,
  adicionar, gravar), `app/Abstracoes/IGeradorCredencial.cs` (`Gerar()` → valor + hash; `CalcularHash(valor)`)
  e `app/Abstracoes/ILimpezaSessoes.cs`; `IEmissorToken.Emitir(usuario, sessaoId)`
- [ ] T012 [P] `infra/Seguranca/GeradorCredencialAleatoria.cs` (`RandomNumberGenerator`, 32 bytes, Base64Url,
  `SHA256.HashData`)
- [ ] T013 `infra/Seguranca/EmissorTokenJwt.cs`: claim `sid` com o `SessaoId`; ajustar `ut/` e `it/` que
  conferem as claims (`LoginTests`) para aceitar `sid`

### Persistência

- [ ] T014 Mapeamento `infra/Persistencia/Configuracoes/SessaoConfiguracao.cs` e
  `CredencialRenovacaoConfiguracao.cs` + `DbSet` em `BankDbContext`: tabelas `sessoes` e
  `credenciais_renovacao` exatamente como em data-model.md ("hash bytea NOT NULL UNIQUE CHECK
  (octet_length(hash) = 32)", "UNIQUE (sessao_id) WHERE rotacionada_em IS NULL", "CHECK ((revogada_em IS NULL)
  = (motivo_revogacao IS NULL))", `motivo_revogacao varchar(20)`, FKs `ON DELETE CASCADE`, índices em
  `usuario_id`, `expira_em` e `revogada_em` parcial), com `IdentificadorConverter`
- [ ] T015 Migration `AdicionarSessoes` em `infra/Persistencia/Migrations/` (gerada pelo `dotnet ef`, revisada)
- [ ] T016 [P] `infra/Persistencia/Sql/Consultas/BloquearSessao.sql` (`SELECT … FROM sessoes WHERE id = @id
  FOR UPDATE`) como recurso embutido; `SqlEmbutidoTests` passa a cobri-lo
- [ ] T017 `infra/Persistencia/SessaoRepositorio.cs`: busca a credencial pelo hash, bloqueia a sessão com
  `BloquearSessao.sql` dentro da transação e carrega a sessão com a credencial apresentada e a ativa;
  `AdicionarAsync`, `GravarAsync`; registro em `infra/DependencyInjection.cs` (repositório, gerador)
- [ ] T018 [P] `it/SessaoPersistenciaTests.cs`: restrições do banco (hash de 31 bytes recusado, duas
  credenciais ativas na mesma sessão recusadas, revogação sem motivo recusada, exclusão do usuário apaga
  sessões e credenciais)

### Login cria a sessão — testes

- [ ] T019 [P] Ampliar `ut/Application/AutenticarTests.cs` (dublês em `ut/Application/Dubles.cs`): sucesso cria
  sessão e devolve token, credencial e prazos (900 e 28800 com os padrões); credencial atual de uma sessão
  ativa é revogada com `NovoLogin`; credencial desconhecida é ignorada; falha de login não cria sessão nem
  revoga nada
- [ ] T020 [P] `it/SessaoDeTeste.cs` (helper): lê o `Set-Cookie` de `bank_renovacao`, expõe valor e
  atributos, e monta o cabeçalho `Cookie` explicitamente (research R4, o `CookieContainer` não reenvia
  `Secure` sobre HTTP)
- [ ] T021 Ampliar `it/LoginTests.cs`: `Set-Cookie` com `HttpOnly`, `Secure`, `SameSite=Strict`,
  `Path=/api/v1/autenticacao`, `Max-Age=900` e sem `Domain`; `Cache-Control: no-store`; corpo com
  `expiraEmSegundos` 300, `sessaoExpiraPorInatividadeEmSegundos` e `sessaoExpiraEmSegundos`; banco com uma
  sessão e um hash ≠ valor do cookie; login com cookie de sessão ativa revoga a anterior (`NovoLogin`) e não
  toca sessões de outro "navegador" da mesma pessoa (FR-012a); falha de login sem `Set-Cookie`

### Login cria a sessão — implementação

- [ ] T022 `app/Autenticacao/Autenticar.cs`: recebe a credencial atual opcional; em uma transação, revoga a
  sessão anterior apresentada (`NovoLogin`) e cria a nova; devolve `SessaoEmitida` (token, tipo, expiração,
  credencial, prazos) em `app/Autenticacao/SessaoEmitida.cs`; métrica `bank.autenticacao.sessoes_revogadas`
  (`motivo`) em `app/Observabilidade/MetricasUsuarios.cs`
- [ ] T023 `api/Seguranca/CookieRenovacao.cs`: nome `bank_renovacao`, atributos do contrato, `Ler`, `Escrever
  (valor, maxAge)` e `Remover`; `api/Endpoints/AutenticacaoEndpoints.cs`: o login lê o cookie, chama o caso de
  uso, escreve o cookie, devolve o corpo 1.1.0 com `no-store`; documentação OpenAPI dos campos e do cookie
- [ ] T024 Contrato: atualizar `OpenApiTests` se houver snapshot e regenerar `src/api/schema.d.ts`
  (`npm run api:tipos`, que lê `/openapi/v1.json` da API em execução: subir a API do branch antes, por SDK
  na porta 5080 ou por `BANK_API_URL`)

### Frontend: token só em memória

- [ ] T025 [P] Testes em `src/sessao/sessao.test.tsx`: após `entrar`, nenhum valor em `sessionStorage` ou
  `localStorage` contém o token; a chave antiga `bank.sessao` é removida na carga; `removerSessao()` ainda
  limpa transferência e `bank.operacoes`
- [ ] T026 `src/sessao/tokenMemoria.ts` (obter, definir, limpar) e `src/sessao/armazenamento.ts` sem
  `sessionStorage` para a sessão (remove `bank.sessao` legado); `src/api/cliente.ts` lê o token de
  `tokenMemoria`; `ContextoSessao.entrar` guarda token e prazos (instantes locais a partir dos segundos
  recebidos)

**Checkpoint**: login cria sessão e cookie; o app funciona como antes, mas recarregar a página leva ao login
até a US1

---

## Phase 3: User Story 1 - Continuar usando o app sem ser deslogado (Priority: P1) 🎯 MVP

**Goal**: renovação sob demanda com rotação, repetição única da chamada e sessão compartilhada entre abas

**Independent Test**: com token de 10 s, entrar, esperar vencer, consultar e transferir sem voltar ao login e
sem perder a revisão; recarregar e continuar logado; abrir uma segunda aba já logada

### Tests for User Story 1

- [ ] T027 [P] [US1] `ut/Domain/SessaoTests.cs`: `Renovar` com a credencial ativa rotaciona (antiga com
  `RotacionadaEm` e `SubstitutaId`, nova ativa, `UltimaRenovacaoEm = agora`); sessão revogada → `Recusada`
- [ ] T028 [P] [US1] `ut/Application/RenovarSessaoTests.cs`: sucesso emite token com `sid` e nova credencial;
  credencial ausente, desconhecida ou de sessão revogada → falha `nao-autenticado` sem motivo; métrica
  `bank.autenticacao.renovacoes` (`sucesso`, `recusada`)
- [ ] T029 [P] [US1] `it/RenovacaoTests.cs`: `POST /renovar` com cookie válido → 200, corpo 1.1.0 e cookie
  novo; o antigo deixa de ser o ativo no banco; sem cookie e com cookie inventado → 401
  `urn:bank:problema:nao-autenticado`, `traceId` e `Set-Cookie` de remoção; token novo aceito em
  `/carteiras/minha`; `/renovar` não exige `Authorization`; respostas com `Cache-Control: no-store`
- [ ] T030 [P] [US1] `src/sessao/renovador.test.ts`: voo único (3 chamadas concorrentes → 1 `renovar`); usa
  `navigator.locks` quando existe e funciona sem; falha propaga `expirada`
- [ ] T031 [P] [US1] `src/api/cliente.test.ts`: 401 em chamada autenticada → renova e repete uma vez com o
  mesmo corpo e o mesmo `Idempotency-Key`; 401 na repetição → encerra; 401 em `/autenticacao/*` nunca renova;
  chamadas concorrentes com 401 → uma renovação e todas repetidas
- [ ] T032 [P] [US1] `src/sessao/sessao.test.tsx`: na carga, estado `verificando` → renova → `ativa` na rota
  pedida; sem sessão → login com retorno; renovação não apaga `bank.operacoes` nem a transferência em revisão;
  mensagem `entrou` de outra aba leva a tela de login ao início; `entrou` com outro `sub` faz a aba com
  sessão de outra pessoa descartá-la (inclusive dados da operação) e renovar; `renovou` atualiza os prazos

### Implementation for User Story 1

- [ ] T033 [US1] `dom/Sessoes/Sessao.cs`: `Renovar(hashApresentado, novoHash, agora, limites)` com os passos 1
  e 4 de data-model.md (revogada → recusa; ativa → rotaciona) e `dom/Sessoes/ResultadoRenovacao.cs`
- [ ] T034 [US1] `app/Autenticacao/RenovarSessao.cs`: transação, bloqueio pela credencial, `Sessao.Renovar`,
  emissão do token, gravação; log `Information` de recusa com motivo interno e `SessaoId`; métricas em
  `MetricasUsuarios`; registro em `app/DependencyInjection.cs`
- [ ] T035 [US1] `api/Endpoints/AutenticacaoEndpoints.cs`: `POST /renovar` público, sem corpo; sucesso escreve
  o cookie novo; falha remove o cookie e devolve o problema `nao-autenticado` (`ResultadoHttpExtensions`)
- [ ] T036 [US1] `src/api/cliente.ts`: funções `renovar()`; invólucro do `fetch` (guarda `request.clone()`,
  renova e repete uma vez; ignora `/autenticacao/*`); `integrarSessao` passa a receber `renovar` e
  `aoNaoAutenticado`
- [ ] T037 [US1] `src/sessao/renovador.ts` (voo único + `navigator.locks.request('bank.renovacao', …)`) e
  `src/sessao/canalAbas.ts` (`BroadcastChannel('bank.sessao')`: `entrou {sub}`, `renovou`, `saiu`, `expirou`, sem
  token; sem suporte, vira no-op)
- [ ] T038 [US1] `src/sessao/ContextoSessao.tsx` e `src/sessao/Rotas.tsx`: estado `verificando`/`ativa`/
  `ausente`; renovação na carga antes das rotas protegidas (tela de carregamento acessível); remove o
  temporizador de expiração por `expiraEm`; publica `entrou` e `renovou`; reage a `entrou` nas telas públicas
- [ ] T039 [US1] `e2e/sessao.spec.ts` e `e2e/apoio.ts`: recarregar mantém a sessão; segunda aba no mesmo
  contexto já entra; com o token de 10 s do e2e (T002a), uma transferência em revisão conclui após o
  vencimento, uma vez só; nenhum token em `sessionStorage`/`localStorage`; cookie `HttpOnly` (via
  `context.cookies()`); a tela de carregamento da abertura passa no axe e em `e2e/responsividade.spec.ts`
  (`desktop` e `mobile`); as suítes e2e existentes continuam verdes com os prazos curtos

**Checkpoint**: US1 entrega o MVP — sessão não cai em uso e sobrevive à recarga

---

## Phase 4: User Story 2 - Sair encerra a sessão de verdade (Priority: P1)

**Goal**: "Sair" revoga a sessão no servidor e leva todas as abas ao login

**Independent Test**: guardar o cookie, sair e tentar renovar com ele → 401

### Tests for User Story 2

- [ ] T040 [P] [US2] `ut/Application/EncerrarSessaoTests.cs`: credencial ativa → revoga com `Logout` e
  métrica; credencial já trocada (dentro ou fora da tolerância) → também revoga a sessão; ausente,
  desconhecida ou de sessão já revogada → sucesso sem efeito
- [ ] T041 [P] [US2] `it/SairTests.cs`: `POST /sair` → 204 com `Set-Cookie` de remoção, com e sem cookie,
  repetido; depois, `/renovar` com o cookie antigo → 401; `motivo_revogacao = Logout`; token de acesso emitido
  antes continua aceito até expirar (US2 cenário 4); corrida: renovar (A→B) e depois `sair` com A → a sessão
  fica revogada e `/renovar` com B → 401
- [ ] T042 [P] [US2] `src/sessao/sessao.test.tsx`: `sair` chama `/sair`, encerra mesmo com falha de rede e
  publica `saiu`; mensagem `saiu` de outra aba leva ao login sem aviso de expiração e limpa a operação em
  andamento da aba; uma renovação concluída depois do `sair` não restabelece a sessão

### Implementation for User Story 2

- [ ] T043 [US2] `app/Autenticacao/EncerrarSessao.cs` (localiza a sessão por qualquer credencial dela, ativa
  ou já trocada; idempotente; log `Information`) e registro em
  `app/DependencyInjection.cs`
- [ ] T044 [US2] `api/Endpoints/AutenticacaoEndpoints.cs`: `POST /sair` público, sem corpo, 204 e remoção do
  cookie
- [ ] T045 [US2] `src/api/cliente.ts` (`sair()`), `src/sessao/ContextoSessao.tsx` (`sair` assíncrono,
  descarte local garantido, `saiu` no canal, descarte de renovação tardia) e chamadores em `comp/Layout.tsx` e
  `src/paginas/Perfil.tsx`
- [ ] T046 [US2] `e2e/sessao.spec.ts`: "Sair" em uma aba leva a outra ao login; depois de sair, recarregar
  continua no login

**Checkpoint**: US1 e US2 completas — as duas histórias P1

---

## Phase 5: User Story 3 - Credencial roubada é detectada (Priority: P2)

**Goal**: reuso fora da tolerância revoga a sessão; abas simultâneas não são confundidas com reuso

**Independent Test**: renovar guardando a credencial antiga; após 30 s, reapresentá-la → 401 e a nova também
deixa de funcionar

### Tests for User Story 3

- [ ] T047 [P] [US3] `ut/Domain/SessaoTests.cs`: credencial rotacionada há 30 s exatos → aceita e rotaciona a
  ativa (sem segunda credencial ativa); há 30 s + 1 tick → `ReusoDetectado` e sessão revogada com `Reuso`;
  credencial de duas rotações atrás → reuso
- [ ] T048 [P] [US3] `ut/Application/RenovarSessaoTests.cs`: reuso → falha `nao-autenticado` idêntica à
  recusa comum, log `Warning` só com `SessaoId` e `UsuarioId`, métricas `renovacoes{resultado=reuso}` e
  `sessoes_revogadas{motivo=reuso}`
- [ ] T049 [P] [US3] `it/RenovacaoConcorrenteTests.cs`: 5 renovações paralelas com o mesmo cookie → todas 200,
  uma única credencial ativa no banco e sessão ativa; reuso após a tolerância (relógio controlado) → 401 com o
  mesmo corpo de recusa e a credencial mais nova também recusada; corpo e cabeçalhos da recusa por reuso iguais
  aos da recusa por cookie desconhecido

### Implementation for User Story 3

- [ ] T050 [US3] `dom/Sessoes/Sessao.cs`: passos 4 (tolerância) e 5 (reuso) de `Renovar`
- [ ] T051 [US3] `app/Autenticacao/RenovarSessao.cs`: grava a revogação por reuso antes de responder; log
  `Warning` ("Reuso de credencial detectado; sessão {SessaoId} revogada") e métricas

**Checkpoint**: US3 completa — rotação com detecção de reuso

---

## Phase 6: User Story 4 - A sessão termina por inatividade ou duração máxima (Priority: P2)

**Goal**: limites de 15 min e 8 h, aviso antes do fim (60 s, ou metade do prazo se menor) em todas as abas e limpeza das sessões encerradas

**Independent Test**: com prazos curtos, ficar parado até o aviso, escolher "Continuar conectado"; depois,
deixar chegar a zero e cair no login

### Tests for User Story 4

- [ ] T052 [P] [US4] `ut/Domain/SessaoTests.cs`: `Renovar` em `UltimaRenovacaoEm + Inatividade` → `Recusada
  (Inativa)`; em `ExpiraEm` → `Recusada(LimiteAbsoluto)`; renovação perto do limite absoluto gera credencial
  com `ExpiraEm = Sessao.ExpiraEm`; prazos devolvidos encolhem até o limite absoluto
- [ ] T053 [P] [US4] `ut/Application/LimparSessoesTests.cs` e `it/LimpezaSessoesTests.cs`: remove sessões
  revogadas, vencidas pelo limite absoluto ou por inatividade há mais de 7 dias, com as credenciais; mantém as
  demais; métrica `bank.autenticacao.sessoes_removidas` só quando remove algo
- [ ] T054 [P] [US4] `it/RenovacaoTests.cs`: com relógio controlado (`FakeTimeProvider` na factory), renovação
  após 15 min sem uso → 401; com renovações a cada 10 min, a renovação após 8 h → 401; `Max-Age` do cookie
  acompanha o prazo restante
- [ ] T055 [P] [US4] `src/sessao/AvisoExpiracao.test.tsx` (temporizadores falsos): aviso aos 60 s do fim (ou na metade do prazo, se menor que 2 min) por
  inatividade com "Continuar conectado" (renova, fecha e publica `renovou`) e "Sair"; zero → login com sessão
  expirada; aviso de limite absoluto só com "Entendi"; `renovou` de outra aba fecha o aviso; `aria-live`
  anuncia na abertura e a cada 15 s; axe sem violações

### Implementation for User Story 4

- [ ] T056 [US4] `dom/Sessoes/Sessao.cs`: passos 2 e 3 de `Renovar` e prazo da nova credencial limitado ao
  absoluto
- [ ] T057 [US4] `app/Autenticacao/LimparSessoes.cs` + `ILimpezaSessoes` em `infra/Persistencia/
  SessaoRepositorio.cs` (`ExecuteDeleteAsync`, LINQ); `infra/Sessoes/LimpezaSessoesHospedado.cs` e
  `LimpezaSessoesOptions.cs` (modelo de `ProcessamentoDiarioHospedado`); `AddLimpezaSessoes` em
  `wrk/Program.cs`
- [ ] T058 [US4] `src/sessao/AvisoExpiracao.tsx` sobre `comp/Confirmacao.tsx`, com os textos de
  contracts/frontend-sessao.md, montado no layout autenticado; temporizadores a partir dos prazos da sessão,
  reiniciados por `renovou`
- [ ] T059 [US4] `e2e/sessao.spec.ts`: com a inatividade de 25 s do e2e (T002a), o aviso
  aparece nas duas abas e "Continuar conectado" em uma fecha nas duas; `e2e/responsividade.spec.ts` e
  `e2e/teclado.spec.ts` cobrem o modal em `desktop` e `mobile`

**Checkpoint**: todas as histórias completas

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T060 [P] `it/DadosSensiveisSessaoTests.cs`: login, renovação, reuso e saída com coleta de logs, spans e
  métricas; nenhuma ocorrência do valor do cookie, do hash em hexadecimal ou do token (modelo de
  `DadosSensiveisTelemetriaTests`); `e2e/telemetria.spec.ts` confere que spans do navegador não carregam o
  token
- [ ] T061 [P] Arquitetura: confirmar que `IdentificadoresTipadosTests` e `LayerDependencyTests` cobrem
  `Sessoes/` e que nenhum endpoint injeta `BankDbContext`
- [ ] T062 [P] Documentação: nota de histórico em `specs/004-frontend-base/spec.md` (FR-009, FR-011, FR-012,
  FR-013 → spec 016) e em `specs/003-usuarios-carteiras/contracts/usuarios-carteiras-api.md` (v1.1.0 na spec
  016); `docs/desenvolvimento.md` com os prazos e como encurtá-los
- [ ] T063 Rodar o [quickstart.md](quickstart.md) (cenários 1 a 9) e a suíte completa; comparar a contagem com
  o T001
- [ ] T064 Marcar na issue #57 os itens efetivamente entregues

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências
- **Foundational (Phase 2)**: depende do Setup; bloqueia todas as histórias
- **US1 (Phase 3)**: depende da fundação — é o MVP
- **US2 (Phase 4)**: depende da fundação; no frontend, usa o canal de abas da US1 (T037)
- **US3 (Phase 5)**: depende da US1 (estende `Sessao.Renovar` e `RenovarSessao`)
- **US4 (Phase 6)**: depende da US1 (prazos na renovação); a limpeza (T053, T057) só depende da fundação
- **Polish (Phase 7)**: depois das histórias desejadas

### Within Each Phase

- Testes antes da implementação, falhando primeiro
- Domínio → Application → Infrastructure/Api → frontend → e2e
- `Sessao.cs` é editado em sequência por T009, T033, T050 e T056 (mesmo arquivo, sem [P])
- `AutenticacaoEndpoints.cs` em sequência por T023, T035 e T044; `ContextoSessao.tsx` por T026, T038 e T045

### Parallel Opportunities

- Fundação: T003–T006 juntos; T007, T008, T010, T011, T012 e T016 juntos
- US1: T027–T032 juntos; backend (T033–T035) e frontend (T036–T038) em paralelo
- US2 e a limpeza da US4 (T053, T057) podem andar em paralelo com a US3
- Polish: T060–T062 juntos

## Parallel Example: User Story 1

```text
# Testes que podem começar juntos:
T027 ut/Domain/SessaoTests.cs (Renovar)
T028 ut/Application/RenovarSessaoTests.cs
T029 it/RenovacaoTests.cs
T030 src/sessao/renovador.test.ts
T031 src/api/cliente.test.ts
T032 src/sessao/sessao.test.tsx

# Depois, duas frentes:
Backend:  T033 → T034 → T035
Frontend: T036 → T037 → T038 → T039 (e2e precisa do backend)
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Setup e fundação (T001–T026)
2. US1 (T027–T039)
3. **Parar e validar**: quickstart, cenários 2, 3 e 4

### Incremental Delivery

1. Fundação → login com sessão
2. US1 → renovação (MVP) → US2 → sair no servidor (fecha as duas P1)
3. US3 → reuso → US4 → limites, aviso e limpeza
4. Polish → telemetria, documentação, quickstart, issue

Um commit por passo coerente (Conventional Commits), na branch `016-sessao-refresh-token`.
