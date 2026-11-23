# Implementation Plan: Dados bancários da carteira

**Branch**: `012-dados-bancarios-carteira` | **Date**: 2026-11-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/012-dados-bancarios-carteira/spec.md` (issue #28,
TASK-15)

## Summary

A carteira passa a ter dados bancários obrigatórios: banco (um de cinco, sendo 001 "One Bank"
o nosso), agência, número de 8 dígitos com dígito verificador (Luhn sobre agência + número) e
tipo (conta corrente ou poupança). O Domain ganha o catálogo `Banco`, o enum
`TipoContaBancaria` e o value object `ContaBancaria`, que formata, valida e calcula o dígito.
O cadastro obtém o próximo número de uma sequence do PostgreSQL (por uma abstração nova da
Application e uma consulta `.sql`) e cria sempre conta corrente no 001, agência 0001. Uma
migration cria `bancos`, a sequence, a function SQL do dígito, preenche as carteiras existentes
e só então torna as colunas obrigatórias, com índice único, FK e `CHECK`; cria também
`contas_externas` para as contas de outros bancos (002–005), que só são gravadas por script.
`GET /api/v1/carteiras/minha` ganha o objeto `contaBancaria` (mudança aditiva). Dois scripts SQL
reexecutáveis registram contas de outros bancos e criam um titular com conta poupança. Frontend
fica para uma task posterior.

## Technical Context

**Language/Version**: C# / .NET 10; SQL (PostgreSQL 18)

**Primary Dependencies**: as das specs 002–011; **nenhuma dependência nova**

**Storage**: PostgreSQL 18 — tabelas novas `bancos` e `contas_externas`, colunas novas em
`carteiras`, sequence `seq_numero_conta_one_bank`, function `calcular_digito_conta` (migration
`AdicionarDadosBancariosCarteira`)

**Testing**: xUnit v3; `WebApplicationFactory` da API; Testcontainers (PostgreSQL); ArchUnitNET
e testes de arquitetura existentes

**Target Platform**: contêineres Linux (Compose, sem serviço novo) e SDK local (Windows e runner
Linux do CI)

**Project Type**: web-service (backend + scripts SQL; sem mudança no frontend nem no Worker)

**Performance Goals**: o cadastro ganha 1 ida ao banco (`nextval`) — impacto desprezível; o
backfill é um único `UPDATE` (proporcional ao número de carteiras, que é pequeno neste projeto)

**Constraints**: dados bancários imutáveis após a criação; unicidade (banco, agência, número)
garantida pelo banco; dígito conferido também pelo banco (`CHECK`); campos atuais do contrato
sem mudança; dados bancários fora de logs e telemetria (FR-013)

**Scale/Scope**: 2 tabelas novas, 5 colunas, 1 sequence, 1 function; 4 tipos novos no Domain
(+ `ContaExternaId`), 1 abstração, 1 implementação, ajustes em `Carteira`, `Usuario`,
`RegistrarUsuario`, consulta e endpoint da carteira; 1 migration; 3 arquivos `.sql` da
aplicação; 2 scripts operacionais

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 6 clarificações; issue #28; branch `012-dados-bancarios-carteira` = pasta | ✅ |
| II. Endpoints só transporte; dependências | Endpoint só mapeia `DadosCarteira` → resposta; formato e dígito no Domain (`ContaBancaria`); orquestração em `RegistrarUsuario`; sequence e SQL na Infrastructure por `IGeradorNumeroConta` (R3) | ✅ |
| III. ProblemDetails | Sem resposta de erro nova | ✅ |
| IV. Atomicidade | Usuário + carteira com dados bancários no mesmo `SaveChanges` (como hoje); backfill e restrições na mesma migration (R7) | ✅ |
| IV. Concorrência definida no plano | `nextval` atômico entre cadastros simultâneos; índice único como garantia final; lacunas aceitas (R2) | ✅ |
| IV. Idempotência | Scripts reexecutáveis (`ON CONFLICT DO NOTHING` / `WHERE NOT EXISTS`) (R8) | ✅ |
| V. Contratos versionados | `GET /carteiras/minha` 1.0.0 → 1.1.0, aditivo (`contracts/carteira-minha.md`) | ✅ |
| VI. Observabilidade, sem dados sensíveis | Nenhum log/métrica novo com agência ou número; logs atuais inalterados (FR-013) | ✅ |
| VII. Unitários, integração com contêineres | Domain/Application unitários; integração para cadastro, concorrência, backfill, restrições, scripts, contrato (R10) | ✅ |
| Restrições: schema por migrations | `AdicionarDadosBancariosCarteira` (tabelas, colunas, sequence, function, dados dos bancos) | ✅ |
| Restrições: SQL puro em `.sql` | `Sql/Procedures/CalcularDigitoConta.sql` (function, criada pela migration); `Sql/Consultas/ObterProximoNumeroConta.sql` (execução); `Sql/Migracoes/PreencherDadosBancariosCarteiras.sql` (backfill, subpasta nova — ver Complexity Tracking); scripts operacionais em `scripts/sql/` fora da aplicação (R8) | ✅ |
| Restrições: identificadores tipados | `ContaExternaId`; `Banco` usa o código de 3 dígitos como chave natural, não `Guid` (R5); `ContaBancaria` é value object | ✅ |
| Restrições: dependência nova justificada | Nenhuma | ✅ |
| Convenções: português | `Banco`, `ContaBancaria`, `TipoContaBancaria`, `ContaExterna`, `IGeradorNumeroConta`, `contas_externas` | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado; um item em Complexity
Tracking (subpasta `Sql/Migracoes/`).

## Project Structure

### Documentation (this feature)

```text
specs/012-dados-bancarios-carteira/
├── plan.md
├── research.md                 # R1–R10
├── data-model.md
├── quickstart.md
├── contracts/
│   └── carteira-minha.md       # GET /api/v1/carteiras/minha 1.1.0
├── checklists/requirements.md
└── tasks.md                    # /speckit-tasks
```

### Source Code (repository root)

```text
scripts/sql/dados-bancarios/                        # novo (R8)
├── registrar-contas-outros-bancos.sql
└── criar-conta-poupanca.sql

src/backend/
├── src/
│   ├── Bank.Domain/
│   │   ├── Bancos/                                 # novo
│   │   │   ├── Banco.cs                            # catálogo fixo (R5)
│   │   │   ├── TipoContaBancaria.cs
│   │   │   ├── ContaBancaria.cs                    # value object + Luhn (R1, R3)
│   │   │   ├── ContaExterna.cs                     # registro de outros bancos (R6)
│   │   │   └── ContaExternaId.cs
│   │   ├── Carteiras/Carteira.cs                   # + dados bancários; Criar recebe ContaBancaria
│   │   └── Usuarios/Usuario.cs                     # Criar recebe ContaBancaria
│   ├── Bank.Application/
│   │   ├── Abstracoes/IGeradorNumeroConta.cs       # novo (R2)
│   │   ├── Abstracoes/ICarteiraConsultas.cs        # DadosCarteira + DadosContaBancaria (R9)
│   │   └── Usuarios/RegistrarUsuario.cs            # número + conta corrente 001/0001
│   ├── Bank.Infrastructure/
│   │   ├── DependencyInjection.cs                  # IGeradorNumeroConta
│   │   └── Persistencia/
│   │       ├── BankDbContext.cs                    # DbSet Bancos/ContasExternas, sequence, conversor
│   │       ├── GeradorNumeroConta.cs               # novo: executa ObterProximoNumeroConta.sql
│   │       ├── CarteiraConsultas.cs                # join com bancos
│   │       ├── Configuracoes/CarteiraConfiguracao.cs      # colunas, índice, FK, CHECK (R4)
│   │       ├── Configuracoes/BancoConfiguracao.cs         # novo + HasData
│   │       ├── Configuracoes/ContaExternaConfiguracao.cs  # novo
│   │       ├── Sql/Procedures/CalcularDigitoConta.sql
│   │       ├── Sql/Consultas/ObterProximoNumeroConta.sql
│   │       ├── Sql/Migracoes/PreencherDadosBancariosCarteiras.sql
│   │       └── Migrations/*_AdicionarDadosBancariosCarteira.cs   # ordem R7
│   └── Bank.Api/Endpoints/CarteirasEndpoints.cs    # contaBancaria na resposta
└── tests/
    ├── Bank.UnitTests/
    │   ├── Domain/ContaBancariaTests.cs            # novo: vetores, erro de um dígito, formato
    │   ├── Domain/(UsuarioTests, CarteiraTests …)  # ajustados à nova assinatura
    │   ├── ContasDeTeste.cs                        # novo: ContaBancaria de teste para os chamadores
    │   └── Application/RegistrarUsuarioTests.cs    # + gerador de número falso
    └── Bank.IntegrationTests/
        ├── DadosBancariosCadastroTests.cs          # novo: PF/PJ, 50 simultâneos, consulta
        ├── DadosBancariosMigracaoTests.cs          # novo: backfill, base vazia, saldo/extrato
        ├── DadosBancariosRestricoesTests.cs        # novo: CHECK/FK/único, function = C#
        ├── ScriptsDadosBancariosTests.cs           # novo: scripts 2x, login da poupança
        ├── MinhaCarteiraTests.cs                   # + contaBancaria
        └── OpenApiTests.cs                         # + schema de contaBancaria
```

**Structure Decision**: os conceitos bancários ficam numa pasta própria do Domain (`Bancos/`),
pois servem à carteira e às contas externas; a carteira continua sendo criada só pelo
agregado `Usuario`. A Infrastructure segue os padrões existentes (configuração por entidade,
SQL embutido via `SqlEmbutido`, repositório/consulta por abstração). Os scripts operacionais
ficam fora de `src/` porque não fazem parte da aplicação.

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
| --- | --- | --- |
| Subpasta nova `Persistencia/Sql/Migracoes/` para o backfill | A constituição lista `Procedures/` (functions/views) e `Consultas/` (execução); um `UPDATE` executado uma única vez pela migration não é nenhum dos dois, mas precisa estar em `.sql` | Pôr em `Procedures/` misturaria objetos de banco com comandos de dados; pôr em `Consultas/` sugeriria execução em tempo de execução; string no C# viola a regra de SQL em arquivo |
