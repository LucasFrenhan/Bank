---

description: "Lista de tarefas da feature 014-cartao-credito-cobrancas"
---

# Tasks: Cartão de crédito, cobranças, faturas e recebimento do lojista

**Input**: Design documents from `specs/014-cartao-credito-cobrancas/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII): unitários (Domain, Application) e integração com
PostgreSQL e RabbitMQ em contêiner. Em cada fase os testes vêm antes da implementação e devem falhar
primeiro. Todo endpoint tem 401 (sem token e token inválido), 403 (perfil errado e claims ausentes) e
cada erro do contrato, com `ProblemDetails` e `traceId`.

**Organization**: a fundação reúne Domain, schema e infraestrutura comuns, porque a aprovação de um
pagamento (US3) toca cartão, cobrança, fatura e recebível na mesma transação. As histórias acrescentam
casos de uso, consultas e endpoints.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (`back/`); comandos `dotnet` rodam ali. `dom/` = `back/src/Bank.Domain/`,
`app/` = `back/src/Bank.Application/`, `infra/` = `back/src/Bank.Infrastructure/`, `api/` =
`back/src/Bank.Api/`, `worker/` = `back/src/Bank.Worker/`, `ut/` = `back/tests/Bank.UnitTests/`, `it/` =
`back/tests/Bank.IntegrationTests/`, `arq/` = `back/tests/Bank.ArchitectureTests/`. SQL em
`infra/Persistencia/Sql/`, com cabeçalho `-- Origem: specs/014-cartao-credito-cobrancas (...)`.

---

## Phase 1: Setup

**Purpose**: configuração nova (plan.md, "Configuração nova"), sem dependência nem serviço novos

- [X] T001 [P] Acrescentar em `docker-compose.yml` (serviço `api`) `Cartoes__ChaveDerivacao:
  ${CARTOES_CHAVE_DERIVACAO:-bank-dev-only-card-derivation-key-change-me-0123456789}`,
  `Cartoes__LimitePadrao: ${LIMITE_CARTAO_PADRAO:-5000.00}` e `Cartoes__JurosAtrasoMensalPercentual:
  ${JUROS_ATRASO_MENSAL:-15.00}`; documentar as três em `.env.example`, com o aviso de que trocar a chave
  muda todos os cartões (R1)
- [X] T002 [P] Acrescentar em `api/appsettings.json` a seção `Cartoes` (`LimitePadrao` 5000.00,
  `JurosAtrasoMensalPercentual` 15.00, sem chave) e em `worker/appsettings.json` a seção
  `ProcessamentoDiario` (`Habilitado` true, `IntervaloSegundos` 60); em `worker/appsettings.Testing.json`,
  `Habilitado` false
- [X] T003 Definir `Cartoes:ChaveDerivacao` aleatória em `it/BankApiFactory.cs` (propriedade
  `ChaveCartoes`, como `ChaveJwt`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain, schema, unidade de trabalho e apoio de endpoints usados por todas as histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Domain — testes

- [X] T004 [P] `ut/Domain/CalendarioBancarioTests.cs`: feriados fixos e móveis (vetores R12: 2027 — 08 e
  09/02, 26/03, 27/05), fim de semana, `ProximoDiaUtil(10/01/2027)` = 11/01/2027 e de dia útil = ela mesma,
  `SomarDiasUteis(24/11/2026, 30)` = 07/01/2027, partida em dia não útil
- [X] T005 [P] `ut/Domain/DadosDeCartaoTests.cs`: `NumeroCartao` (16 dígitos, Luhn, espaços aceitos, erro
  por campo, `ToString()` mascarado), `Cvv` (3 dígitos; 2, 4 e letras recusados; `ToString()` = `***`),
  `ValidadeCartao` (`MM/AA`, mês 13, vence no fim do mês), `CodigoAutorizacao` (6 de `[A-Z0-9]`), `Bandeira`
  (001/002, código desconhecido)
- [X] T006 [P] `ut/Domain/CartaoTests.cs`: emissão (validade = mês + 60 meses, nome em maiúsculas, limite
  copiado, utilizado 0), consumir até o limite (inclusivo), recusar acima, liberar, nunca negativo;
  `AutorizadorBandeira` na ordem cartão não autorizado → fatura vencida → limite insuficiente (R11)
- [X] T007 [P] `ut/Domain/FaturaTests.cs`: `CompetenciaFatura` (07/12 → 2026-12; 08/12 → 2027-01; virada do
  ano), vencimento em 11/01/2027, `Situacao` em cada data (07, 08 sem liberar, liberada, vencimento, dia
  seguinte, paga), lançar soma e recusa compra de outra fatura ou fatura liberada, liberar só após o dia
  07, liberação depois do vencimento muda o vencimento para o dia da liberação, pagar só `Disponivel` ou
  `Vencida`; `JurosAtraso`: 1.000,00 × 10 dias = 50,00; 299,90 × 1 dia = 1,50; 0 dias = 0; valores do
  pagamento não mudam com outra taxa
- [X] T008 [P] `ut/Domain/CobrancaTests.cs`: emissão (valor e descrição; limites 0,01/0/negativo/3 casas,
  descrição vazia, só espaços, 200 e 201), transições permitidas e proibidas, recusa mantém `Pendente` com
  motivo e instante; `PagamentoCobranca` produz cobrança paga, limite consumido, autorização, compra,
  recebível e evento coerentes
- [X] T009 [P] `ut/Domain/RecebivelTests.cs`: data prevista, liquidar só na data ou depois, antecipar só
  antes (no dia previsto recusa), taxa 299,90 → 6,00/293,90; 0,01 → 0,00/0,01; 100,00 → 2,00/98,00; estados
  finais; `ut/Domain/LancamentoCartaoTests.cs`: três novas fábricas, direção e descrição truncada em 200
- [X] T010 [P] Ampliar `ut/Domain/PermissoesPorPerfilTests.cs` e `ut/Api/PermissaoRequirementHandlerTests.cs`
  com `UsarCartaoCredito` (só PF) e `GerenciarRecebiveis` (só PJ)

### Domain — implementação

- [X] T011 [P] Criar `dom/Calendario/CalendarioBancario.cs` (R12; FR-022a): `Feriados(int ano)` com cache,
  `EhDiaUtil`, `ProximoDiaUtil`, `SomarDiasUteis`
- [X] T012 [P] Criar os 7 identificadores (data-model, "Identificadores tipados novos") no padrão de
  `TransferenciaId`: em `dom/Cartoes/` `CartaoId`, `AutorizacaoCartaoId`, `CompraCartaoId`, `FaturaId`,
  `LancamentoFaturaId`; em `dom/Cobrancas/` `CobrancaId`; em `dom/Recebiveis/` `RecebivelId`
- [X] T013 [P] Criar em `dom/Cartoes/` `Bandeira.cs` (`Codigo` 3, `Nome` até 30; `Visa` "001", `Mastercard`
  "002"; `Todas`, `DeCodigo`), `NumeroCartao.cs`, `Cvv.cs`, `ValidadeCartao.cs` e `CodigoAutorizacao.cs`
  (`RandomNumberGenerator`), com `ToString()` mascarado (R2) e erros de validação nos campos
  `numeroCartao`, `cvv` e `validade`, sem repetir o valor
- [X] T014 Criar `dom/Cartoes/Cartao.cs`, `AutorizacaoCartao.cs`, `AutorizadorBandeira.cs` e
  `ErrosCartao.cs` (`cartao-ja-existente` conflito, `cartao-nao-encontrado`, `cartao-nao-autorizado`,
  `cartao-bloqueado-fatura-vencida` e `limite-cartao-insuficiente` regra de negócio). `LimiteTotal` > 0,
  0 ≤ `LimiteUtilizado` ≤ `LimiteTotal`, `NomeImpresso` 1–150, `UltimosDigitos` 4, `Variacao` 0–4
- [X] T015 Criar `dom/Cartoes/CompetenciaFatura.cs`, `JurosAtraso.cs`, `CompraCartao.cs`,
  `LancamentoFatura.cs`, `Fatura.cs` e `ErrosFatura.cs` (`fatura-nao-encontrada`, `fatura-nao-disponivel`
  conflito, `saldo-insuficiente` com mensagem própria) conforme data-model e R5/R13; `Estabelecimento`
  1–150, `Descricao` 1–200
- [X] T016 Criar `dom/Recebiveis/Recebivel.cs` e `ErrosRecebivel.cs` (`recebivel-nao-encontrado`,
  `recebivel-nao-antecipavel` conflito): `PrazoDiasUteis = 30`, `TaxaPercentual = 2`, arredondamento
  `AwayFromZero`, `ValorLiquido = ValorBruto − Taxa` > 0
- [X] T017 Criar `dom/Cobrancas/Cobranca.cs`, `CobrancaPaga.cs` (evento, `Tipo` "cobranca.paga", `Versao` 1,
  com `LojistaNome`), `PagamentoCobranca.cs` (FR-018) e `ErrosCobranca.cs` (`cobranca-nao-encontrada`,
  `cobranca-nao-pendente` conflito)
- [X] T018 Ampliar `dom/Transferencias/Lancamento.cs` (`FaturaId?`, `RecebivelId?`, `TipoLancamento`
  `PagamentoFatura` = 3, `LiquidacaoRecebivel` = 4, `AntecipacaoRecebivel` = 5 e as três fábricas com as
  descrições de [contracts/transferencias-api.md](contracts/transferencias-api.md)) e
  `dom/Permissoes/Permissao.cs` / `PermissoesPorPerfil.cs` (R16)

### Schema e persistência

- [X] T019 [P] Criar `Sql/Procedures/ImpedirAlteracaoDeRegistrosDeCartao.sql` (triggers em
  `autorizacoes_cartao`, `compras_cartao`, `lancamentos_fatura`, reutilizando
  `impedir_alteracao_de_registro_imutavel`) e `RemoverImpedimentoDeRegistrosDeCartao.sql`; criar
  `Sql/Consultas/BloquearCobranca.sql`, `BloquearCartao.sql` (por `@usuario_id` ou `@id`) e
  `BloquearRecebivel.sql` (todas as colunas mapeadas, `FOR UPDATE`, R8); incluir os arquivos em
  `arq/SqlEmbutidoTests.cs`
- [X] T020 Criar em `infra/Persistencia/Configuracoes/` as configurações de `Bandeira` (`HasData`),
  `Cartao`, `Cobranca`, `AutorizacaoCartao`, `Fatura`, `CompraCartao`, `LancamentoFatura` e `Recebivel`, com
  todos os `CHECK`, FKs (RESTRICT), únicos e índices parciais da tabela "PostgreSQL" do data-model; ajustar
  `LancamentoConfiguracao.cs` (`ck_lancamentos_tipo`, `ck_lancamentos_origem_coerente`,
  `ux_lancamentos_fatura_id`, `ux_lancamentos_recebivel_id`); em `dom/Notificacoes/TipoNotificacao.cs`,
  `ComprovanteCobranca` = 3 e `CobrancaPaga` = 4, com conversor e `CHECK` (`comprovante_cobranca`,
  `cobranca_paga`) em `NotificacaoAuditoriaConfiguracao.cs`; `DbSet`s e conversores dos 7 ids em
  `BankDbContext.cs`
- [X] T021 Gerar a migration `AdicionarCartoesCobrancasERecebiveis` (`dotnet ef migrations add`), que aplica
  o SQL de T019 no `Up` e o remove no `Down`; conferir que `Down` + `Up` funciona com dados anteriores
- [X] T022 Criar `it/RestricoesCartoesTests.cs`: cada `CHECK`, único e FK recusa dado inválido gravado por
  SQL; `UPDATE`/`DELETE` nas três tabelas imutáveis falham; lançamentos e saldos anteriores à migration
  intactos; `Down` + `Up`
- [X] T023 Criar `app/Abstracoes/IUnidadeDeTrabalho.cs` (`IniciarAsync`, `ConfirmarAsync` →
  `Confirmacao(Ok | Conflito, Restricao)`, `DesfazerAsync`) e `infra/Persistencia/UnidadeDeTrabalho.cs`
  (`ReadCommitted`, `ChangeTracker.Clear` ao desfazer, R9), e `app/Abstracoes/ICarteiraRepositorio.cs` com
  `BloquearAsync(CarteiraId)` + `AdicionarLancamento`, reutilizando
  `BloquearCarteirasParaTransferencia.sql`; registrar em `AddPersistencia`

### Cartão derivado, API e apoio

- [X] T024 [P] `ut/Infrastructure/DadosCartaoHmacTests.cs`: mesma entrada → mesmos dados; prefixo e Luhn por
  bandeira; CVV com 3 dígitos (zeros à esquerda); variação, cartão ou chave diferentes mudam o resultado;
  impressão digital não contém o número; chave com menos de 32 bytes recusada na partida; divergência com a
  impressão gravada lança erro sem dados do cartão
- [X] T025 Criar `app/Abstracoes/IDadosCartao.cs` e, em `infra/Seguranca/`, `DadosCartaoHmac.cs` e
  `ChaveCartoesOptions.cs` (`ValidateOnStart`); registrar só em `AddInfrastructure` (API) (R1)
- [X] T026 [P] Criar `app/Cartoes/CartoesOptions.cs` + validador (`LimitePadrao` > 0 com 2 casas; juros entre
  0 e 100) com `ut/Application/CartoesOptionsTests.cs`; registrar em `AddApplication`
- [X] T027 [P] Criar `app/Observabilidade/MetricasCartoes.cs` (meter `Bank.Cartoes`) e
  `MetricasProcessamento.cs` (meter `Bank.Processamento`) com os instrumentos da tabela do plan; incluir os
  meters em `api/Observability/` e `worker/Observability/TelemetriaWorker.cs`
- [X] T028 [P] Extrair para `app/Comum/` a leitura comum de `pagina`/`tamanhoPagina` (padrão 20, máximo 100) e
  de `Idempotency-Key`, usando-a nos casos novos sem mudar mensagens nem comportamento dos atuais; em
  `api/Endpoints/`, compartilhar a marcação OpenAPI do cabeçalho; títulos dos 11 novos códigos de erro
  (T014–T017, exceto `saldo-insuficiente`, que já existe) em `api/ErrorHandling/ResultadoHttpExtensions.cs`
- [X] T029 [P] Criar `it/ApiCartoes.cs` (ajudantes: criar PF com cartão, PJ, cobrança, pagar, ler fatura,
  antecipar) e `it/CartoesApiFactory.cs` (relógio ajustável, saldo inicial PF de
  R$ 5.000,00 e, a partir da US4, `AddProcessamento`, para chamar `ProcessarDia` sem o Worker; o Worker fica
  só no teste do serviço hospedado)
- [X] T030 Rodar `dotnet build` e toda a suíte anterior; ajustar só o que a mudança de `Lancamento` exigir

**Checkpoint**: Domain completo, banco migrado, suíte anterior verde

---

## Phase 3: User Story 1 - Solicitar o cartão de crédito (Priority: P1) 🎯 MVP

**Goal**: a PF solicita o cartão escolhendo a bandeira, consulta limite e exibe os dados completos

**Independent Test**: solicitar com 001, conferir limite de R$ 5.000,00, repetir e receber 409

### Tests

- [X] T031 [P] [US1] `ut/Application/SolicitarCartaoTests.cs`: bandeira ausente/desconhecida, já existente,
  colisão de impressão digital incrementa `variacao` (até 5 tentativas), conflito do único de usuário vira
  `cartao-ja-existente`, limite copiado da configuração
- [X] T032 [P] [US1] `it/CartoesTests.cs`: cenários 1–8 da US1; 50 solicitações simultâneas → 1 cartão
  (SC-003); `GET /meu` sem número nem CVV; `/meu/dados` sempre igual e com `Cache-Control: no-store`; PF sem
  cartão 404; PJ 403; 401; mudança do limite configurado não altera cartão existente
- [X] T033 [P] [US1] `it/OpenApiTests.cs`: três rotas de cartão e respostas documentadas

### Implementation

- [X] T034 [US1] Criar `app/Abstracoes/ICartaoRepositorio.cs` (adicionar, obter por usuário sem rastreio,
  bloquear por usuário e por id, total a lançar) e `infra/Persistencia/CartaoRepositorio.cs`
- [X] T035 [US1] Criar `app/Cartoes/SolicitarCartao.cs`, `ObterMeuCartao.cs` e `ExibirDadosCartao.cs`
  (FR-002 a FR-008; logs só com `CartaoId` e bandeira)
- [X] T036 [US1] Criar `api/Endpoints/CartoesEndpoints.cs` (contrato; `no-store` em `/meu/dados`; resposta
  com dados crus com `ToString` mascarado) e mapear em `api/Program.cs`

**Checkpoint**: US1 funcional

---

## Phase 4: User Story 2 - Emitir e acompanhar cobranças (Priority: P1)

**Goal**: a PJ emite, consulta, lista e cancela; qualquer autenticado consulta pelo identificador

**Independent Test**: emitir, consultar como PF e como PJ, listar com filtro e cancelar

### Tests

- [X] T037 [P] [US2] `ut/Application/CobrancasTests.cs`: emissão, visibilidade por papel (emissor, pagador,
  terceiro) de `pagamento` e `ultimaRecusa`, lista paginada, cancelamento próprio e alheio
- [X] T038 [P] [US2] `it/CobrancasTests.cs`: cenários 1–8 da US2, `cobrancaId` inválido 400, filtro `status`
  inválido 400, 401/403, identidade do pagador ausente, rotas no OpenAPI

### Implementation

- [X] T039 [US2] Criar `app/Abstracoes/ICobrancaRepositorio.cs` (adicionar, bloquear) e
  `ICobrancaConsultas.cs` (por id com lojista, página do emissor) e as implementações em
  `infra/Persistencia/`
- [X] T040 [US2] Criar `app/Cobrancas/EmitirCobranca.cs`, `ConsultarCobranca.cs`,
  `ListarMinhasCobrancas.cs` e `CancelarCobranca.cs` (FR-009 a FR-013; cancelamento com a cobrança bloqueada)
- [X] T041 [US2] Criar `api/Endpoints/CobrancasEndpoints.cs` com as quatro rotas (literal `/minhas` antes de
  `/{cobrancaId}`) e mapear em `api/Program.cs`

**Checkpoint**: US1 e US2 funcionais

---

## Phase 5: User Story 3 - Pagar uma cobrança com o cartão (Priority: P1)

**Goal**: pagamento autorizado pela bandeira, atômico e idempotente, com evento no outbox

**Independent Test**: limite 5.000,00; pagar 299,90 (aprovada) e tentar 4.700,11 (recusada)

### Tests

- [X] T042 [P] [US3] `ut/Application/PagarCobrancaTests.cs`: validação acumulada por campo, ordem das
  verificações do contrato, dados de terceiro/CVV/validade errados e sem cartão → mesmo erro, repetição da
  chave, divergência (outra cobrança, outros dados), recusa gravada e chave não consumida, corrida no único
  da chave, compra com fatura já liberada vai ao ciclo seguinte (R6), `ToString()` do comando sem dados
- [X] T043 [P] [US3] `it/PagamentoCobrancaTests.cs`: cenários 1–13 da US3 (o 13 com fatura vencida preparada
  por SQL no arranjo), validade vencida, limite inclusivo, saldos inalterados, recebível e compra criados,
  evento único no outbox com o contrato, cada erro do contrato, 401/403
- [X] T044 [P] [US3] `it/ConcorrenciaPagamentoTests.cs`: 50 pagamentos da mesma cobrança → 1 aprovado; duas
  cobranças que excedem o limite → 1 aprovada; cancelar × pagar → 1 vence; mesma chave simultânea → 1 efeito;
  limite disponível = total − compras não quitadas (SC-003, SC-004)
- [X] T045 [P] [US3] Ampliar `ut/Infrastructure/ContratoEventosTests.cs` (`lojistaNome` opcional, campos e
  formato) e `it/DadosSensiveisCartaoTests.cs`: após emitir, exibir, pagar e recusar, nenhum número ou CVV em
  tabelas (todas as colunas de texto/json), logs, spans, métricas, outbox e corpos de erro (SC-009)

### Implementation

- [X] T046 [US3] Criar `app/Abstracoes/IFaturaRepositorio.cs` (obter ou abrir por cartão e competência,
  existe vencida, adicionar compra), `IRecebivelRepositorio.cs` (adicionar) e as implementações em
  `infra/Persistencia/`; em `ICobrancaRepositorio`, autorização por chave e adicionar autorização
- [X] T047 [US3] Ajustar `infra/Mensageria/Contratos/CobrancaPagaV1.cs` (`LojistaNome`) e criar a sobrecarga
  `EventoOutbox.De(CobrancaPaga)`; a gravação fica no repositório de cobranças
- [X] T048 [US3] Criar `app/Cobrancas/PagarCobranca.cs` (FR-014 a FR-021): validar, repetição, iniciar,
  bloquear cobrança e cartão, repetição de novo, ler o relógio, decidir, gravar aprovação ou recusa,
  confirmar; instante em microssegundos; métrica `bank.cobrancas.autorizacoes` e logs sem dados do cartão
- [X] T049 [US3] Acrescentar `POST /{cobrancaId}/pagar` em `api/Endpoints/CobrancasEndpoints.cs` (requisição
  com `ToString` mascarado; 200 na primeira vez e na repetição)
- [X] T050 [US3] Medir p95 do pagamento com 100 pagamentos sequenciais em `it/PagamentoCobrancaTests.cs`
  (≤ 2 s, SC-002)

**Checkpoint**: MVP completo (US1–US3)

---

## Phase 6: User Story 4 - Lançar compras e consultar faturas (Priority: P2)

**Goal**: processamento diário idempotente no Worker e consulta de faturas

**Independent Test**: compras ao redor do dia 07 com relógio controlado; fatura e disponibilidade corretas

### Tests

- [X] T051 [P] [US4] `ut/Application/ProcessarDiaTests.cs`: dia não útil não faz nada; ordem lançar → liberar
  → liquidar; compra de hoje não entra; falha em um cartão não para os demais e conta em `falhas`; repetição
  sem efeito
- [X] T052 [P] [US4] `it/ProcessamentoDiarioTests.cs`: cenários 1–8 e 11 da US4; 07 às 23:59:59 e 08 à 00:00
  (Brasília); domingo 07/03/2027; virada do ano; compra não confirmada segura o cartão (R6); duas execuções
  simultâneas; interrupção no meio; dias perdidos; liberação depois do vencimento; serviço hospedado ligado
  dispara na partida e à 00:00
- [X] T053 [P] [US4] `it/FaturasTests.cs`: cenários 9 e 10 da US4, fatura atual sem compras (total 0),
  competência inválida 400, inexistente 404, PF sem cartão 404, paginação, 401/403, OpenAPI
- [X] T054 [P] [US4] `it/VolumeProcessamentoTests.cs`: 10.000 compras em 1.000 cartões (dados por SQL) em
  ≤ 5 min, cada uma lançada uma vez (SC-005, SC-007)

### Implementation

- [X] T055 [US4] Acrescentar a `IFaturaRepositorio` os cartões com fatura a processar, as faturas não
  liberadas com compras a lançar e adicionar lançamento; criar `app/Abstracoes/IFaturaConsultas.cs` (lista e
  detalhe) e `infra/Persistencia/FaturaConsultas.cs`
- [X] T056 [US4] Criar `app/Processamento/ProcessarDia.cs` (etapas lançar e liberar, uma transação por
  cartão, com o cartão bloqueado) e `AddProcessamento` em `app/DependencyInjection.cs` (R7)
- [X] T057 [US4] Criar `infra/Processamento/ProcessamentoDiarioOptions.cs` (`IntervaloSegundos` ≥ 1) e
  `ProcessamentoDiarioHospedado.cs` (partida, 00:00 de Brasília e intervalo; erro não encerra o serviço; span
  só quando há trabalho); registrar em `worker/Program.cs` com a validação do fuso na partida
- [X] T058 [US4] Criar `app/Faturas/ListarFaturas.cs` e `ObterFatura.cs` (fatura atual virtual sem compras) e
  `api/Endpoints/FaturasEndpoints.cs` com os dois `GET`; mapear em `api/Program.cs`

**Checkpoint**: compras aparecem na fatura do ciclo certo

---

## Phase 7: User Story 5 - Pagar a fatura (Priority: P2)

**Goal**: pagamento total com saldo, juros por atraso e liberação do limite

**Independent Test**: fatura de R$ 299,90 disponível; pagar e conferir saldo, extrato, status e limite

### Tests

- [X] T059 [P] [US5] `ut/Application/PagarFaturaTests.cs`: disponível, vencida com juros, saldo insuficiente,
  aberta/fechada/paga, repetição e divergência da chave, limite liberado sem os juros
- [X] T060 [P] [US5] `it/PagamentoFaturaTests.cs`: cenários 1–7 da US5; dois pagamentos simultâneos → 1
  débito; não conta no limite diário de transferência (FR-029); compra bloqueada por fatura vencida e aprovada
  depois de pagar (US3, cenários 13 e 14); no dia do vencimento não bloqueia; taxa guardada não muda com a
  configuração; saldo = razão; cada erro do contrato; 401/403

### Implementation

- [X] T061 [US5] Criar `app/Faturas/PagarFatura.cs` (FR-028, FR-028a: bloquear cartão e carteira, calcular,
  debitar, lançar, liberar limite) com métrica `bank.faturas.pagamentos` e `POST
  /faturas/{competencia}/pagar` em `api/Endpoints/FaturasEndpoints.cs`; em `IFaturaRepositorio`, fatura por
  chave

**Checkpoint**: ciclo da PF completo

---

## Phase 8: User Story 6 - Lojista recebe ou antecipa (Priority: P2)

**Goal**: recebíveis consultáveis, liquidados na data prevista ou antecipados com taxa de 2%

**Independent Test**: antecipar R$ 299,90 → R$ 293,90; outra cobrança liquidada na data prevista

### Tests

- [X] T062 [P] [US6] `ut/Application/RecebiveisTests.cs`: lista e detalhe próprios, antecipação, repetição,
  divergência, estados finais, dia previsto; `ProcessarDia` etapa liquidar com falha isolada
- [X] T063 [P] [US6] `it/RecebiveisTests.cs`: cenários 1–8 da US6; antecipar × liquidar simultâneos → 1
  crédito; mesma chave simultânea; processamento parado na data prevista; saldo = razão e extrato; filtros e
  erros do contrato; 401/403; OpenAPI (SC-008)

### Implementation

- [X] T064 [US6] Acrescentar a `IRecebivelRepositorio` bloquear, por chave e devidos até a data; criar
  `app/Abstracoes/IRecebivelConsultas.cs` e `infra/Persistencia/RecebivelConsultas.cs`
- [X] T065 [US6] Criar `app/Recebiveis/ListarRecebiveis.cs`, `ObterRecebivel.cs` e `AnteciparRecebivel.cs`
  (FR-032, FR-033; bloquear recebível e carteira) e `api/Endpoints/RecebiveisEndpoints.cs`; mapear em
  `api/Program.cs`
- [X] T066 [US6] Acrescentar a etapa liquidar em `app/Processamento/ProcessarDia.cs` (FR-031, uma transação
  por recebível)

**Checkpoint**: ciclo do lojista completo

---

## Phase 9: User Story 7 - Notificar o pagamento da cobrança (Priority: P3)

**Goal**: consumo de `cobranca.paga` com duas notificações registradas

**Independent Test**: pagar uma cobrança e conferir as duas notificações do evento

### Tests

- [X] T067 [P] [US7] `ut/Infrastructure/LeitorCobrancaPagaTests.cs` (campos obrigatórios, versão, tipo,
  `lojistaNome` ausente), `ut/Domain/NotificacaoAuditoriaTests.cs` (textos do contrato) e
  `ut/Application/NotificarCobrancaPagaTests.cs`; ajustar `ut/Infrastructure/TopologiaMensageriaTests.cs`
- [X] T068 [P] [US7] `it/NotificacoesCobrancaTests.cs`: cenários 1–3 da US7 ponta a ponta (API → outbox →
  Worker), reentrega, mensagem inválida na DLQ, falha transitória passa pela espera, trace contínuo, texto sem
  dados do cartão; ajustar `it/TopologiaTests.cs` (filas de espera de cobranças) e a prontidão do Worker

### Implementation

- [X] T069 [US7] Criar em `dom/Notificacoes/NotificacaoAuditoria.cs` as fábricas `ComprovanteCobranca` e
  `CobrancaPaga`, com os textos de [contracts/evento-cobranca-paga.md](contracts/evento-cobranca-paga.md)
- [X] T070 [US7] Criar `app/Abstracoes/INotificadorCobranca.cs`, `app/Notificacoes/NotificarCobrancaPaga.cs`,
  `infra/Mensageria/LeitorCobrancaPaga.cs` e `ConsumidorNotificacoesCobranca.cs`; `ComFilasEspera: true` em
  `TopologiaMensageria.Cobrancas`; registrar em `AddNotificacoes` e `AddMensageria`

**Checkpoint**: todas as histórias funcionais

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T071 [P] Frontend (FR-038, R14): testes e rótulos em `src/frontend/src/paginas/lancamentos.ts`
  (`nomeContraparte`; `NOME_ABERTURA` só para `Abertura`; desconhecido = "Lançamento"), `Inicio.tsx`
  (`detalhePf`) e `Extrato.tsx` (mobile); conferir desktop e mobile; `npm run lint`, `typecheck` e `test`
- [X] T072 [P] `it/RecebiveisTests.cs` e `it/PagamentoFaturaTests.cs`: novos tipos com `contraparte` e `transferenciaId` nulos e filtro de
  direção (extrato 1.3.0)
- [X] T073 [P] `it/DadosSensiveisCartaoTests.cs` (API) e `it/ProcessamentoDiarioTests.cs` (Worker): instrumentos e tags da tabela do plan na API e no Worker, sem
  dados pessoais nem de cartão (FR-037)
- [X] T074 [P] Atualizar `docs/desenvolvimento.md` (chave, configuração, endpoints, processamento diário,
  métricas) e o histórico de `specs/009-limite-transferencia/contracts/transferencias-api.md`,
  `specs/010-mensageria-rabbitmq/contracts/evento-cobranca-paga.md` e `topologia-e-mensagens.md` e
  `specs/011-notificacoes-auditoria/contracts/notificacoes-auditoria.md`, apontando para a 014
- [X] T075 Rodar `dotnet build` sem avisos e `dotnet test` completo; reler `spec.md`, `plan.md`,
  `data-model.md` e contratos e corrigir divergências com o código (Princípio I)
- [X] T076 Executar o [quickstart.md](quickstart.md) no Compose (cenários 1, 2, 3, 5 e 6, e a parte do 4 que
  não depende da virada) e `npm run e2e`

---

## Dependencies & Execution Order

- **Setup (T001–T003)** → **Foundational (T004–T030)** → histórias.
- **US1** e **US2** são independentes entre si.
- **US3** depende de US1 e US2.
- **US4** depende de US3; **US5** depende de US4.
- **US6** depende de US3 e, na etapa liquidar (T066), de T056.
- **US7** depende de US3 (T047) e pode correr em paralelo com US4–US6.
- **Polish** depois das histórias.

Dentro de cada fase: testes → abstrações e persistência → casos de uso → endpoints. Na fundação:
T011–T013 → T014 → T015 → T016 → T017 → T018 → T020 → T021 → T022; T019 antes de T021; T023 depois de T020.

## Parallel Opportunities

- Fundação: T004–T010 (testes de arquivos distintos); T011, T012 e T013; T024, T026, T027, T028 e T029.
- US1 e US2 em paralelo, salvo `api/Program.cs`.
- Depois da US3: US4, US7 e a parte de consulta e antecipação da US6 (T062–T065).

```text
# Exemplo (US3), testes em paralelo
T042 ut/Application/PagarCobrancaTests.cs
T043 it/PagamentoCobrancaTests.cs
T044 it/ConcorrenciaPagamentoTests.cs
T045 ut/Infrastructure/ContratoEventosTests.cs + it/DadosSensiveisCartaoTests.cs
```

## Implementation Strategy

- **MVP**: Setup + Foundational + US1 + US2 + US3 (cartão, cobrança e pagamento autorizado).
- **Incrementos**: US4 (faturas) → US5 (pagamento) → US6 (recebíveis) → US7 (notificações) → Polish.
- Um commit por fase ou por passo coerente, em Conventional Commits (`feat(cartao-credito): ...`), com a
  suíte verde em cada um.
