# Implementation Plan: Sessão renovável com refresh token rotativo e logout no servidor

**Branch**: `016-sessao-refresh-token` | **Date**: 2026-11-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/016-sessao-refresh-token/spec.md` (issue #57)

## Summary

O login passa a criar uma **sessão no servidor** e a entregar, além de um token de acesso de 5 minutos, uma
credencial de renovação opaca em cookie `HttpOnly; Secure; SameSite=Strict` restrito a
`/api/v1/autenticacao`. O banco guarda só o SHA-256 da credencial. `POST /renovar` rotaciona a credencial de
forma atômica (bloqueio na linha da sessão), aceita a anterior por 30 s para abas simultâneas e respostas
perdidas e, fora disso, trata o uso como reuso e revoga a sessão. `POST /sair` revoga e apaga o cookie. A
sessão vence por 15 min sem renovação ou 8 h após o login; o Worker apaga sessões encerradas após 7 dias.
No frontend, o token fica só em memória; um invólucro do `fetch` renova uma única vez por 401 (voo único na
aba e Web Locks entre abas) e repete a chamada; a sessão é compartilhada entre abas por `BroadcastChannel`,
com aviso de expiração 60 s antes do fim (ou na metade do prazo, se ele for menor que 2 min). Os dados da operação em andamento deixam de ser perdidos na renovação.

## Technical Context

**Language/Version**: C# / .NET 10; SQL (PostgreSQL 18); TypeScript 5.9 / React 19

**Primary Dependencies**: as das specs 002–015; **nenhuma dependência nova** (geração aleatória e SHA-256
do .NET; Web Locks e `BroadcastChannel` nativos do navegador)

**Storage**: PostgreSQL 18 — 2 tabelas novas (`sessoes`, `credenciais_renovacao`); migration
`AdicionarSessoes`; 1 consulta nativa (`BloquearSessao.sql`)

**Testing**: xUnit v3, `WebApplicationFactory` (API e Worker), Testcontainers (PostgreSQL), ArchUnitNET,
`FakeTimeProvider`; Vitest + MSW; Playwright

**Target Platform**: contêineres Linux (Compose, sem serviço novo) e SDK local; navegadores da spec 004

**Project Type**: web-service + worker (backend) e SPA (frontend)

**Performance Goals**: renovação imperceptível (SC-007), sem meta numérica própria (usa a latência das rotas existentes); no máximo uma renovação
simultânea por navegador

**Constraints**: nenhum token em armazenamento do navegador, banco em texto puro ou telemetria; recusa única
sem motivo; contrato aditivo; renovação só sob demanda

**Scale/Scope**: 2 endpoints novos e 1 alterado; 3 casos de uso novos e 1 alterado; 2 identificadores
tipados; 1 serviço hospedado; 1 modal novo; 4 histórias

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 6 clarificações; issue #57; branch = pasta | ✅ |
| II. Endpoints só transporte; dependências | Regras no agregado `Sessao` (Domain); orquestração em `Autenticar`, `RenovarSessao`, `EncerrarSessao`, `LimparSessoes`; geração de credencial, JWT, SQL e agendamento na Infrastructure. Ler e escrever o cookie é transporte HTTP e fica no endpoint | ✅ |
| III. ProblemDetails | `renovar` recusa com o tipo existente `nao-autenticado` (401) e `traceId`; `sair` não tem erro | ✅ |
| IV. Integridade financeira | Não movimenta saldo. A repetição após renovação reaproveita o mesmo `Idempotency-Key` (R11), sem risco de duplicar operações | ✅ |
| IV. Concorrência definida no plano | Bloqueio pessimista da sessão + índice único parcial de credencial ativa (R3) | ✅ |
| V. Contratos versionados | `usuarios-carteiras` 1.0.0 → 1.1.0 aditivo; OpenAPI e `schema.d.ts` regenerados; contrato do frontend registra os FRs substituídos da spec 004 | ✅ |
| VI. Observabilidade, sem dados sensíveis | Métricas e logs abaixo; varredura de telemetria em teste (R12) | ✅ |
| VII. Testes | Unitários de Domain e Application; integração por endpoint, incluindo 401; contrato; arquitetura; Vitest e e2e (R15) | ✅ |
| Restrições: migrations, SQL em `.sql` | Migration `AdicionarSessoes`; bloqueio em `Sql/Consultas/BloquearSessao.sql`; limpeza em LINQ | ✅ |
| Restrições: identificadores tipados | `SessaoId`, `CredencialRenovacaoId` (UUIDv7) | ✅ |
| Restrições: autenticação ASP.NET Core Identity + JWT | JWT Bearer mantido; refresh próprio, pois o Identity não oferece rotação com detecção de reuso para JWT | ✅ |
| Restrições: dependência nova justificada | Nenhuma | ✅ |
| Restrições: segredos | Nenhum segredo novo; credenciais são geradas, não configuradas | ✅ |
| Restrições: telas desktop e mobile | Modal de aviso sobre `Confirmacao`, validado nas duas larguras (e2e `responsividade`) | ✅ |
| Convenções: português | `Sessao`, `CredencialRenovacao`, `RenovarSessao`, `EncerrarSessao` | ✅ |
| Roadmap (#58/#62) | Tudo em pastas `Sessoes/` e `Autenticacao/`, dentro da fronteira do futuro módulo de Identidade, sem antecipar a refatoração | ✅ |

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado, sem itens em Complexity Tracking.

## Observabilidade

| Meter / instrumento | Tags | Onde |
| --- | --- | --- |
| `Bank.Usuarios` `bank.autenticacao.logins` (existente) | `resultado` | API |
| `bank.autenticacao.renovacoes` | `resultado` (`sucesso`, `recusada`, `reuso`) | API |
| `bank.autenticacao.sessoes_revogadas` | `motivo` (`logout`, `reuso`, `novo-login`) | API |
| `bank.autenticacao.sessoes_removidas` | — | Worker |

Logs estruturados com `SessaoId` e `UsuarioId` apenas: renovação recusada em `Information` (motivo interno),
reuso em `Warning`, revogação por logout em `Information`. Spans da instrumentação HTTP existente; nenhum
cabeçalho capturado. A limpeza só gera span, log e métrica quando remove algo.

## Configuração nova

| Chave | Padrão | Compose (`.env`) | Onde |
| --- | --- | --- | --- |
| `Jwt:ExpiracaoSegundos` | 300 (era 3600) | `JWT_EXPIRACAO_SEGUNDOS` | API |
| `Sessao:InatividadeSegundos` | 900 | `SESSAO_INATIVIDADE_SEGUNDOS` | API, Worker |
| `Sessao:LimiteAbsolutoHoras` | 8 | `SESSAO_LIMITE_ABSOLUTO_HORAS` | API |
| `Sessao:ToleranciaReusoSegundos` | 30 | `SESSAO_TOLERANCIA_REUSO_SEGUNDOS` | API |
| `Sessao:RetencaoDias` | 7 | — | Worker |
| `LimpezaSessoes:Habilitada` | true (false em `Testing`) | — | Worker |
| `LimpezaSessoes:IntervaloMinutos` | 60 | — | Worker |

Validação na inicialização (como `ValidadorJwtOptions`): valores > 0, inatividade ≤ limite absoluto,
tolerância < inatividade.

## Project Structure

### Documentation (this feature)

```text
specs/016-sessao-refresh-token/
├── plan.md
├── research.md                        # R1–R15
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── autenticacao-sessao-api.md     # usuarios-carteiras 1.1.0
│   └── frontend-sessao.md             # FRs da spec 004 substituídos, aviso e textos
├── checklists/requirements.md
└── tasks.md                           # /speckit-tasks
```

### Source Code (repository root)

```text
docker-compose.yml, .env.example, docs/desenvolvimento.md          # prazos configuráveis
src/backend/
├── src/
│   ├── Bank.Domain/Sessoes/           # Sessao, CredencialRenovacao, SessaoId, CredencialRenovacaoId,
│   │                                  # HashCredencial, MotivoRevogacao, LimitesSessao, ResultadoRenovacao
│   ├── Bank.Application/
│   │   ├── Abstracoes/                # ISessaoRepositorio, IGeradorCredencial, ILimpezaSessoes; IEmissorToken (+ sid)
│   │   ├── Autenticacao/              # Autenticar (alterado), RenovarSessao, EncerrarSessao, LimparSessoes,
│   │   │                              # SessaoEmitida (token + credencial + prazos), SessaoOptions
│   │   ├── Observabilidade/MetricasUsuarios.cs   # + renovações, revogações, remoções
│   │   └── DependencyInjection.cs
│   ├── Bank.Infrastructure/
│   │   ├── Seguranca/                 # GeradorCredencialAleatoria; EmissorTokenJwt (+ sid); JwtOptions (300 s)
│   │   ├── Sessoes/LimpezaSessoesHospedado.cs, LimpezaSessoesOptions.cs
│   │   └── Persistencia/              # BankDbContext, Configuracoes/Sessao*, SessaoRepositorio,
│   │                                  # Sql/Consultas/BloquearSessao.sql, Migrations/AdicionarSessoes
│   ├── Bank.Api/
│   │   ├── Endpoints/AutenticacaoEndpoints.cs    # login com cookie; renovar; sair
│   │   ├── Seguranca/CookieRenovacao.cs          # nome, atributos, leitura e remoção (transporte)
│   │   └── appsettings*.json
│   └── Bank.Worker/Program.cs         # + AddLimpezaSessoes
└── tests/
    ├── Bank.UnitTests/                # Sessoes/*, Autenticacao/*
    ├── Bank.IntegrationTests/         # SessaoTests, RenovacaoConcorrenteTests, LimpezaSessoesTests,
    │                                  # DadosSensiveisSessaoTests, SessaoDeTeste (helper de cookie), LoginTests
    └── Bank.ArchitectureTests/        # regras existentes cobrem os novos tipos
src/frontend/
├── src/
│   ├── api/cliente.ts                 # invólucro de fetch: renova e repete uma vez; renovar(), sair()
│   ├── api/schema.d.ts                # regenerado
│   ├── sessao/                        # armazenamento.ts (sessão só em memória), renovador.ts, canalAbas.ts,
│   │                                  # ContextoSessao.tsx (verificando/ativa/ausente; prazos), Rotas.tsx,
│   │                                  # AvisoExpiracao.tsx + testes
│   └── paginas/                       # Entrar, Cadastro, Perfil/Layout ("Sair" assíncrono)
└── e2e/sessao.spec.ts, apoio.ts       # multiaba, recarga, sair, renovação, armazenamento
specs/004-frontend-base/spec.md, specs/003-usuarios-carteiras/contracts/usuarios-carteiras-api.md
                                       # nota apontando para a 016 (histórico)
```

**Structure Decision**: a sessão é um agregado próprio em `Domain/Sessoes/`, separado de `Usuarios/`,
porque tem ciclo de vida e concorrência independentes do usuário e será movida em bloco para o módulo de
Identidade (#62). O cookie é tratado no endpoint, como transporte; a Application recebe e devolve só o valor
da credencial. A limpeza vai para o Worker, que já hospeda os serviços de fundo. No frontend, a renovação fica
no cliente da API (único ponto que fala com o servidor) e a coordenação entre abas em `sessao/`.

## Complexity Tracking

Sem violações a justificar.
