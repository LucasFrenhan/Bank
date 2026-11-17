# Implementation Plan: Usuários (PF/PJ), Carteiras e Autenticação

**Branch**: `003-usuarios-carteiras` | **Date**: 2026-11-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/003-usuarios-carteiras/spec.md` (issue #5;
implementação na issue #6)

## Summary

Primeira funcionalidade de negócio do Bank. O Domain ganha o agregado `Usuario` (PF/PJ) com
sua `Carteira` 1:1, os value objects `Cpf` e `Cnpj` (incluindo o CNPJ alfanumérico da IN RFB
nº 2.229/2024, com um único algoritmo de DV para os dois formatos), a política de senha e a
matriz de permissões por perfil. A Application orquestra três casos de uso — registrar,
autenticar e obter a própria carteira — por resultados tipados. A Infrastructure persiste em
PostgreSQL via EF Core/Npgsql (migration inicial com constraints de unicidade, saldo não
negativo e moeda), gera o hash de senha com o `PasswordHasher` do ASP.NET Core Identity e
emite JWT HS256. A Api expõe `POST /api/v1/autenticacao/registrar`,
`POST /api/v1/autenticacao/login` e `GET /api/v1/carteiras/minha` com erros
`ProblemDetails` de `type` estável, políticas de autorização e métricas de negócio. Testes de
integração passam a usar PostgreSQL real com Testcontainers.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK e runtime fixados pela spec 002)

**Primary Dependencies**: ASP.NET Core Minimal APIs; EF Core 10.0.15 +
`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3; `Microsoft.Extensions.Identity.Core` 10.0.15
(hasher); `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15;
`Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.15 — justificativas em
[research.md R1](research.md#r1-pacotes-nuget-justificativa-exigida-pela-constituição)

**Storage**: PostgreSQL 18 (Compose da spec 001); tabelas `usuarios` e `carteiras` por
migration EF Core ([data-model.md](data-model.md))

**Testing**: xUnit v3 (MTP), `WebApplicationFactory`, `Testcontainers.PostgreSql` 4.16.0
(`postgres:18.7-alpine`), ArchUnitNET

**Target Platform**: contêiner Linux (Compose) e execução local pelo SDK

**Project Type**: web-service (backend); sem frontend nesta spec

**Performance Goals**: p95 < 1 s para cadastro, login e consulta no ambiente local (SC-007);
o custo dominante é o hash PBKDF2 (≈ 100 ms por operação)

**Constraints**: saldo nunca negativo (constraint no banco); senha só como hash; nenhum
dado sensível em telemetria; chave JWT fora do código versionado; erros com `type`
estável; Domain sem dependências de framework

**Scale/Scope**: 3 endpoints, 2 tabelas, 1 migration, 4 políticas de permissão + 1 de
autenticação, ~6 value objects/enums de domínio

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec + clarify (CNPJ alfanumérico), issue #5, branch `003-usuarios-carteiras` = pasta | ✅ |
| II. Endpoints só transporte | Endpoints convertem DTO → comando, chamam serviço de Application e traduzem `Resultado` em HTTP; leem claims do `HttpContext.User` (transporte) e passam o `usuarioId` ao serviço | ✅ |
| II. Sem `DbContext`/repositórios nos endpoints | Repositórios só via abstrações de Application implementadas em Infrastructure; regra de arquitetura existente + novas (R10) | ✅ |
| II. Domain sem dependências | CPF/CNPJ, senha, permissões e resultados em C# puro; hash e JWT atrás de `IHasherSenha`/`IEmissorToken` | ✅ |
| III. ProblemDetails, `traceId`, 400 com `errors`, `type` estável | Tabela de `type` `urn:bank:problema:*` no contrato; 401/403 do JwtBearer convertidos (R4, R6) | ✅ |
| III. Falhas esperadas por resultados tipados | `Resultado<T>`/`Erro`; exceções só para falhas inesperadas; violação `23505` traduzida em resultado (R7) | ✅ |
| IV. Dinheiro em `decimal`/`numeric` com moeda | `numeric(18,2)` + `moeda char(3) = 'BRL'` | ✅ |
| IV. Atomicidade | Usuário + carteira em um único `SaveChanges` (RN-04) | ✅ |
| IV. Idempotência / lançamentos imutáveis / outbox | Não há movimentação de saldo nem eventos nesta spec. O saldo de demonstração só existe fora de `Production` (validado na subida; R8) e a issue #8 cria o lançamento de abertura na inicialização, também só fora de `Production` (Clarifications da spec). Cadastro repetido é barrado pela unicidade (409) | ✅ (com registro para a issue #8) |
| IV. Controle de concorrência do saldo | Token otimista `xmin` já configurado na carteira (R7) | ✅ |
| V. Contratos versionados + OpenAPI/Scalar | [contracts/usuarios-carteiras-api.md](contracts/usuarios-carteiras-api.md) v1.0.0; rotas `/api/v1`; esquema `Bearer` no OpenAPI | ✅ |
| VI. Observabilidade e dados sensíveis | Meter `Bank.Usuarios`; logs sem e-mail/documento/senha/token; teste dedicado (R9) | ✅ |
| VII. Unitários de domínio e serviços | Todos os VOs, entidades, permissões e serviços (R10) | ✅ |
| VII. Integração caminho feliz + erros | 201/400/409, 200/400/401, 200/401/403/404 (R10) | ✅ |
| VII. 401 e 403 em endpoint protegido | `GET /carteiras/minha`: sem token, expirado, adulterado, outro algoritmo → 401; sem claims → 403 | ✅ |
| VII. Testcontainers (sem banco em memória) | `Testcontainers.PostgreSql` com a imagem do Compose | ✅ |
| Restrições: EF Core + Npgsql + migrations | Migration `CriarUsuariosECarteiras` | ✅ |
| Restrições: ASP.NET Core Identity + JWT Bearer | Hasher do Identity (sem `IdentityDbContext`, ver Complexity Tracking) + JwtBearer | ✅ (com justificativa) |
| Restrições: nova dependência justificada | Tabela R1, com alternativas rejeitadas | ✅ |
| Restrições: segredos não versionados | Chave JWT via user-secrets/`.env`; `.env.example` só com padrão de desenvolvimento, como as senhas da spec 001 | ✅ |
| Convenções: linguagem ubíqua em português | `Usuario`, `Carteira`, `TipoConta`, `PoliticaSenha`, `Permissao` | ✅ |

**Resultado pré-pesquisa**: aprovado.
**Re-check pós-design (Phase 1)**: aprovado, com a decisão de usar apenas o hasher do
Identity registrada em Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/003-usuarios-carteiras/
├── plan.md                          # Este arquivo
├── research.md                      # Phase 0: decisões R1–R10
├── data-model.md                    # Phase 1: domínio, tabelas e configuração
├── quickstart.md                    # Phase 1: roteiro de validação
├── contracts/
│   └── usuarios-carteiras-api.md    # Phase 1: endpoints, JWT, tipos de erro, políticas
├── checklists/
│   └── requirements.md
└── tasks.md                         # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
/
├── .config/dotnet-tools.json                     # novo: dotnet-ef 10.0.15
├── docker-compose.yml                            # api: ConnectionStrings__Bank, Jwt__ChaveAssinatura
├── .env.example                                  # + JWT_CHAVE_ASSINATURA (só desenvolvimento)
└── src/backend/
    ├── Directory.Packages.props                  # + pacotes de R1
    ├── src/
    │   ├── Bank.Domain/
    │   │   ├── Comum/            Resultado.cs, Erro.cs
    │   │   ├── Usuarios/         Usuario.cs, TipoConta.cs, Email.cs, Cpf.cs, Cnpj.cs,
    │   │   │                     Documento.cs, PoliticaSenha.cs, ErrosUsuario.cs
    │   │   ├── Carteiras/        Carteira.cs, ErrosCarteira.cs
    │   │   └── Permissoes/       Permissao.cs, PermissoesPorPerfil.cs
    │   ├── Bank.Application/
    │   │   ├── DependencyInjection.cs            # + serviços e SaldoInicialOptions
    │   │   ├── Abstracoes/       IUsuarioRepositorio.cs, ICarteiraConsultas.cs,
    │   │   │                     IHasherSenha.cs, IEmissorToken.cs
    │   │   ├── Usuarios/         RegistrarUsuario.cs (comando, resposta, serviço),
    │   │   │                     SaldoInicialOptions.cs
    │   │   ├── Autenticacao/     Autenticar.cs (comando, resposta, serviço)
    │   │   ├── Carteiras/        ObterMinhaCarteira.cs
    │   │   └── Observabilidade/  MetricasUsuarios.cs   # meter Bank.Usuarios
    │   ├── Bank.Infrastructure/
    │   │   ├── DependencyInjection.cs            # + DbContext, repositórios, hasher, JWT
    │   │   ├── Persistencia/     BankDbContext.cs, Configuracoes/*.cs,
    │   │   │                     UsuarioRepositorio.cs, CarteiraConsultas.cs,
    │   │   │                     Migrations/*, PersistenciaOptions.cs
    │   │   ├── Seguranca/        HasherSenhaIdentity.cs, EmissorTokenJwt.cs, JwtOptions.cs
    │   │   └── MigracoesNaInicializacao.cs       # IHostedService condicionado (R7)
    │   └── Bank.Api/
    │       ├── Program.cs                        # + AddAutenticacaoJwt, UseAuthentication/Authorization, endpoints
    │       ├── appsettings.json                  # + Carteiras, Jwt (sem chave), Persistencia
    │       ├── appsettings.Development.json      # + ConnectionStrings:Bank, migrações ligadas
    │       ├── Endpoints/        AutenticacaoEndpoints.cs, CarteirasEndpoints.cs
    │       ├── Seguranca/        AutenticacaoExtensions.cs, PoliticasAutorizacao.cs,
    │       │                     PermissaoRequirementHandler.cs
    │       ├── ErrorHandling/    ResultadoHttpExtensions.cs (Erro → ProblemDetails),
    │       │                     ProblemDetailsTraceIdCustomizer.cs (+ types 401/403)
    │       └── OpenApi/          OpenApiExtensions.cs (+ esquema Bearer)
    └── tests/
        ├── Bank.UnitTests/
        │   ├── Domain/           CpfTests.cs, CnpjTests.cs, EmailTests.cs, PoliticaSenhaTests.cs,
        │   │                     UsuarioTests.cs, PermissoesPorPerfilTests.cs
        │   ├── Application/      RegistrarUsuarioTests.cs, AutenticarTests.cs, ObterMinhaCarteiraTests.cs,
        │   │                     SaldoInicialOptionsTests.cs
        │   ├── Infrastructure/   EmissorTokenJwtTests.cs   # + referência a Bank.Infrastructure
        │   └── Api/              PermissaoRequirementHandlerTests.cs
        ├── Bank.IntegrationTests/
        │   ├── PostgresFixture.cs                # Testcontainers, um contêiner por execução
        │   ├── BankApiFactory.cs                 # + connection string, chave JWT de teste
        │   ├── RegistrarTests.cs, LoginTests.cs, MinhaCarteiraTests.cs,
        │   ├── CadastroConcorrenteTests.cs, AtomicidadeCadastroTests.cs,
        │   └── DadosSensiveisTelemetriaTests.cs
        └── Bank.ArchitectureTests/
            └── LayerDependencyTests.cs           # + regras de R10
```

**Structure Decision**: mantém as quatro camadas da spec 002 e organiza cada camada por
conceito de domínio (`Usuarios`, `Carteiras`, `Autenticacao`, `Permissoes`). Os endpoints
continuam em `Bank.Api.Endpoints`, para que as regras de arquitetura existentes os
alcancem; segurança HTTP fica em `Bank.Api.Seguranca`.

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|------|----------------------|-------------------------------------------|
| Só o `PasswordHasher` do ASP.NET Core Identity, sem `IdentityDbContext`/`UserManager` | A constituição pede Identity; a issue #5 fixa o modelo `usuarios`/`carteiras` com `senha_hash` | O Identity completo cria `AspNetUsers` e tabelas auxiliares e conflita com o modelo contratado; um hasher próprio reinventaria criptografia |
| Migrations aplicadas na inicialização (só `Development`/`Testing`) | Subida local e testes de integração sem passo manual | Exigir `dotnet ef database update` antes de cada execução e em cada teste tornaria o quickstart e o CI frágeis; produção continua sem migração automática |
| Hash fictício no login de e-mail inexistente | FR-019: tempo de resposta não pode revelar e-mails cadastrados | Responder imediatamente o 401 vaza a existência do e-mail por diferença de ~100 ms |
