---

description: "Lista de tarefas da feature 009-limite-transferencia"
---

# Tasks: Limite de transferência

**Input**: Design documents from `specs/009-limite-transferencia/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/transferencias-api.md](contracts/transferencias-api.md),
[contracts/frontend.md](contracts/frontend.md), [quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII; critérios de aceite da issue #33):
unitários, integração contra PostgreSQL em contêiner e concorrência no backend; Vitest e
Playwright no frontend. Em cada história, os testes são escritos primeiro e devem falhar antes da
implementação.

**Organization**: setup e fundação comuns; depois US1 (limite por operação), US2 (limite diário),
US3 (concorrência e idempotência) e US4 (consulta de limites e tela Transferir); por fim,
transversais.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (abreviado `back/`): `back/src/Bank.*` e `back/tests/Bank.*`; comandos
`dotnet` rodam em `src/backend/`. Frontend em `src/frontend/` (abreviado `front/`); comandos `npm`
rodam em `src/frontend/`. Arquivos de raiz usam o caminho completo.

---

## Phase 1: Setup

**Purpose**: configuração dos limites e variáveis do ambiente local

- [ ] T001 Em `back/src/Bank.Api/appsettings.json`, acrescentar a seção
  `"Transferencias": { "Limites": { "PessoaFisica": { "PorOperacao": 1000.00, "Diario": 5000.00 } } }`
  (FR-006, research R2)
- [ ] T002 [P] Em `docker-compose.yml`, no `environment` do serviço `api`, acrescentar
  `Transferencias__Limites__PessoaFisica__PorOperacao: ${LIMITE_PF_POR_OPERACAO-1000.00}` e
  `Transferencias__Limites__PessoaFisica__Diario: ${LIMITE_PF_DIARIO-5000.00}` (sem `:` — vazio
  desliga o limite; só a variável não definida usa o padrão, research R2); em `.env.example`, nova
  seção "Limites de transferência (spec 009)" com `LIMITE_PF_POR_OPERACAO=1000.00` e
  `LIMITE_PF_DIARIO=5000.00` e comentários dizendo que valor vazio desliga o limite e que valores
  baixos (ex.: 4.00 / 6.00) permitem ver a recusa com o saldo de demonstração (quickstart, cenário 2)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: erro com detalhes, regra de domínio, opções e métrica usadas por todas as histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Testes da fundação (escrever primeiro)

- [ ] T003 [P] Criar `back/tests/Bank.UnitTests/Domain/LimitesTransferenciaTests.cs`:
  `VerificarOperacao` aceita valor igual ao limite e recusa um centavo acima (tipo `PorOperacao`,
  `limite` = `valorDisponivel` = limite); `VerificarDiario` aceita `transferidoHoje + valor ==
  Diario`, recusa um centavo acima com `valorDisponivel = max(0, Diario − transferidoHoje)` e
  nunca negativo (transferido acima do limite → 0); limites `null` nunca recusam;
  `DisponivelHoje` devolve `null` sem limite diário
- [ ] T004 [P] Criar `back/tests/Bank.UnitTests/Application/LimitesTransferenciaOptionsTests.cs`
  (no estilo de `SaldoInicialOptionsTests.cs`): aceita os padrões, aceita valores ausentes
  (`null` = sem limite); ligando uma `ConfigurationBuilder` com o padrão e uma sobrescrita de
  valor vazio (`""`), o limite resultante é `null` (research R2); recusa ≤ 0, mais de 2 casas decimais e `PorOperacao > Diario`, com a
  chave completa da configuração na mensagem (`Transferencias:Limites:PessoaFisica:...`)
- [ ] T005 [P] Criar `back/tests/Bank.UnitTests/ErrorHandling/ResultadoHttpExtensionsTests.cs`:
  um `Erro` com `Detalhes` vira `ProblemDetails` com essas chaves em `Extensions`, status 422 e
  `type` `urn:bank:problema:limite-excedido` com título "Limite de transferência excedido"; um erro
  sem detalhes continua sem extensões extras

### Implementação da fundação

- [ ] T006 Em `back/src/Bank.Domain/Comum/Erro.cs`, acrescentar o parâmetro opcional
  `IReadOnlyDictionary<string, object?>? Detalhes = null` ao record `Erro` (depois de `Campo`),
  sem mudar os usos existentes (data-model, "Erro (alterado)")
- [ ] T007 Criar `back/src/Bank.Domain/Transferencias/LimitesTransferencia.cs` com o enum
  `TipoLimite { PorOperacao, Diario }` e o record `LimitesTransferencia(decimal? PorOperacao,
  decimal? Diario)` com `VerificarOperacao(decimal valor)`, `VerificarDiario(decimal valor,
  decimal transferidoHoje)` (ambos `Resultado`) e `DisponivelHoje(decimal transferidoHoje)`
  (`decimal?`), regras exatamente como em data-model "LimitesTransferencia"; limites inclusivos
  (FR-004)
- [ ] T008 Em `back/src/Bank.Domain/Transferencias/ErrosTransferencia.cs`, acrescentar
  `LimiteExcedido(TipoLimite tipo, decimal limite, decimal valorDisponivel)`: código
  `limite-excedido`, `TipoErro.RegraDeNegocio`, mensagem "O valor passa do limite por
  transferência." (`PorOperacao`) ou "O valor passa do limite diário de transferências."
  (`Diario`), `Detalhes` = `{ tipoLimite: tipo.ToString(), limite, valorDisponivel }`
  (contracts/transferencias-api.md)
- [ ] T009 Em `back/src/Bank.Api/ErrorHandling/ResultadoHttpExtensions.cs`, acrescentar o título
  `["limite-excedido"] = "Limite de transferência excedido"` e copiar `erro.Detalhes` para as
  extensões do `ProblemDetails` em `ParaProblema` (R5); T005 passa
- [ ] T010 Criar `back/src/Bank.Application/Transferencias/LimitesTransferenciaOptions.cs`:
  `Secao = "Transferencias:Limites"`, propriedade `PessoaFisica` do tipo `LimitesPerfilOptions {
  decimal? PorOperacao; decimal? Diario }`, método `ParaPessoaFisica()` →
  `LimitesTransferencia`, e `ValidadorLimitesTransferenciaOptions : IValidateOptions` com as
  regras de T004 (> 0, até 2 casas com `Dinheiro.TemAteDuasCasas`, `PorOperacao ≤ Diario`)
- [ ] T011 Em `back/src/Bank.Application/DependencyInjection.cs`, registrar
  `AddOptions<LimitesTransferenciaOptions>().BindConfiguration(...).ValidateOnStart()` e o
  validador (como `SaldoInicialOptions`); T003 e T004 passam
- [ ] T012 Em `back/src/Bank.Application/Observabilidade/MetricasTransferencias.cs`, sobrecarga
  `RegistrarRecusa(Erro erro)` que adiciona a tag `tipo_limite` (valor de
  `Detalhes["tipoLimite"]`) somente quando o código é `limite-excedido`; demais recusas com a tag
  `motivo` apenas, como hoje (R7)

**Checkpoint**: `dotnet build` e `dotnet test --filter "FullyQualifiedName~Unit"` verdes

---

## Phase 3: User Story 1 - Recusar transferência acima do limite por operação (Priority: P1) 🎯 MVP

**Goal**: valor acima do limite por operação → 422 `limite-excedido`/`PorOperacao`, sem
alterar saldo, verificado antes do saldo

**Independent Test**: limite por operação R$ 1.000,00 e saldo R$ 5.000,00: R$ 1.000,00 conclui;
R$ 1.000,01 → 422 com `tipoLimite=PorOperacao`, `limite=1000.00`, `valorDisponivel=1000.00`;
valor acima do limite e do saldo → `limite-excedido`

### Testes da US1 (escrever primeiro)

- [ ] T013 [P] [US1] Em `back/tests/Bank.UnitTests/Application/RealizarTransferenciaTests.cs`,
  acrescentar (com opções de limite injetadas por `Options.Create`/monitor de teste): valor acima
  do limite por operação → falha `limite-excedido` sem iniciar a transação nem bloquear carteiras
  (dublê de `IUnidadeDeTransferencia` sem chamadas); valor acima do limite e do saldo →
  `limite-excedido`; métrica de recusa com `motivo=limite-excedido` e `tipo_limite=PorOperacao`;
  log `Warning` de auditoria com o identificador da carteira, sem documento nem nome
- [ ] T014 [P] [US1] Criar `back/tests/Bank.IntegrationTests/LimiteTransferenciaTests.cs` com uma
  fábrica `LimitesApiFactory : BankApiFactory` (saldo inicial PF `5000.00`, limites
  `PorOperacao=1000.00` e `Diario=2000.00` via `AddInMemoryCollection`, como `SaldoMilApiFactory`)
  e os testes: R$ 1.000,00 → 201; R$ 1.000,01 → 422 `application/problem+json` com `type`,
  `traceId`, `tipoLimite`, `limite`, `valorDisponivel` do contrato e saldos inalterados;
  acima do limite e do saldo → 422 `limite-excedido`; a mesma `Idempotency-Key` usada depois com
  R$ 500,00 → 201 (a recusa não gravou a chave, FR-010)

### Implementação da US1

- [ ] T015 [US1] Em `back/src/Bank.Application/Transferencias/RealizarTransferencia.cs`, injetar
  `IOptionsMonitor<LimitesTransferenciaOptions>`; depois da verificação de autotransferência e
  antes de `ObterTitularAsync`/`unidade.IniciarAsync`, chamar `VerificarOperacao(valor.Valor)` e
  recusar com o erro (FR-008); trocar `Recusar` para usar `metricas.RegistrarRecusa(erros[0])`;
  acrescentar `[LoggerMessage(Level = Warning)]` "Transferência recusada por limite {TipoLimite}
  na carteira {CarteiraOrigemId}: valor {Valor}, limite {Limite}, disponível {ValorDisponivel}"
  (FR-016) chamado em toda recusa por limite; T013 e T014 passam

**Checkpoint**: US1 funcional e testável sozinha

---

## Phase 4: User Story 2 - Recusar transferência que estoura o limite diário (Priority: P1)

**Goal**: soma do dia de Brasília (concluídas e enviadas) + valor > diário → 422
`limite-excedido`/`Diario` com o disponível; renova à meia-noite de Brasília

**Independent Test**: diário R$ 2.000,00: R$ 1.500,00 conclui; R$ 600,00 → 422 com
`valorDisponivel=500.00`; R$ 500,00 conclui; transferências recebidas e recusadas não contam;
virada às 00h00 de Brasília (03h00 UTC) libera o limite

### Testes da US2 (escrever primeiro)

- [ ] T016 [P] [US2] Criar `back/tests/Bank.UnitTests/Application/CalendarioBrasiliaTests.cs`:
  `2026-11-18T02:59:59Z` → dia 2026-11-17 (23h59 de Brasília); `2026-11-18T03:00:00Z` → dia
  2026-11-18 com `InicioUtc = 2026-11-18T03:00:00Z` e `FimUtc = 2026-11-19T03:00:00Z`; `FimUtc`
  exclusivo
- [ ] T017 [P] [US2] Em `back/tests/Bank.UnitTests/Application/RealizarTransferenciaTests.cs`,
  acrescentar: soma do dia + valor acima do diário → `limite-excedido`/`Diario` com
  `valorDisponivel` correto, transação desfeita (`DesfazerAsync`) e nada adicionado à unidade;
  a soma é pedida ao repositório depois de `BloquearCarteirasAsync` (ordem das chamadas no dublê)
  e com o intervalo do `CalendarioBrasilia`; valor que cabe no limite mas não no saldo →
  `saldo-insuficiente` (dublês em `back/tests/Bank.UnitTests/Application/DublesTransferencia.cs`
  ganham `SomarEnviadasAsync` configurável)
- [ ] T018 [P] [US2] Criar `back/tests/Bank.IntegrationTests/LimiteDiarioTests.cs` com a fábrica
  `LimiteDiarioApiFactory : BankApiFactory` (saldo inicial PF `5000.00`, `Diario=2000.00` e
  `PorOperacao=""` — vazio, sem limite por operação, para que só o diário recuse) e os testes:
  R$ 1.500,00 (201) → R$ 600,00 (422, `tipoLimite=Diario`, `limite=2000.00`,
  `valorDisponivel=500.00`) → R$ 500,00 (201) → R$ 0,01 (422, `valorDisponivel=0.00`);
  transferências recebidas pela PF não mudam o disponível; virada do dia com relógio controlado
  (fábrica que substitui o `TimeProvider` por um `RelogioAjustavel` próprio do projeto de
  integração): R$ 2.000,00 às 23h50 de Brasília (`02:50Z`) e R$ 2.000,00 às 00h10 (`03:10Z`) do
  dia seguinte → ambas 201; com `Diario=""` (fábrica derivada via `WithWebHostBuilder`) nenhuma
  recusa por limite

### Implementação da US2

- [ ] T019 [P] [US2] Criar `back/src/Bank.Application/Transferencias/CalendarioBrasilia.cs`:
  `DiaBrasilia(DateOnly Data, DateTimeOffset InicioUtc, DateTimeOffset FimUtc)` e
  `Hoje(DateTimeOffset agoraUtc)`; fuso resolvido uma vez por `America/Sao_Paulo`, depois
  `E. South America Standard Time`, senão `InvalidOperationException` com mensagem clara (R4),
  no construtor; registrar como singleton em `back/src/Bank.Application/DependencyInjection.cs`
  junto com um `IHostedService` interno (`VerificacaoFusoBrasilia`) que pede o singleton no
  `StartAsync`, para a API falhar na inicialização; T016 passa
- [ ] T020 [US2] Em `back/src/Bank.Application/Abstracoes/ITransferencias.cs`, acrescentar a
  `ITransferenciaRepositorio` o método `Task<decimal> SomarEnviadasAsync(Guid carteiraOrigemId,
  DateTimeOffset inicioUtc, DateTimeOffset fimUtc, CancellationToken)` (intervalo `[inicio,
  fim)`, 0 sem transferências) e implementá-lo por LINQ em
  `back/src/Bank.Infrastructure/Persistencia/TransferenciaRepositorio.cs`, usando o mesmo
  `BankDbContext` da unidade de transferência (mesma transação)
- [ ] T021 [US2] Em `back/src/Bank.Infrastructure/Persistencia/Configuracoes/TransferenciaConfiguracao.cs`,
  acrescentar o índice `ix_transferencias_origem_criado_em (carteira_origem_id, criado_em)` e
  gerar a migration `AdicionarIndiceTransferenciasOrigemCriadoEm` em
  `back/src/Bank.Infrastructure/Persistencia/Migrations/` (`dotnet ef migrations add`, sem SQL
  puro)
- [ ] T022 [US2] Em `back/src/Bank.Application/Transferencias/RealizarTransferencia.cs`, injetar
  `CalendarioBrasilia`; dentro da transação, depois do bloqueio e da segunda verificação de
  idempotência e antes de `Transferencia.Executar`, calcular o dia a partir do mesmo `agora`,
  somar com `SomarEnviadasAsync`, chamar `VerificarDiario` e, se recusar, `DesfazerAsync`, log de
  auditoria e `Recusar` (FR-008, FR-009, FR-010); T017 e T018 passam
- [ ] T023 [P] [US2] Em `src/backend/Dockerfile`, no estágio `final`, acrescentar
  `RUN apk add --no-cache tzdata` antes de `USER app`, com comentário citando a spec 009 (R4);
  validar com `docker compose up -d --build --wait` que a API sobe saudável

**Checkpoint**: US1 e US2 funcionais; quickstart cenário 2, passos 1–4

---

## Phase 5: User Story 3 - Limite consistente com concorrência e idempotência (Priority: P1)

**Goal**: envios simultâneos nunca somam mais que o diário; replay não consome nem reavalia
limite

**Independent Test**: diário R$ 1.000,00 e saldo R$ 5.000,00: 5 × R$ 300,00 simultâneas → 3
concluídas e 2 `limite-excedido`; replay de uma concluída com o limite esgotado → 200 original

### Testes da US3 (escrever primeiro)

- [ ] T024 [P] [US3] Criar `back/tests/Bank.IntegrationTests/ConcorrenciaLimiteTests.cs` com
  fábrica própria (saldo inicial PF `5000.00`, `Diario=1000.00`, `PorOperacao=""`): 5
  transferências simultâneas de R$ 300,00 de uma PF nova → exatamente 3 × 201 e 2 × 422
  `limite-excedido`, total enviado R$ 900,00 e saldo R$ 4.100,00, repetido 5 rodadas (SC-001);
  20 reenvios paralelos da mesma `Idempotency-Key` → 1 débito e o disponível cai uma única vez
  (SC-004)
- [ ] T025 [P] [US3] Em `back/tests/Bank.IntegrationTests/LimiteTransferenciaTests.cs`,
  acrescentar: com o diário esgotado, reenviar uma transferência concluída com a mesma chave e
  os mesmos dados → 200 com o mesmo `transferenciaId`; depois de reduzir o limite (fábrica com
  `WithWebHostBuilder` e `PorOperacao` menor que o valor original) o replay continua 200
- [ ] T026 [P] [US3] Em `back/tests/Bank.UnitTests/Application/RealizarTransferenciaTests.cs`,
  acrescentar: replay (primeira e segunda verificação de idempotência) não chama
  `SomarEnviadasAsync` nem registra recusa por limite, mesmo com valor acima do limite por
  operação configurado

### Implementação da US3

- [ ] T027 [US3] Confirmar em `back/src/Bank.Application/Transferencias/RealizarTransferencia.cs`
  que a verificação por operação fica depois da primeira verificação de idempotência (replay sai
  antes) e a diária depois da segunda (dentro do bloqueio), com T024–T026 verdes; corrigir a
  ordem se algum falhar (FR-008)

**Checkpoint**: US1–US3 completas; o backend entrega a regra inteira

---

## Phase 6: User Story 4 - Ver o limite antes de transferir (Priority: P2)

**Goal**: `GET /api/v1/transferencias/limites` e a tela Transferir mostrando o máximo agora e os
avisos de limite (desktop e mobile)

**Independent Test**: `GET /limites` → 200 (PF), 401, 403 (PJ); na tela Transferir a dica mostra
"Limite agora", valor acima não avança e o 422 da confirmação mostra o aviso por tipo

### Testes da US4 — backend (escrever primeiro)

- [ ] T028 [P] [US4] Criar `back/tests/Bank.UnitTests/Application/ConsultarLimitesTests.cs`:
  `ResumoLimites` com `PorOperacao`, `Diario`, `TransferidoHoje`, `DisponivelHoje = max(0, Diario
  − TransferidoHoje)` (`null` sem diário) e `Dia` de Brasília do relógio; usuário sem carteira →
  `usuario-nao-encontrado` (como os demais casos de uso); métrica
  `bank.transferencias.limites.consultas` com `resultado=consultada` ou `usuario-nao-encontrado`
- [ ] T029 [P] [US4] Em `back/tests/Bank.IntegrationTests/LimiteTransferenciaTests.cs`,
  acrescentar `GET /api/v1/transferencias/limites`: 200 com os campos do contrato antes e depois de
  uma transferência (`transferidoHoje`, `disponivelHoje`, `dia` no formato `yyyy-MM-dd`); 401 sem
  token e com token inválido; 403 `acesso-negado` para PJ; a rota não é capturada por
  `/{transferenciaId}`

### Implementação da US4 — backend

- [ ] T030 [US4] Criar `back/src/Bank.Application/Transferencias/ConsultarLimites.cs`
  (`ResumoLimites` + caso de uso `ExecutarAsync(Guid usuarioId, ...)` → `Resultado<ResumoLimites>`,
  sem transação nem bloqueio, R6) e registrá-lo em `back/src/Bank.Application/DependencyInjection.cs`;
  acrescentar `RegistrarConsultaLimites(string resultado)` (contador
  `bank.transferencias.limites.consultas`, R7) em
  `back/src/Bank.Application/Observabilidade/MetricasTransferencias.cs`; T028 passa
- [ ] T031 [US4] Em `back/src/Bank.Api/Endpoints/TransferenciasEndpoints.cs`, mapear
  `grupo.MapGet("/limites", ConsultarLimitesAsync)` antes de `/{transferenciaId}`, com
  `RequireAuthorization(politicasPagador)`, `WithName("ConsultarLimites")`, resumo em português,
  `Produces<LimitesResposta>(200)`, `ProducesProblem(401)`, `ProducesProblem(403)`; record
  `LimitesResposta(decimal? PorOperacao, decimal? Diario, decimal TransferidoHoje, decimal?
  DisponivelHoje, DateOnly Dia)`; na operação `Transferir`, descrição citando `limite-excedido`
  no 422; T029 passa

### Testes da US4 — frontend (escrever primeiro)

- [ ] T032 [P] [US4] Em `front/src/api/erros.test.ts`, acrescentar: `limite-excedido` com
  extensões vira `MensagemErro` com `codigo: 'limite-excedido'`, variante `atencao`, título
  "Limite excedido" e `limite: { tipo, limite, disponivel }` em centavos; extensões ausentes ou
  `tipoLimite` desconhecido → mesmo título com "Ajuste o valor para continuar."
- [ ] T033 [P] [US4] Criar `front/src/paginas/useLimites.test.ts`: máximo agora =
  `min(porOperacao, disponivelHoje)` ignorando `null`; ambos `null` → sem máximo; falha de rede,
  403 ou 5xx → sem máximo (sem aviso de erro); 401 segue o tratamento de sessão da spec 004
- [ ] T034 [P] [US4] Em `front/src/paginas/Transferir.test.tsx`, acrescentar: dica "Disponível:
  R$ {saldo} · Limite agora: R$ {máximo} · mínimo R$ 0,01" e, sem máximo, a dica da v1.1.1; valor
  acima do máximo não avança e mostra o aviso por tipo (`PorOperacao` antes de `Diario`); valor
  igual ao máximo avança; diário esgotado mostra "Você já usou todo o limite diário de R$
  {limite}. Ele renova à meia-noite (horário de Brasília)."
- [ ] T035 [P] [US4] Em `front/src/paginas/ConfirmarTransferencia.test.tsx`, acrescentar: 422
  `limite-excedido` da API mostra "Limite excedido" com o texto por tipo (contracts/frontend.md,
  "Tradução de erros") e oferece voltar para a etapa Dados

### Implementação da US4 — frontend

- [ ] T036 [US4] Com a API da US4 rodando, executar `npm run api:tipos` para regenerar
  `front/src/api/schema.d.ts` (operação `ConsultarLimites`, esquema `LimitesResposta`)
- [ ] T037 [US4] Em `front/src/api/erros.ts`, acrescentar `MENSAGENS.limiteExcedido` ("Limite
  excedido") e os detalhes por tipo de contracts/frontend.md; em `traduzirErro`, ler `tipoLimite`,
  `limite` e `valorDisponivel` do problema e preencher o novo campo opcional
  `limite?: { tipo: string; limiteCentavos: number; disponivelCentavos: number }` de
  `MensagemErro`; T032 passa
- [ ] T038 [US4] Criar `front/src/paginas/useLimites.ts` (no estilo de `useCarteira.ts`):
  `GET /api/v1/transferencias/limites`, estado `carregando | pronto | indisponivel`, e
  `maximoCentavos` + `tipoDoMaximo` calculados como em contracts/frontend.md; T033 passa
- [ ] T039 [US4] Em `front/src/paginas/avisosTransferencia.tsx`, renderizar `limite-excedido`
  com `Aviso variante="atencao"`, título "Limite excedido" e o texto por tipo/disponível
  (contracts/frontend.md, "Tradução de erros")
- [ ] T040 [US4] Em `front/src/transferencia/useTransferencia.ts`, estender
  `continuar(saldoCentavos)` para receber também os limites (`porOperacao`, `disponivelHoje` em
  centavos) e recusar localmente com uma `MensagemErro` `limite-excedido` equivalente à da API
  quando o valor passar do máximo (por operação antes do diário)
- [ ] T041 [US4] Em `front/src/paginas/Transferir.tsx`, usar `useLimites`, montar a dica do campo
  valor da tabela de contracts/frontend.md e mostrar o aviso `limite-excedido` no mesmo lugar do
  aviso de saldo insuficiente (abaixo do campo valor); recarregar os limites ao voltar da
  confirmação; T034 passa
- [ ] T042 [US4] Em `front/src/paginas/ConfirmarTransferencia.tsx`, tratar o resultado recusado com
  `codigo === 'limite-excedido'` como o de `saldo-insuficiente` (aviso + voltar para Dados);
  T035 passa
- [ ] T043 [US4] Em `front/e2e/transferencia.spec.ts`, acrescentar um teste que abre Transferir
  com uma PF nova e confere a dica "Limite agora: R$ 1.000,00" (o saldo de demonstração não
  alcança o limite padrão; a recusa fica nos testes de componente), em desktop e na largura
  mobile já usada pelos demais testes

**Checkpoint**: todas as histórias funcionais; quickstart cenários 2 e 3

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T044 [P] Em `back/tests/Bank.IntegrationTests/OpenApiTests.cs`, conferir a operação
  `ConsultarLimites` com `Bearer` e respostas 200/401/403, o esquema `LimitesResposta` e o 422 de
  `Transferir` (FR-017)
- [ ] T045 [P] Em `back/tests/Bank.IntegrationTests/DadosSensiveisTelemetriaTests.cs`, provocar uma
  recusa por limite e uma consulta de limites e conferir que nenhum documento, nome ou e-mail
  aparece em logs, spans ou métricas, e que a métrica de recusa tem `tipo_limite` (SC-008)
- [ ] T046 [P] Acrescentar nota de versão apontando para a v1.2.0 em
  `specs/008-consulta-transferencia/contracts/transferencias-api.md` (Histórico) e para o frontend
  v1.2.0 em `specs/007-frontend-transferencias/contracts/frontend.md` (Histórico)
- [ ] T047 [P] Em `docs/desenvolvimento.md`, documentar os limites de transferência: seção de
  configuração `Transferencias:Limites`, variáveis `LIMITE_PF_POR_OPERACAO`/`LIMITE_PF_DIARIO`
  (valor vazio desliga o limite), dia de Brasília e o `tzdata` da imagem
- [ ] T048 Rodar o quickstart completo (cenários 1–4), incluindo `dotnet test` em `src/backend/` e
  `npm run lint && npm run typecheck && npm test && npm run e2e` em `src/frontend/`, e conferir a
  dica e os avisos em desktop e mobile; medir o SC-007 no Compose (50 transferências sequenciais
  de uma PF com histórico no dia, p95 < 1 s) e registrar o resultado no PR; atualizar a spec, o plano e os contratos se algo divergir
  (Princípio I)

---

## Dependencies & Execution Order

- **Setup (1)** → **Foundational (2)** → US1 (3) → US2 (4) → US3 (5) → US4 (6) → Polish (7)
- US2 e US3 alteram o mesmo caso de uso que a US1 (`RealizarTransferencia.cs`), por isso seguem
  em sequência; a US3 é sobretudo de testes.
- US4 backend (T028–T031) depende só da fundação e de `SomarEnviadasAsync`/`CalendarioBrasilia`
  (T019, T020); US4 frontend depende de T031 (tipos gerados em T036).
- Dentro de cada história: testes primeiro (falhando), depois implementação.

### Parallel Opportunities

- T001 e T002; T003, T004 e T005
- T013 e T014; T016, T017 e T018; T019 e T023
- T024, T025 e T026
- T028 e T029; T032, T033, T034 e T035
- T044, T045, T046 e T047

## Parallel Example: User Story 2

```text
T016 CalendarioBrasiliaTests.cs   T017 RealizarTransferenciaTests.cs   T018 LimiteDiarioTests.cs
T019 CalendarioBrasilia.cs        T023 Dockerfile (tzdata)
```

---

## Implementation Strategy

### MVP First

1. Setup e fundação (T001–T012)
2. US1 (T013–T015) → limite por operação
3. **PARAR e VALIDAR**: quickstart cenário 2, passos 1–3

### Incremental Delivery

1. US2 → limite diário no dia de Brasília (com `tzdata`)
2. US3 → concorrência e idempotência comprovadas
3. US4 → consulta de limites e tela Transferir
4. Polish → OpenAPI, telemetria, documentação

### Commits

Conventional Commits por fase, escopo `limite-transferencia` (por exemplo
`feat(limite-transferencia): ...`, `test(limite-transferencia): ...`); PR com `Closes #33`.
