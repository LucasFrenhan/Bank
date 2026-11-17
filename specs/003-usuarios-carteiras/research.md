# Research: Usuários (PF/PJ), Carteiras e Autenticação

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-17

Versões consultadas no NuGet em 2026-11-17. Regra herdada das specs 001/002: versão completa
fixada, evitando majors novas e releases publicadas há poucos dias.

## R1. Pacotes NuGet (justificativa exigida pela constituição)

| Pacote | Versão | Projeto | Justificativa |
| --- | --- | --- | --- |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Infrastructure | Provedor EF Core para PostgreSQL, fixado pela constituição (Persistência). |
| `Microsoft.EntityFrameworkCore.Relational` | 10.0.15 | Infrastructure | Fixa o EF Core em 10.0.15 para todos os consumidores: o Npgsql 10.0.3 depende de EF Core ≥ 10.0.4 e, sem a referência direta, os projetos de teste resolviam 10.0.4 enquanto a Infrastructure compilava com 10.0.15 (aviso MSB3277; identificado na implementação). |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.15 | Infrastructure (`PrivateAssets=all`) | Geração de migrations versionadas (constituição: schema evolui só por migrations). Fixa também a versão do EF Core em 10.0.15. |
| `Microsoft.Extensions.Identity.Core` | 10.0.15 | Infrastructure | `PasswordHasher<TUser>` do ASP.NET Core Identity para o hash da senha (FR-010; R3). |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.15 | Api (validação), Infrastructure (emissão via `Microsoft.IdentityModel.JsonWebTokens`, dependência transitiva) | Autenticação JWT Bearer exigida pela constituição (FR-017 a FR-021; R4). |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | 10.0.15 | Application, Infrastructure | `BindConfiguration` + `ValidateOnStart` para `SaldoInicialOptions`, `JwtOptions` e `PersistenciaOptions` dentro das camadas que as definem, sem depender do ASP.NET Core (FR-013, FR-021; identificado no `/speckit-analyze`). |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.15 | Application | `ILogger<T>` para logs estruturados dos casos de uso (Princípio VI) sem acoplar a camada ao ASP.NET Core (identificado na implementação). |
| `Testcontainers.PostgreSql` | 4.16.0 | IntegrationTests | PostgreSQL real em contêiner nos testes de integração (constituição, Princípio VII). A 4.17.0 saiu em 2026-11-16 e é evitada. |

- **Ferramenta**: `dotnet-ef` 10.0.15 como ferramenta local (`.config/dotnet-tools.json` na
  raiz), para gerar migrations com versão reprodutível.
- **Não adotados**:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (Identity completo com
    `IdentityDbContext`): criaria as tabelas `AspNetUsers`/`AspNetRoles`/… e não o modelo
    `usuarios`/`carteiras` da issue #5; só o hasher do Identity é necessário (R3).
  - `EFCore.NamingConventions`: são duas tabelas; o mapeamento explícito de nomes
    `snake_case` é curto e evita uma dependência.
  - FluentValidation: validações são regras de domínio (CPF, CNPJ, senha) que devem estar no
    Domain sem dependências; um acumulador de erros próprio basta (R6).
  - `AspNetCore.HealthChecks.NpgSql`: incluir o banco no `/health` não é requisito desta
    spec.
  - Bibliotecas de CPF/CNPJ: nenhuma madura suporta o CNPJ alfanumérico com garantia; o
    algoritmo é curto e totalmente coberto por testes (R2).

## R2. Validação de CPF e CNPJ (FR-004 a FR-006)

- **Decision**: value objects no Domain — `Cpf`, `Cnpj` e `Documento` (união dos dois,
  com o tipo de conta) — com fábricas `Criar(string?)` que devolvem resultado tipado
  (`Resultado<Cpf>`), nunca exceção.
- **Normalização** (antes da validação):
  1. remover espaços nas extremidades;
  2. CPF: remover `.` e `-`; CNPJ: remover `.`, `/` e `-`;
  3. CNPJ: converter letras para maiúsculas (`ToUpperInvariant`).
  Qualquer outro caractere torna o documento inválido (não é removido).
- **CPF**: exatamente 11 caracteres `0`–`9`; rejeita os 10 casos de dígito repetido;
  DV pelo módulo 11 tradicional (pesos 10..2 e 11..2; resto < 2 → `0`, senão `11 − resto`).
- **CNPJ** (IN RFB nº 2.229/2024):
  - exatamente 14 caracteres; posições 1–12 em `[0-9A-Z]`, posições 13–14 em `[0-9]`;
  - valor do caractere = código ASCII − 48 (`'0'`→0 … `'9'`→9, `'A'`→17 … `'Z'`→42);
  - DV1: pesos `5,4,3,2,9,8,7,6,5,4,3,2` sobre as 12 primeiras posições; DV2: pesos
    `6,5,4,3,2,9,8,7,6,5,4,3,2` sobre as 13 primeiras; resto < 2 → `0`, senão `11 − resto`;
  - rejeita os 14 caracteres iguais.
  - Com apenas dígitos, o cálculo é idêntico ao do CNPJ numérico — um único algoritmo serve
    aos dois formatos.
- **Vetores de teste conferidos**: `12ABC34501DE35` válido; `12ABC34501DE36` inválido;
  `11222333000181` válido; `52998224725` e `12345678909` CPFs válidos.
- **Rationale**: regra de negócio pura, isolada no Domain e testável sem infraestrutura
  (Princípios II e VII).
- **Alternatives considered**: regex única por formato (não valida DV); validar só no
  endpoint (viola o Princípio II).

## R3. Hash de senha (FR-009, FR-010)

- **Decision**: `PasswordHasher<Usuario>` do ASP.NET Core Identity (formato V3:
  PBKDF2-HMAC-SHA512, sal aleatório de 128 bits, 100.000 iterações), atrás da abstração
  `IHasherSenha` declarada em Application e implementada em Infrastructure. O hash
  autodescritivo é guardado em `usuarios.senha_hash` (`text`). `VerifyHashedPassword`
  devolvendo `SuccessRehashNeeded` regrava o hash após login bem-sucedido.
- **Política de senha** (`PoliticaSenha`, Domain): 8–128 caracteres, ≥ 1 maiúscula,
  ≥ 1 minúscula, ≥ 1 dígito, ≥ 1 caractere que não seja letra nem dígito. Cada regra violada
  gera uma mensagem própria no campo `senha`.
- **Anti-enumeração (FR-019)**: quando o e-mail não existe, o serviço de login ainda executa
  `VerificarAsync` contra um hash fictício fixo (gerado uma vez na inicialização), para que
  o tempo de resposta não revele se o e-mail existe. A resposta 401 é a mesma nos dois casos.
- **Rationale**: atende a constituição ("ASP.NET Core Identity") sem impor o schema do
  Identity, preservando o modelo de dados da issue.
- **Alternatives considered**: Identity completo (R1); Argon2/BCrypt via pacote de terceiro
  (dependência extra sem exigência).

## R4. JWT (FR-017 a FR-021)

- **Emissão** (Infrastructure, `EmissorTokenJwt : IEmissorToken`): `JsonWebTokenHandler`,
  HMAC-SHA256 (`HS256`), claims `sub`, `email`, `tipo_conta`, `carteira_id`, `jti`, `iat`,
  `exp`; `iss` = `Jwt:Emissor`, `aud` = `Jwt:Audiencia`; validade `Jwt:ExpiracaoSegundos`
  (padrão 3600).
- **Validação** (Api): `AddAuthentication().AddJwtBearer` com `MapInboundClaims = false`
  (mantém `sub` e demais nomes), `ValidAlgorithms = [HS256]`, emissor, audiência, tempo de
  vida e chave validados, `ClockSkew = TimeSpan.Zero` (edge case "expirado há 1 s → 401").
- **Configuração** (`JwtOptions`, `ValidateOnStart`): `ChaveAssinatura` com ≥ 32 bytes
  (256 bits) obrigatória; a API não sobe sem ela. Nunca versionada com valor real:
  - SDK: `dotnet user-secrets` no projeto `Bank.Api` (quickstart);
  - Compose: `JWT_CHAVE_ASSINATURA` do `.env`, com padrão de desenvolvimento em
    `.env.example` (mesmo critério das senhas locais da spec 001);
  - Testes: chave aleatória gerada pela `BankApiFactory`.
- **401 × 403**: o desafio do JwtBearer devolve 401 sem corpo, convertido em
  `ProblemDetails` pelo `UseStatusCodePages` já existente (spec 002, R6); o
  `CustomizeProblemDetails` atribui os `type` `urn:bank:problema:nao-autenticado` (401) e
  `urn:bank:problema:acesso-negado` (403) quando o `type` ainda é o padrão. A política padrão
  dos endpoints autenticados (`UsuarioAutenticado`) exige as claims `sub`, `tipo_conta` e
  `carteira_id`; token válido sem elas → 403 `ProblemDetails`.
- **Alternatives considered**: RS256 com par de chaves (sem consumidor externo que valide o
  token, a complexidade de gestão de chaves não se paga agora); refresh token (fora de
  escopo na spec).

## R5. Permissões por perfil (FR-023)

- **Decision**: `PermissoesPorPerfil` no Domain mapeia `TipoConta` → conjunto de
  `Permissao` (`RealizarTransferencia`, `ReceberTransferencia`, `PagarCobrancaCartao`,
  `GerarCobrancaCartao`). A Api registra uma política de autorização por permissão
  (`Permissao:RealizarTransferencia`, …) com um `IAuthorizationRequirement` que lê a claim
  `tipo_conta` e consulta o Domain. Nenhum endpoint desta spec usa essas políticas; a issue #8
  usará `Permissao:RealizarTransferencia`.
- **Teste**: unitário no Domain (matriz completa) e unitário do handler de autorização
  (claim PF/PJ/ausente/inválida).

## R6. Erros e resultados tipados (Princípio III)

- **Decision**: `Resultado` / `Resultado<T>` e `Erro` (código estável + mensagem + campo
  opcional) no Domain. Erros de validação acumulam todos os campos inválidos em uma
  resposta (FR-011). A Api traduz o código em `ProblemDetails` com `type` estável no formato
  `urn:bank:problema:<codigo>` (tabela em [contracts/usuarios-carteiras-api.md](contracts/usuarios-carteiras-api.md)).
- **Corpo da requisição**: os DTOs de entrada usam `string?` em todos os campos (inclusive
  `tipoConta`), para que valores ausentes, nulos ou desconhecidos virem erros por campo
  (`400 ValidationProblemDetails`) e não falha genérica de desserialização. JSON
  malformado continua em 400 pelo pipeline da spec 002.
- **Alternatives considered**: exceções de domínio (proibidas para falhas esperadas pela
  constituição); `type` em URL HTTP (domínio não controlado pelo projeto).

## R7. Persistência

- **Decision**: `BankDbContext` (Infrastructure) com mapeamento explícito
  (`IEntityTypeConfiguration<T>`), nomes `snake_case`, migrations em
  `Bank.Infrastructure/Persistencia/Migrations`.
  - Identificadores UUIDv7 gerados no Domain (`Guid.CreateVersion7()`), não pelo banco.
  - Unicidade: índices únicos `ux_usuarios_email`, `ux_usuarios_documento`,
    `ux_carteiras_usuario_id`; FK `fk_carteiras_usuarios` com `ON DELETE RESTRICT`.
  - `CHECK (saldo >= 0)` (`ck_carteiras_saldo_nao_negativo`), `CHECK (moeda = 'BRL')`
    (`ck_carteiras_moeda`) e `CHECK (tipo_conta IN (...))` (`ck_usuarios_tipo_conta`).
  - Concorrência otimista da carteira pela coluna de sistema `xmin` (token de concorrência
    do Npgsql), já preparando as movimentações da issue #8 sem coluna extra.
- **Atomicidade (RN-04, FR-012)**: usuário e carteira são adicionados ao mesmo contexto e
  persistidos por um único `SaveChangesAsync` (uma transação). Teste de integração injeta
  falha na inserção da carteira e verifica que nenhum usuário foi criado.
- **Unicidade sob concorrência (FR-008, SC-004)**: o serviço faz a verificação prévia (para
  a resposta comum) e o repositório traduz `PostgresException` `23505` pelo nome da
  constraint (`ux_usuarios_email` → `email-ja-cadastrado`; `ux_usuarios_documento` →
  `documento-ja-cadastrado`). A base é a garantia final.
- **Migrations na inicialização**: aplicadas automaticamente (`MigrateAsync`) quando
  `Persistencia:AplicarMigracoesNaInicializacao = true` — ligado em `Development` e
  `Testing`, desligado por padrão. Produção (bundle/script) fica para a spec de deploy.
- **Connection string**: `ConnectionStrings:Bank`. SDK: `appsettings.Development.json`
  aponta para o PostgreSQL local da spec 001 com as credenciais de desenvolvimento já
  documentadas; Compose: variável de ambiente com host `postgres`.
- **Valores monetários**: `decimal` / `numeric(18,2)`; antes de devolver, o valor é
  normalizado para escala 2 (`decimal.Round(v, 2)` + escala fixa), para que o JSON mostre
  `10.00` e `0.00` (contrato).

## R8. Saldo inicial configurável (RN-05, FR-013)

- **Decision**: `SaldoInicialOptions` em Application, seção `Carteiras:SaldoInicial`, com
  validação na inicialização (`ValidateOnStart`): ≥ 0 e no máximo 2 casas decimais; em
  `Production`, ambos MUST ser `0.00` (o validador recebe o `IHostEnvironment`), senão a API
  não sobe.
  - `appsettings.json` (base, vale em `Production`): `0.00` / `0.00`;
  - `appsettings.Development.json`: `PessoaFisica` `10.00`, `PessoaJuridica` `0.00`;
  - `Testing`: os mesmos valores de desenvolvimento, definidos pela `BankApiFactory`.
- **Lançamento de abertura (issue #8)**: os dados de carteiras são só de desenvolvimento e
  testes. A spec de lançamentos criará, na inicialização e apenas fora de `Production`, o
  lançamento de abertura das carteiras sem lançamento correspondente (Clarifications).
- O valor é lido no momento do cadastro e gravado na carteira; mudar a configuração não
  altera carteiras existentes.

## R9. Observabilidade (FR-025, FR-027)

- **Métricas**: meter `Bank.Usuarios` (registrado no `AddTelemetria`):
  - `bank.usuarios.cadastros` (counter) — tag `tipo_conta`;
  - `bank.autenticacao.logins` (counter) — tag `resultado` = `sucesso` | `falha`.
- **Logs**: templates nomeados com `UsuarioId` e `TipoConta`; e-mail, documento, senha e
  token nunca são registrados. Para diagnóstico de conflito, apenas o código do erro.
- **Traces**: spans do EF Core não são adicionados nesta spec (exigiriam pacote extra de
  instrumentação); os spans HTTP já existentes cobrem os três endpoints.
- **Teste (SC-006)**: teste de integração captura logs e spans em memória durante cadastro e
  login e verifica que senha, token e documento não aparecem.

## R10. Testes

- **Unitários** (`Bank.UnitTests`): `Cpf`, `Cnpj` (todos os edge cases da spec, via
  `[Theory]`), `Email`, `PoliticaSenha`, `Usuario.Criar`/`Carteira`, `PermissoesPorPerfil`,
  serviços `RegistrarUsuario`, `Autenticar` e `ObterMinhaCarteira` com dublês das
  abstrações, handler de autorização por permissão.
- **Integração** (`Bank.IntegrationTests`): Testcontainers com `postgres:18.7-alpine` (mesma
  imagem do Compose), um contêiner por execução (fixture de assembly) compartilhado pela
  `BankApiFactory`, com limpeza das tabelas entre testes. Cobre: 201 PF/PJ, 400 por campo,
  409 e-mail/documento, 409 concorrente (SC-004, 50 requisições paralelas), atomicidade,
  200/401/400 do login, 200/401/403 da carteira, token expirado/adulterado/outro algoritmo,
  formato `ProblemDetails` com `traceId` e `type`.
- **Arquitetura** (`Bank.ArchitectureTests`): novas regras — Domain não depende de
  `Microsoft.Extensions.Identity`, `Microsoft.IdentityModel` nem `Npgsql`; Application não
  depende de `Microsoft.IdentityModel`, `Microsoft.AspNetCore.Authentication` nem `Npgsql`;
  Endpoints não dependem de `Npgsql`.
- **CI**: o runner `ubuntu-latest` já tem Docker; nenhuma mudança no workflow além do tempo
  limite, se necessário.
