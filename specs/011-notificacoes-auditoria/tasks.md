---

description: "Lista de tarefas da feature 011-notificacoes-auditoria"
---

# Tasks: Notificações de transferência persistidas (auditoria)

**Input**: Design documents from `specs/011-notificacoes-auditoria/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/notificacoes-auditoria.md](contracts/notificacoes-auditoria.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII; FR-012, FR-013): unitários (Domain, caso de
uso, leitor) e integração contra PostgreSQL e RabbitMQ em contêiner. Em cada história, os testes
são escritos primeiro e devem falhar antes da implementação. Exceção: T020 verifica restrições
do esquema criado na fundação (T008) e já nasce verde.

**Organization**: fundação comum (entidade, abstração, tabela, migration e ajuste dos dados de
teste da 010); depois US1 (duas notificações por transferência), US2 (duplicatas) e US3 (falha,
rollback, DLQ e reprocessamento); por fim, documentação e validação.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (abreviado `back/`): `back/src/Bank.*` e `back/tests/Bank.*`; comandos
`dotnet` rodam em `src/backend/`. Testes de integração em `back/tests/Bank.IntegrationTests/`
(abreviado `it/`).

---

## Phase 1: Setup

**Purpose**: nada a preparar: sem projeto, pacote, serviço de Compose ou configuração novos
(plan.md, "Primary Dependencies: nenhuma dependência nova"). A branch `011-notificacoes-auditoria`
já existe.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: modelo de domínio, abstração, mapeamento, migration e dados de teste que todas as
histórias usam

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

- [X] T001 [P] Criar `back/src/Bank.Domain/Notificacoes/NotificacaoAuditoriaId.cs`:
  `public readonly record struct NotificacaoAuditoriaId(Guid Valor) : IIdentificador<NotificacaoAuditoriaId>`
  com `De(Guid)`, `Novo(DateTimeOffset agora)` (`Guid.CreateVersion7(agora)`) e `ToString()`, no
  mesmo formato de `back/src/Bank.Domain/Usuarios/UsuarioId.cs`
- [X] T002 [P] Criar `back/src/Bank.Domain/Notificacoes/TipoNotificacao.cs`
  (`enum TipoNotificacao { ComprovanteEnvio, AvisoCredito }`) e
  `back/src/Bank.Domain/Notificacoes/CanalNotificacao.cs` (`enum CanalNotificacao { Simulado }`),
  com comentário XML citando `specs/011-notificacoes-auditoria` (data-model)
- [X] T003 [P] Escrever `back/tests/Bank.UnitTests/Domain/NotificacaoAuditoriaTests.cs` (deve
  falhar): `ComprovanteEnvio` com nome → `Tipo` `ComprovanteEnvio`, `UsuarioDestinoId` = pagador,
  `Canal` `Simulado`, `Mensagem` `"Você enviou R$ 150,00 para Maria Souza."`; `AvisoCredito` com
  nome → `"Você recebeu R$ 150,00 de João Silva."`; nome `null`, `""` ou `"   "` →
  `"Você enviou R$ 150,00."` / `"Você recebeu R$ 150,00."`; formato do valor `0.01m` → `0,01`,
  `1234.5m` → `1.234,50`, `1234567.89m` → `1.234.567,89`; nome `"  Maria Souza  "` sem espaços
  nas extremidades no texto; `Id` não vazio e `CriadoEm` igual ao `agora` recebido; mesmo
  resultado com `CultureInfo.CurrentCulture` trocada (o formato não depende da cultura — R5)
- [X] T004 Criar `back/src/Bank.Domain/Notificacoes/NotificacaoAuditoria.cs` (research R1, R5):
  propriedades só de leitura `Id`, `EventoId`, `UsuarioDestinoId` (`UsuarioId`), `Tipo`, `Canal`,
  `Mensagem`, `CriadoEm`; construtor privado sem parâmetros para o EF Core; fábricas
  `ComprovanteEnvio(EventoId eventoId, UsuarioId pagador, decimal valor, string? recebedorNome, DateTimeOffset agora)`
  e `AvisoCredito(EventoId eventoId, UsuarioId recebedor, decimal valor, string? pagadorNome, DateTimeOffset agora)`
  com `Canal = Simulado` e `Id = NotificacaoAuditoriaId.Novo(agora)`; textos exatamente como na
  tabela de [contracts/notificacoes-auditoria.md](contracts/notificacoes-auditoria.md); valor
  formatado com `NumberFormatInfo` próprio (clone de `CultureInfo.InvariantCulture.NumberFormat`
  com `NumberDecimalSeparator = ","` e `NumberGroupSeparator = "."`) e padrão `"#,##0.00"`, nunca
  `CultureInfo("pt-BR")` (`InvariantGlobalization=true`); constante pública
  `TamanhoMaximoMensagem = 500` e `TamanhoMaximoTipo`/`TamanhoMaximoCanal = 30`; sem métodos de
  alteração (FR-007). T003 passa
- [X] T005 [P] Criar `back/src/Bank.Application/Abstracoes/INotificacoesAuditoria.cs` (research R2):
  `Task<bool> ExistemParaEventoAsync(EventoId eventoId, CancellationToken cancellationToken)` e
  `Task AdicionarAsync(IReadOnlyCollection<NotificacaoAuditoria> notificacoes, CancellationToken cancellationToken)`;
  comentário XML: implementação grava no `DbContext` do escopo, dentro da transação do consumidor,
  sem transação própria
- [X] T006 Criar `back/src/Bank.Infrastructure/Persistencia/Configuracoes/NotificacaoAuditoriaConfiguracao.cs`
  (data-model, research R7): tabela `notificacoes_auditoria`; PK `pk_notificacoes_auditoria (id)`;
  colunas `id` (uuid, `ValueGeneratedNever`), `evento_id` (uuid, obrigatório),
  `usuario_destino_id` (uuid, obrigatório), `tipo` (varchar(30), obrigatório, conversor enum ↔
  `"comprovante_envio"`/`"aviso_credito"`), `canal` (varchar(30), obrigatório, conversor enum ↔
  `"simulado"`), `mensagem` (varchar(500), obrigatório), `criado_em` (timestamptz, obrigatório);
  índice único `ux_notificacoes_auditoria_evento_tipo (evento_id, tipo)`; FK
  `fk_notificacoes_auditoria_usuarios_usuario_destino_id` para `Usuario` (`usuarios.id`) com
  `OnDelete(DeleteBehavior.Restrict)` e índice `ix_notificacoes_auditoria_usuario_destino_id`;
  checks `ck_notificacoes_auditoria_tipo` (`tipo IN ('comprovante_envio', 'aviso_credito')`) e
  `ck_notificacoes_auditoria_canal` (`canal IN ('simulado')`); sem navegação na entidade de domínio
  (FK só por `HasOne<Usuario>().WithMany().HasForeignKey(n => n.UsuarioDestinoId)`)
- [X] T007 Em `back/src/Bank.Infrastructure/Persistencia/BankDbContext.cs`: `DbSet<NotificacaoAuditoria> NotificacoesAuditoria`
  e, em `ConfigureConventions`,
  `configurationBuilder.Properties<NotificacaoAuditoriaId>().HaveConversion<IdentificadorConverter<NotificacaoAuditoriaId>>()`
  (depende de T001, T004, T006)
- [X] T008 Gerar a migration `AdicionarNotificacoesAuditoria`
  (`dotnet ef migrations add AdicionarNotificacoesAuditoria --project src/Bank.Infrastructure --startup-project src/Bank.Api`)
  e conferir que só cria `notificacoes_auditoria` com a PK, o índice único, a FK `RESTRICT`, o
  índice da FK e os 2 checks; o snapshot muda só nessa tabela (depende de T007)
- [X] T009 Criar `back/src/Bank.Infrastructure/Persistencia/NotificacoesAuditoriaRepositorio.cs`
  (`internal sealed`, implementa `INotificacoesAuditoria` com o `BankDbContext` injetado):
  `ExistemParaEventoAsync` = `AnyAsync(n => n.EventoId == eventoId)`; `AdicionarAsync` =
  `AddRange` + `SaveChangesAsync` (sem abrir transação: usa a do consumidor — R2); registrar como
  scoped em `AddPersistencia` de `back/src/Bank.Infrastructure/DependencyInjection.cs` (depende de
  T005, T007)
- [X] T010 Dados de teste com usuários reais (a FK `RESTRICT` rejeitaria os ids aleatórios do
  `Payload` da spec 010): em `it/ConsumoIdempotenteTests.cs`, trocar
  `Payload(Guid eventoId)` por `Payload(Guid eventoId, Guid pagadorUsuarioId, Guid recebedorUsuarioId)`
  e criar um auxiliar `it/UsuariosDeTeste.cs` com
  `Task<(Guid Pagador, Guid Recebedor)> CriarParAsync(BankApiFactory api)` que
  cadastra uma PF e uma PJ pela API (`CriarPfAsync`/`CriarPjAsync`, com carteira, como
  usuários reais); atualizar os chamadores em
  `it/ConsumoIdempotenteTests.cs`, `it/NovaTentativaEDlqTests.cs` e
  `it/RastreamentoMensageriaTests.cs`; rodar esses testes e confirmar que continuam passando antes
  de seguir (depende de T008)

**Checkpoint**: `dotnet build` e `dotnet test` verdes; a tabela existe e nenhum comportamento mudou

---

## Phase 3: User Story 1 - Transferência concluída gera duas notificações registradas (Priority: P1) 🎯 MVP

**Goal**: cada `transferencia.concluida` consumido com sucesso grava o comprovante de envio (pagador)
e o aviso de crédito (recebedor), na transação de `eventos_processados`, com o texto do contrato

**Independent Test**: `POST /api/v1/transferencias` → após o consumo, exatamente 2 linhas em
`notificacoes_auditoria` para o `eventoId`, com tipos, destinatários, canal `simulado` e textos
esperados (quickstart, cenário 2)

### Testes da US1 (escrever primeiro)

- [X] T011 [P] [US1] Em `back/tests/Bank.UnitTests/Infrastructure/LeitorTransferenciaConcluidaTests.cs`:
  payload com `pagadorNome`/`recebedorNome` → `NotificacaoTransferencia` com os nomes; ausentes,
  `null`, `""` ou `"   "` → `null`, e a leitura continua válida (eventos antigos; R4)
- [X] T012 [P] [US1] Em `back/tests/Bank.UnitTests/Application/NotificarTransferenciaConcluidaTests.cs`,
  com um `INotificacoesAuditoria` em memória (lista + `ExistemParaEventoAsync` por `EventoId`) e
  `RelogioFixo` (de `back/tests/Bank.UnitTests/Application/Dubles.cs`): o caso de uso grava exatamente 2 notificações — `ComprovanteEnvio` para
  `PagadorUsuarioId` com o nome do recebedor no texto e `AvisoCredito` para `RecebedorUsuarioId`
  com o nome do pagador —, ambas com `EventoId` da notificação e `CriadoEm` = relógio; sem nomes →
  textos sem nome; o teste atual de log e métrica continua valendo (2 logs, sem nome nem texto nas
  propriedades; métrica por papel)
- [X] T013 [P] [US1] Em `it/PublicacaoOutboxTests.cs`, no teste `Transferencia_EPublicadaENotificadaUmaVez`:
  depois do processamento, ler `NotificacoesAuditoria` do `eventoId` pelo `CriarContexto()` e
  verificar 2 linhas — `comprovante_envio` para o pagador e `aviso_credito` para o recebedor —,
  canal `simulado`, textos `"Você enviou R$ {valor} para {nome do recebedor}."` /
  `"Você recebeu R$ {valor} de {nome do pagador}."` com os nomes cadastrados no teste, e `criado_em`
  preenchido e não anterior ao `ocorridoEm` do evento no outbox; renomear o teste para
  `Transferencia_EPublicadaENotificadaUmaVez_ComDuasNotificacoesRegistradas`

### Implementação da US1

- [X] T014 [US1] Em `back/src/Bank.Application/Abstracoes/INotificadorTransferencia.cs`: acrescentar a
  `NotificacaoTransferencia` os parâmetros finais `string? PagadorNome = null` e
  `string? RecebedorNome = null`; atualizar o comentário XML (os nomes entram para o texto
  persistido da notificação — spec 011 —, mas continuam fora de logs; FR-010)
- [X] T015 [US1] Em `back/src/Bank.Infrastructure/Mensageria/LeitorTransferenciaConcluida.cs`: ler
  `pagadorNome` e `recebedorNome` no `ConteudoLido` (opcionais; em branco → `null`) e passá-los ao
  `NotificacaoTransferencia`; não exigir os campos (T011 passa)
- [X] T016 [US1] Em `back/src/Bank.Application/Notificacoes/NotificarTransferenciaConcluida.cs`
  (research R3, R6): injetar `INotificacoesAuditoria` e `TimeProvider`; criar
  `NotificacaoAuditoria.ComprovanteEnvio(...)` e `NotificacaoAuditoria.AvisoCredito(...)` com
  `agora = relogio.GetUtcNow()`; `await AdicionarAsync([comprovante, aviso])`; só depois manter os
  2 logs `EventId` 5100 e `metricas.RegistrarEnviada(papel)` (sem nomes nem texto); atualizar o
  comentário da classe citando a spec 011 (T012 passa)
- [X] T017 [US1] Conferir que `back/src/Bank.Application/DependencyInjection.cs` (`AddNotificacoes`) e o
  `Bank.Worker` resolvem as novas dependências (`INotificacoesAuditoria` vem de `AddPersistencia`,
  `TimeProvider` já registrado); rodar `Worker_EmDevelopment_ResolveTodasAsDependencias` e T013

**Checkpoint**: US1 funcional; quickstart cenário 2

---

## Phase 4: User Story 2 - Entregas repetidas não duplicam as notificações (Priority: P1)

**Goal**: entregas repetidas ou simultâneas, e reentrega após ACK perdido, mantêm exatamente 2
notificações por evento (FR-006)

**Independent Test**: mesma mensagem 2 vezes e cópias simultâneas em dois Workers → 2 linhas e 1
`eventos_processados` (quickstart, cenário 3)

### Testes da US2 (escrever primeiro)

- [X] T018 [P] [US2] Em `back/tests/Bank.UnitTests/Application/NotificarTransferenciaConcluidaTests.cs`:
  o mesmo `NotificacaoTransferencia` processado 2 vezes pelo caso de uso → o repositório em memória
  tem 1 único par; a 2ª chamada não chama `AdicionarAsync`, não emite log 5100 nem métrica (R3;
  FR-012)
- [X] T019 [P] [US2] Em `it/ConsumoIdempotenteTests.cs`: em `MesmaMensagemDuasVezes_UmaNotificacao` e
  `CopiasSimultaneasEmDoisWorkers_UmaNotificacao`, além da contagem de logs, verificar
  exatamente 2 linhas em `notificacoes_auditoria` para o `eventoId` (uma de cada tipo); novo teste
  `NotificacoesJaGravadas_SemEventoProcessado_NaoDuplica`: inserir à mão o par de notificações do
  evento (sem linha em `eventos_processados`, simulando um defeito na 1ª camada), publicar a
  mensagem e verificar que continuam 2 linhas e que a mensagem foi confirmada como `sucesso`
- [X] T020 [P] [US2] Novo `it/NotificacoesAuditoriaTests.cs`, classe
  `NotificacoesAuditoriaIntegridadeTests` (`IClassFixture<BankApiFactory>`, só banco; a classe de
  falhas da T022 fica no mesmo arquivo, com outro ambiente):
  - `IndiceUnico_RejeitaSegundaNotificacaoDoMesmoTipo`: inserir pelo contexto uma
    `NotificacaoAuditoria` repetindo `(evento_id, tipo)` de uma existente e esperar
    `DbUpdateException` com `PostgresException.ConstraintName == "ux_notificacoes_auditoria_evento_tipo"`;
  - `UsuarioComNotificacoes_NaoPodeSerExcluido`: criar um usuário sem carteira direto no banco,
    gravar uma notificação para ele e esperar falha no `DELETE` com
    `ConstraintName == "fk_notificacoes_auditoria_usuarios_usuario_destino_id"`

### Implementação da US2

- [X] T021 [US2] Em `back/src/Bank.Application/Notificacoes/NotificarTransferenciaConcluida.cs`: no
  início, `if (await notificacoes.ExistemParaEventoAsync(eventoId))` → log `Debug`
  (`EventId` 5101, `"Notificações do evento {EventoId} já registradas; nada a gravar"`) e retorno,
  sem métrica (T018 e T019 passam)

**Checkpoint**: US1 e US2 funcionais; quickstart cenário 3

---

## Phase 5: User Story 3 - Falha no registro não deixa notificação pela metade (Priority: P2)

**Goal**: qualquer falha desfaz notificações e `eventos_processados`; a mensagem segue nova
tentativa/DLQ da spec 010 e, quando processada, gera as 2 notificações uma única vez

**Independent Test**: falha na 2ª notificação → nada gravado e nova tentativa; falha permanente →
DLQ sem linhas; reprocessamento da DLQ → 2 linhas (quickstart, cenário 4)

### Testes da US3 (escrever primeiro)

- [X] T022 [US3] Em `it/NotificacoesAuditoriaTests.cs`, classe `NotificacoesAuditoriaFalhasTests`
  com o ambiente `AmbienteNotificacoesControlaveis`
  (deriva de `AmbienteMensageria`, sobrescreve `ConfigurarServicosDoWorker` como
  `AmbienteNotificadorControlavel` em `it/NovaTentativaEDlqTests.cs`) com o dublê
  `NotificacoesAuditoriaQueFalham : INotificacoesAuditoria` (scoped, decora o repositório real):
  quando configurado para o `eventoId`, grava a 1ª notificação com `SaveChangesAsync` e lança
  `InvalidOperationException` antes da 2ª, nas N primeiras execuções ou sempre. Testes:
  - `FalhaNaSegundaNotificacao_DesfazTudo_ENovaTentativaGravaUmPar`: falha 2 vezes → ao final, 2
    linhas, 1 `eventos_processados`, `nova_tentativa` = 2;
  - `FalhaPermanente_DlqSemNotificacoes_ReprocessamentoGravaUmPar`: falha sempre → mensagem na DLQ
    com `x-motivo` `tentativas-esgotadas`, 0 linhas e nenhum `eventos_processados`; normalizar o
    dublê, mover a mensagem para a fila principal (como `MoverParaAFilaPrincipalAsync` da 010) →
    2 linhas e log com `Reprocessamentos=1`;
  - `DestinatarioInexistente_VaiParaDlq`: payload com `recebedorUsuarioId` que não existe em
    `usuarios` (sem dublê) → DLQ com `tentativas-esgotadas`, 0 linhas.
  Usar `BankWorkerFactory.AguardarAsync` para todo estado observado depois da DLQ/ACK (log, métrica,
  linhas), lição do PR #48

### Implementação da US3

- [X] T023 [US3] Sem código de produção novo esperado (atomicidade vem de R2: mesma transação do
  consumidor). Se algum teste de T022 falhar, corrigir a causa em
  `back/src/Bank.Infrastructure/Persistencia/NotificacoesAuditoriaRepositorio.cs` ou em
  `NotificarTransferenciaConcluida.cs`, nunca em `ConsumidorEventos.cs` sem atualizar a spec e o
  plano (Princípio I)

**Checkpoint**: todas as histórias funcionais; quickstart cenários 4 e 5

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T024 [P] Em `it/DadosSensiveisMensageriaTests.cs`: com uma transferência de nomes conhecidos, nenhum
  log, span ou tag de métrica do Worker contém os nomes nem o texto das notificações gravadas
  (FR-010)
- [ ] T025 [P] Atualizar a spec 010 (FR-011): em `specs/010-mensageria-rabbitmq/spec.md`, na
  Key Entity "Notificação de transferência", acrescentar que desde a spec 011 ela também é
  persistida em `notificacoes_auditoria`; em `specs/010-mensageria-rabbitmq/data-model.md`, na
  seção "Application — notificação", nota apontando os nomes opcionais e a persistência da spec 011
- [ ] T026 [P] Em `docs/desenvolvimento.md`, seção "Mensageria (Worker)",
  acrescentar a tabela `notificacoes_auditoria` e a consulta do quickstart, cenário 2
- [ ] T027 Rodar `dotnet build` e `dotnet test` em Release (como no CI) com o Docker em execução;
  depois, no Compose (`docker compose up -d --build --wait`), executar os cenários 2–5 do
  [quickstart.md](quickstart.md) e registrar o resultado no PR
- [ ] T028 Marcar na issue #21 os itens entregues pela spec 010 (PR #47) e os entregues aqui
  (2.1 `NotificacaoAuditoria` e migration; testes 3.1 de idempotência e 3.2 ponta a ponta) e
  abrir o PR com `Closes #21` e `--assignee @me`

---

## Dependencies & Execution Order

- **Foundational (2)** → US1 (3) → US2 (4) → US3 (5) → Polish (6)
- T004 depende de T001–T003; T006 de T002 e T004; T007 de T001, T004 e T006; T008 de T007; T009
  de T005 e T007; T010 de T008.
- US2 e US3 usam o caso de uso e o repositório da US1, por isso seguem em sequência; US3 não muda
  código de produção e pode começar logo após a US1 se a US2 estiver em andamento (arquivos de
  teste diferentes).
- Dentro de cada história: testes primeiro (falhando), depois implementação.

### Parallel Opportunities

- T001, T002, T003 e T005
- T011, T012 e T013
- T018, T019 e T020
- T024, T025 e T026

## Parallel Example: User Story 1

```text
T011 LeitorTransferenciaConcluidaTests.cs   T012 NotificarTransferenciaConcluidaTests.cs
T013 PublicacaoOutboxTests.cs
```

---

## Implementation Strategy

### MVP First

1. Fundação (T001–T010)
2. US1 (T011–T017) → transferência até as duas notificações gravadas
3. **PARAR e VALIDAR**: quickstart, cenário 2

### Incremental Delivery

1. US2 → idempotência também no caso de uso, duplicatas sem linhas novas
2. US3 → rollback, DLQ, reprocessamento e integridade comprovados
3. Polish → telemetria sem nomes, documentação viva, validação no Compose, issue e PR

### Commits

Conventional Commits por fase, escopo `notificacoes` (por exemplo `feat(notificacoes): ...`,
`test(notificacoes): ...`, `docs(notificacoes): ...`); PR com `Closes #21`.
