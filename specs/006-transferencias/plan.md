# Implementation Plan: Motor de transferências e extrato

**Branch**: `006-transferencias` | **Date**: 2026-11-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-transferencias/spec.md` (issues #8 e #13)

## Summary

Primeira movimentação de saldo do Bank. Uma PF transfere para PF ou PJ pelo documento: as duas
carteiras são bloqueadas no PostgreSQL em ordem determinística (`SELECT ... ORDER BY id FOR
UPDATE`, em arquivo `.sql`), o saldo é verificado sob bloqueio, e débito, crédito, transferência,
dois lançamentos de razão e o evento `transferencia.concluida` (outbox) são gravados numa única
transação. A idempotência usa `Idempotency-Key` por pagador, garantida por índice único. O razão
(`lancamentos`) alimenta o extrato paginado com filtro de direção e sustenta a invariante "saldo
= soma dos lançamentos", inclusive com o lançamento de abertura do saldo de demonstração (no
cadastro e, para carteiras antigas, na inicialização fora de `Production`). Lançamentos e
transferências são imutáveis por trigger. Inclui consulta prévia do destinatário (corpo, não URL),
métricas de negócio e os testes de concorrência da #13.

## Technical Context

**Language/Version**: C# / .NET 10 (spec 002)

**Primary Dependencies**: as da spec 003 (EF Core 10.0.15 + Npgsql 10.0.3, JwtBearer); nenhuma nova

**Storage**: PostgreSQL 18 — tabelas novas `transferencias`, `lancamentos`, `outbox_eventos`;
migration `CriarTransferenciasLancamentosEOutbox`; SQL puro em `Persistencia/Sql/`

**Testing**: xUnit v3, `WebApplicationFactory` + Testcontainers (PostgreSQL), ArchUnitNET

**Target Platform**: contêiner Linux (Compose) e SDK local

**Project Type**: web-service (backend); telas na issue #32

**Performance Goals**: p95 < 1 s para transferência e extrato (SC-006); 50 cruzadas
simultâneas < 10 s (SC-003)

**Constraints**: saldo nunca negativo; atomicidade total; bloqueio ordenado; idempotência pela
base; lançamentos imutáveis; outbox na mesma transação; nenhum dado pessoal na telemetria nem
em URL; SQL puro só em `.sql`

**Scale/Scope**: 3 endpoints, 3 tabelas, 1 migration, 3 arquivos `.sql`, 3 casos de uso, 1
serviço hospedado, 5 métricas

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 7 clarificações; issues #8 e #13; branch `006-transferencias` = pasta | ✅ |
| II. Endpoints só transporte; Domain isolado | Endpoints leem claims/cabeçalho e chamam casos de uso; regras (saldo, valor, autotransferência) no Domain; transação/bloqueio atrás de `IUnidadeDeTransferencia` na Infrastructure | ✅ |
| III. ProblemDetails com `type` estável | Quatro tipos novos documentados; 422 para regra de negócio (`TipoErro.RegraDeNegocio`) | ✅ |
| IV. `decimal`/`numeric` com moeda | `numeric(18,2)`; BRL (carteira) | ✅ |
| IV. Operações de saldo atômicas e idempotentes com `Idempotency-Key` | Uma transação; `Idempotency-Key` + índice único por pagador (R4) | ✅ |
| IV. Lançamentos imutáveis | Razão `lancamentos` + trigger que impede UPDATE/DELETE (R2) | ✅ |
| IV. Controle de concorrência definido no plano | Bloqueio pessimista ordenado (R3); `xmin` mantido | ✅ |
| IV. Eventos consistentes com a transação | Outbox na mesma transação (R9) | ✅ |
| V. Contratos versionados; eventos no passado com versão | API v1.0.0; `transferencia.concluida` v1 | ✅ |
| VI. Métricas de negócio; sem dados sensíveis | Meter `Bank.Transferencias` (R10); documento fora da URL (R7) | ✅ |
| VII. Unitários, integração, 401/403, Testcontainers, contrato de mensageria | R11, incluindo teste de serialização do evento | ✅ |
| Restrições: schema por migrations | Tabelas, índices e trigger pela migration | ✅ |
| Restrições: SQL puro em arquivos `.sql` (v1.1.0) | 3 arquivos em `Persistencia/Sql/` embutidos; nenhuma string SQL no C# | ✅ |
| Restrições: dependência nova justificada | Nenhuma | ✅ |
| Convenções: português | `Transferencia`, `Lancamento`, `TransferenciasEndpoints` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado; itens em Complexity
Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/006-transferencias/
├── plan.md
├── research.md                              # R1–R11
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── transferencias-api.md                # HTTP v1.0.0
│   └── evento-transferencia-concluida.md    # evento v1
├── checklists/requirements.md
└── tasks.md                                 # /speckit-tasks
```

### Source Code (repository root)

```text
src/backend/
├── src/
│   ├── Bank.Domain/
│   │   ├── Comum/Erro.cs                                  # + TipoErro.RegraDeNegocio
│   │   ├── Carteiras/Carteira.cs                          # + Debitar/Creditar
│   │   ├── Usuarios/Usuario.cs                            # + lançamento de abertura no Criar
│   │   ├── Usuarios/Documento.cs                          # + Normalizar/Ocultar para busca e extrato
│   │   └── Transferencias/  Transferencia.cs, ValorTransferencia.cs, Lancamento.cs,
│   │                        ErrosTransferencia.cs, TransferenciaConcluida.cs
│   ├── Bank.Application/
│   │   ├── Abstracoes/  IUnidadeDeTransferencia.cs, ITransferenciaRepositorio.cs,
│   │   │                IExtratoConsultas.cs, IUsuarioConsultas.cs (+ destinatário)
│   │   ├── Transferencias/  ConsultarDestinatario.cs, RealizarTransferencia.cs, ObterExtrato.cs
│   │   └── Observabilidade/MetricasTransferencias.cs
│   ├── Bank.Infrastructure/
│   │   ├── Bank.Infrastructure.csproj                     # EmbeddedResource Persistencia/Sql/**/*.sql
│   │   ├── Persistencia/
│   │   │   ├── Sql/Procedures/ImpedirAlteracaoDeRegistrosImutaveis.sql
│   │   │   ├── Sql/Consultas/BloquearCarteirasParaTransferencia.sql
│   │   │   ├── Sql/Consultas/CriarLancamentosDeAbertura.sql
│   │   │   ├── SqlEmbutido.cs                             # leitor dos .sql
│   │   │   ├── Configuracoes/ TransferenciaConfiguracao.cs, LancamentoConfiguracao.cs,
│   │   │   │                  EventoOutboxConfiguracao.cs
│   │   │   ├── UnidadeDeTransferencia.cs, TransferenciaRepositorio.cs, ExtratoConsultas.cs
│   │   │   ├── UsuarioRepositorio.cs                      # + grava a abertura no cadastro
│   │   │   ├── LancamentosDeAberturaNaInicializacao.cs
│   │   │   └── Migrations/*_CriarTransferenciasLancamentosEOutbox.cs
│   │   └── Mensageria/EventoOutbox.cs                     # entidade + serialização
│   └── Bank.Api/
│       ├── Endpoints/TransferenciasEndpoints.cs
│       ├── ErrorHandling/ResultadoHttpExtensions.cs       # 422 + títulos novos
│       ├── appsettings*.json                              # + flag de aberturas
│       └── Program.cs
└── tests/
    ├── Bank.UnitTests/ Domain/(Valor, Carteira, Transferencia), Application/(3 casos de uso),
    │                   Infrastructure/EventoOutboxTests.cs (contrato do evento)
    ├── Bank.IntegrationTests/ TransferenciasTests.cs, IdempotenciaTransferenciaTests.cs,
    │                          ConcorrenciaTransferenciaTests.cs, ExtratoTests.cs,
    │                          RazaoEImutabilidadeTests.cs, DestinatarioTests.cs,
    │                          AberturaNaInicializacaoTests.cs, (+ ajustes em MinhaCarteiraTests,
    │                          UsuariosMeTests, OpenApiTests, DadosSensiveisTelemetriaTests)
    └── Bank.ArchitectureTests/ SqlEmbutidoTests.cs
```

**Structure Decision**: mesma organização por conceito das specs anteriores; SQL puro na pasta
definida pela constituição v1.1.0.

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| Tabela `lancamentos` além de `transferencias` (#8 previa só esta) | Extrato com a abertura e invariante saldo = soma dos lançamentos (clarify) | Só `transferencias` não representa a abertura e não fecha com o saldo |
| Trigger de imutabilidade | Princípio IV verificável no banco | Só convenção no código não impede alterações por outros caminhos |
| Testes de "conta removida" das specs 003/005 reescritos com token de usuário inexistente | FK `RESTRICT` + trigger impedem apagar carteiras com lançamentos | Apagar dados para o teste violaria a imutabilidade agora garantida |
| Consulta de destinatário por `POST` com corpo | Documento fora da URL e da telemetria (FR-009b) | `GET /{documento}` exporia CPF/CNPJ em spans e logs de acesso |
