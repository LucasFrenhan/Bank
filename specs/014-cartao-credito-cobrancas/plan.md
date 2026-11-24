# Implementation Plan: Cartão de crédito, cobranças, faturas e recebimento do lojista

**Branch**: `014-cartao-credito-cobrancas` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/014-cartao-credito-cobrancas/spec.md` (issue #22, ampliada)

## Summary

O backend ganha o ciclo completo de cartão de crédito. A PF solicita um cartão (Visa ou Mastercard, um
por CPF) cujo número e CVV são derivados por HMAC e nunca gravados. A PJ emite cobranças; a PF paga com
o próprio cartão, e um autorizador simulado da bandeira decide por fatura vencida e limite, numa
transação que também cria a compra, o recebível e o evento `cobranca.paga` no outbox. O Worker ganha um
processamento idempotente, que a partir da 00:00 de cada dia útil lança compras, libera faturas e liquida
recebíveis. A PF consulta e paga faturas (com juros por atraso) e a PJ consulta e antecipa recebíveis
(taxa de 2%). Um consumidor novo registra as notificações. No frontend, só os rótulos dos novos
lançamentos do extrato.

## Technical Context

**Language/Version**: C# / .NET 10; SQL (PostgreSQL 18); TypeScript 5.9 (só rótulos do extrato)

**Primary Dependencies**: as das specs 002–013; **nenhuma dependência nova**

**Storage**: PostgreSQL 18 — 8 tabelas novas (`bandeiras`, `cartoes`, `cobrancas`,
`autorizacoes_cartao`, `faturas`, `compras_cartao`, `lancamentos_fatura`, `recebiveis`), 2 colunas em
`lancamentos`; migration `AdicionarCartoesCobrancasERecebiveis`

**Testing**: xUnit v3, `WebApplicationFactory` (API e Worker), Testcontainers (PostgreSQL e RabbitMQ),
ArchUnitNET; Vitest

**Target Platform**: contêineres Linux (Compose, sem serviço novo) e SDK local (Windows e CI Linux)

**Project Type**: web-service + worker (backend); ajuste pontual no frontend

**Performance Goals**: pagamento com p95 ≤ 2 s (SC-002); 10.000 compras lançadas em ≤ 5 min (SC-007)

**Constraints**: número e CVV nunca gravados nem em telemetria; registros financeiros imutáveis; datas no
calendário de Brasília; ordem única de bloqueio (R8); processamento reexecutável e seguro com várias
instâncias

**Scale/Scope**: 14 endpoints; 16 casos de uso; 7 identificadores tipados; 3 consultas nativas novas; 1
consumidor; 1 serviço hospedado; 7 histórias

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 12 clarificações; issue #22; branch = pasta | ✅ |
| II. Endpoints só transporte; dependências | Regras no Domain (`AutorizadorBandeira`, `Fatura`, `Recebivel`, `CalendarioBancario`); orquestração na Application; HMAC, SQL e agendamento na Infrastructure (R1, R7, R9) | ✅ |
| III. ProblemDetails | Todo erro novo com `type` estável no contrato e em `ResultadoHttpExtensions`; `detail` sem dados do cartão | ✅ |
| IV. Decimal e escala | `numeric(18,2)`; arredondamento explícito (R13) | ✅ |
| IV. Atomicidade | Pagamento de cobrança e de fatura, antecipação e cada item do processamento em transação única (R9) | ✅ |
| IV. Idempotência | `Idempotency-Key` nas 3 operações financeiras; processamento por estado e índices únicos (R7, R10) | ✅ |
| IV. Imutabilidade | Triggers em autorizações, compras e lançamentos de fatura; razão da carteira inalterado (R5, R14) | ✅ |
| IV. Concorrência definida no plano | Bloqueio pessimista em ordem única; `CHECK` e únicos como garantia final (R4, R6, R8) | ✅ |
| IV. Evento consistente | `cobranca.paga` no outbox, na transação do pagamento | ✅ |
| V. Contratos versionados | API 1.0.0 nova; extrato 1.3.0 aditivo; evento v1 ajustado antes da primeira publicação; topologia 1.1.0 | ✅ |
| VI. Observabilidade, sem dados sensíveis | Métricas abaixo; tipos mascarados e varredura em teste (R2); `no-store` | ✅ |
| VII. Testes | Unitários de Domain e Application; integração por endpoint com 401/403; contrato do evento; arquitetura (R17) | ✅ |
| Restrições: migrations, SQL em `.sql` | Triggers em `Procedures/`, bloqueios em `Consultas/`; demais consultas em LINQ | ✅ |
| Restrições: identificadores tipados | 7 novos; `Bandeira` com chave natural, como `Banco` | ✅ |
| Restrições: dependência nova justificada | Nenhuma | ✅ |
| Restrições: segredos | `Cartoes:ChaveDerivacao` por variável ou user-secrets; padrão só de desenvolvimento no Compose, como o JWT | ✅ |
| Restrições: telas desktop e mobile | Nenhuma tela nova; rótulos conferidos nas duas larguras | ✅ |
| Convenções: português | `Cartao`, `Cobranca`, `Fatura`, `Recebivel`, `CalendarioBancario` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado, sem itens em Complexity Tracking.

## Observabilidade

| Meter / instrumento | Tags | Onde |
| --- | --- | --- |
| `Bank.Cartoes` `bank.cartoes.emitidos` | `bandeira` | API |
| `bank.cobrancas.emitidas`, `bank.cobrancas.canceladas` | — | API |
| `bank.cobrancas.autorizacoes` | `resultado` (`aprovada` ou o motivo), `bandeira` (`001`, `002`, `nenhuma`) | API |
| `bank.faturas.pagamentos` | `vencida` | API |
| `bank.recebiveis.antecipados` | — | API |
| `Bank.Processamento` `bank.processamento.execucoes` | `resultado` (`concluida`, `com-falhas`); só rodadas com trabalho | Worker |
| `bank.processamento.compras_lancadas`, `.faturas_liberadas`, `.recebiveis_liquidados` | — | Worker |
| `bank.processamento.falhas` | `etapa` (`lancamento`, `liquidacao`) | Worker |
| `bank.processamento.duracao` (s) | — | Worker |

Rodadas sem trabalho (ou em dia não útil) não geram métrica, span nem log. Span `processamento diario` (`Bank.Processamento`) nas
demais. Logs estruturados só com identificadores, valores e contagens.

## Configuração nova

| Chave | Padrão | Compose (`.env`) | Onde |
| --- | --- | --- | --- |
| `Cartoes:ChaveDerivacao` | — (obrigatória, ≥ 32 bytes) | `CARTOES_CHAVE_DERIVACAO` | API |
| `Cartoes:LimitePadrao` | 5000.00 | `LIMITE_CARTAO_PADRAO` | API |
| `Cartoes:JurosAtrasoMensalPercentual` | 15.00 | `JUROS_ATRASO_MENSAL` | API |
| `ProcessamentoDiario:Habilitado` | true (false em `Testing`) | — | Worker |
| `ProcessamentoDiario:IntervaloSegundos` | 60 | — | Worker |

## Project Structure

### Documentation (this feature)

```text
specs/014-cartao-credito-cobrancas/
├── plan.md
├── research.md                      # R1–R17
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── cartoes-cobrancas-api.md     # API 1.0.0
│   ├── evento-cobranca-paga.md      # cobranca.paga v1 e topologia 1.1.0
│   └── transferencias-api.md        # extrato 1.3.0
├── checklists/requirements.md
└── tasks.md                         # /speckit-tasks
```

### Source Code (repository root)

```text
docker-compose.yml, .env.example, docs/desenvolvimento.md     # configuração nova
src/backend/
├── src/
│   ├── Bank.Domain/
│   │   ├── Calendario/CalendarioBancario.cs
│   │   ├── Cartoes/                 # Bandeira, Cartao, NumeroCartao, Cvv, ValidadeCartao, CodigoAutorizacao,
│   │   │                            # AutorizacaoCartao, AutorizadorBandeira, CompetenciaFatura, Fatura,
│   │   │                            # CompraCartao, LancamentoFatura, JurosAtraso, ids, ErrosCartao, ErrosFatura
│   │   ├── Cobrancas/               # Cobranca, PagamentoCobranca, CobrancaPaga (evento), id, ErrosCobranca
│   │   ├── Recebiveis/              # Recebivel, id, ErrosRecebivel
│   │   ├── Notificacoes/            # + tipos e textos de cobrança
│   │   ├── Permissoes/              # + UsarCartaoCredito, GerenciarRecebiveis
│   │   └── Transferencias/Lancamento.cs   # + origens fatura e recebível
│   ├── Bank.Application/
│   │   ├── Abstracoes/              # IUnidadeDeTrabalho, repositórios, consultas, IDadosCartao, INotificadorCobranca
│   │   ├── Cartoes/ Cobrancas/ Faturas/ Recebiveis/ Processamento/
│   │   ├── Notificacoes/NotificarCobrancaPaga.cs
│   │   ├── Observabilidade/MetricasCartoes.cs, MetricasProcessamento.cs
│   │   └── DependencyInjection.cs   # AddApplication, AddNotificacoes, AddProcessamento
│   ├── Bank.Infrastructure/
│   │   ├── Seguranca/DadosCartaoHmac.cs, ChaveCartoesOptions.cs
│   │   ├── Processamento/ProcessamentoDiarioHospedado.cs, ProcessamentoDiarioOptions.cs
│   │   ├── Mensageria/              # CobrancaPagaV1, LeitorCobrancaPaga, ConsumidorNotificacoesCobranca,
│   │   │                            # EventoOutbox.De, TopologiaMensageria
│   │   └── Persistencia/            # DbContext, Configuracoes/*, repositórios, consultas, UnidadeDeTrabalho,
│   │                                # Sql/Consultas/Bloquear*.sql, Sql/Procedures/*DeCartao.sql, Migrations
│   ├── Bank.Api/Endpoints/          # CartoesEndpoints, CobrancasEndpoints, FaturasEndpoints, RecebiveisEndpoints,
│   │                                # Idempotencia e paginação comuns; ErrorHandling, Observability
│   └── Bank.Worker/                 # Program, TelemetriaWorker, appsettings
└── tests/                           # Bank.UnitTests, Bank.IntegrationTests, Bank.ArchitectureTests
src/frontend/src/paginas/            # lancamentos.ts, Inicio.tsx, Extrato.tsx + testes
specs/009-…/contracts/transferencias-api.md, specs/010-…/contracts/*   # histórico apontando para a 014
```

**Structure Decision**: cada conceito ganha pasta própria nas camadas existentes; fatura e compra ficam em
`Cartoes/` no Domain porque dependem do cartão e são protegidas pelo mesmo bloqueio. O processamento roda
no Worker, que já hospeda serviços de fundo; a API não agenda nada. As transferências ficam intocadas.

## Complexity Tracking

Sem violações a justificar.
