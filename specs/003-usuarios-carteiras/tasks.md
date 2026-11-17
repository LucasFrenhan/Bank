---

description: "Lista de tarefas da feature 003-usuarios-carteiras"
---

# Tasks: Usuários (PF/PJ), Carteiras e Autenticação

**Input**: Design documents from `specs/003-usuarios-carteiras/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/usuarios-carteiras-api.md](contracts/usuarios-carteiras-api.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios pela constituição (Princípio VII: unitários para toda regra de
domínio e serviço; integração para caminho feliz, erros documentados, 401 e 403; PostgreSQL
real via Testcontainers). Em cada história, os testes são escritos primeiro e devem falhar
antes da implementação.

**Organization**: tarefas agrupadas por história de usuário (US1–US4).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend em `src/backend/` (abreviado abaixo como `backend/`): projetos em
`backend/src/<Projeto>/` e testes em `backend/tests/<Projeto>/`. Arquivos de raiz
(`docker-compose.yml`, `.env.example`, `.config/`, `docs/`) usam o caminho completo.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: pacotes, ferramenta de migrations e referências de projeto

- [X] T001 Adicionar a `backend/Directory.Packages.props` exatamente as versões do R1:
  `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Microsoft.EntityFrameworkCore.Design`
  10.0.15, `Microsoft.Extensions.Identity.Core` 10.0.15,
  `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.15,
  `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.15 (grupo Camadas) e
  `Testcontainers.PostgreSql` 4.16.0 (grupo Testes); atualizar o comentário do arquivo para
  citar também `specs/003-usuarios-carteiras/research.md (R1)`
- [X] T002 Referenciar os pacotes nos projetos: `backend/src/Bank.Infrastructure/Bank.Infrastructure.csproj`
  (Npgsql EF, EF Design com `PrivateAssets=all`, Identity.Core, JwtBearer,
  Options.ConfigurationExtensions), `backend/src/Bank.Application/Bank.Application.csproj`
  (Options.ConfigurationExtensions),
  `backend/src/Bank.Api/Bank.Api.csproj` (JwtBearer, EF Design com `PrivateAssets=all` para
  o `dotnet ef` usar a API como startup project) e
  `backend/tests/Bank.IntegrationTests/Bank.IntegrationTests.csproj`
  (`Testcontainers.PostgreSql`; referência de projeto a `Bank.Infrastructure` para a limpeza
  das tabelas); conferir que `Bank.UnitTests` referencia Domain, Application e Api
- [X] T003 [P] Criar `.config/dotnet-tools.json` na raiz com `dotnet-ef` 10.0.15
  (`dotnet new tool-manifest` + `dotnet tool install dotnet-ef --version 10.0.15`)
- [X] T004 Executar `dotnet build src/backend/Bank.slnx` e confirmar build sem avisos

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: resultados tipados, modelo de domínio, persistência, hash de senha, tradução de
erros e infraestrutura de testes — usados por todas as histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Testes (escrever primeiro, devem falhar)

- [X] T005 [P] Criar `backend/tests/Bank.UnitTests/Domain/CpfTests.cs` com `[Theory]` para
  todos os edge cases de CPF da spec: válidos `52998224725`, `529.982.247-25`,
  `12345678909`, `01234567890` (zero à esquerda preservado; 2º DV `0` por resto < 2);
  inválidos: os 10 com dígitos repetidos,
  1º DV errado, 2º DV errado, 10 e 12 dígitos, letras, `/` ou outros caracteres
- [X] T006 [P] Criar `backend/tests/Bank.UnitTests/Domain/CnpjTests.cs` com `[Theory]` para
  todos os edge cases de CNPJ da spec (R2): válidos `11222333000181`, `11.222.333/0001-81`,
  `12ABC34501DE35`, `12.ABC.345/01DE-35`, `12abc34501de35` (normalizado para
  `12ABC34501DE35`); inválidos `12ABC34501DE36` (2º DV), CNPJ alfanumérico e numérico com
  1º DV errado, `12ABC34501DE3A` (letra no DV), `00000000000000`, `AAAAAAAAAAAAAA`, 13 e 15
  caracteres, letra acentuada, `Ç`, caracteres além de `.`, `/` e `-`
- [X] T007 [P] Criar `backend/tests/Bank.UnitTests/Domain/EmailTests.cs`,
  `PoliticaSenhaTests.cs` e `TipoContaTests.cs` em `backend/tests/Bank.UnitTests/Domain/`:
  e-mail com espaços/maiúsculas normalizado, formato inválido e > 150 caracteres rejeitados;
  senha "8–128 caracteres; maiúscula, minúscula, dígito e caractere especial" com uma
  mensagem por regra violada; `TipoConta` aceita só `PessoaFisica`/`PessoaJuridica`
  (`pessoafisica`, `PF`, `1`, vazio e nulo → erro)
- [X] T008 [P] Criar `backend/tests/Bank.UnitTests/Domain/DocumentoTests.cs` e
  `UsuarioTests.cs`: CPF com `PessoaJuridica` e CNPJ com `PessoaFisica` → erro no campo
  `documento`; `Usuario.Criar` cria exatamente uma carteira com `Saldo` informado, `Moeda`
  `BRL`, `AtualizadoEm == CriadoEm`, ids UUIDv7; nome "1–150 caracteres após `Trim`" (vazio,
  só espaços e 151 caracteres rejeitados); `TipoConta` sem setter público

### Implementação

- [X] T009 [P] Criar `backend/src/Bank.Domain/Comum/Erro.cs` (código estável, mensagem, campo
  opcional, `TipoErro`: `Validacao`, `Conflito`, `NaoAutorizado`, `NaoEncontrado`) e
  `backend/src/Bank.Domain/Comum/Resultado.cs` (`Resultado`, `Resultado<T>`, com lista de
  erros para validação acumulada) (R6)
- [X] T010 [P] Criar `backend/src/Bank.Domain/Usuarios/TipoConta.cs` (enum + `TentarConverter`
  sensível a maiúsculas), `Email.cs` e `PoliticaSenha.cs` conforme
  [data-model.md](data-model.md#value-objects-e-enums) (faz T007 passar)
- [X] T011 [P] Criar `backend/src/Bank.Domain/Usuarios/Cpf.cs` (normaliza removendo `.` e `-`;
  "11 dígitos, DV módulo 11, rejeita dígitos repetidos") (faz T005 passar)
- [X] T012 [P] Criar `backend/src/Bank.Domain/Usuarios/Cnpj.cs`: normaliza removendo `.`, `/`
  e `-` e `ToUpperInvariant`; "14 caracteres `[0-9A-Z]{12}[0-9]{2}`"; valor = ASCII − 48; DV1
  pesos `5,4,3,2,9,8,7,6,5,4,3,2`, DV2 pesos `6,5,4,3,2,9,8,7,6,5,4,3,2`, resto < 2 → `0`,
  senão `11 − resto`; rejeita os 14 caracteres iguais (R2) (faz T006 passar)
- [X] T013 Criar `backend/src/Bank.Domain/Usuarios/Documento.cs` (valida por `TipoConta`,
  expõe `Valor` normalizado) e `ErrosUsuario.cs` com os códigos `validacao`,
  `email-ja-cadastrado`, `documento-ja-cadastrado`, `credenciais-invalidas` (depende de
  T010–T012)
- [X] T014 Criar `backend/src/Bank.Domain/Carteiras/Carteira.cs` (`Saldo` ≥ 0 escala 2,
  `Moeda` = `BRL`, `AtualizadoEm`), `ErrosCarteira.cs` (`carteira-nao-encontrada`) e
  `backend/src/Bank.Domain/Usuarios/Usuario.cs` com `Usuario.Criar(nome, email, documento,
  tipoConta, senhaHash, saldoInicial, agora)` criando usuário e carteira juntos,
  `Guid.CreateVersion7()` e `AtualizarSenhaHash` (para rehash) (faz T008 passar)
- [X] T015 [P] Criar as abstrações em `backend/src/Bank.Application/Abstracoes/`:
  `IUsuarioRepositorio.cs` (`ExisteEmailAsync`, `ExisteDocumentoAsync`, `ObterPorEmailAsync`,
  `AdicionarAsync` → `ResultadoPersistencia` {`Ok`, `EmailDuplicado`, `DocumentoDuplicado`},
  `AtualizarAsync`), `ICarteiraConsultas.cs` (`ObterDoUsuarioAsync(usuarioId)` → DTO de
  leitura), `IHasherSenha.cs` (`GerarHash`, `Verificar` → {`Falha`, `Sucesso`,
  `SucessoRehashNecessario`}) e `IEmissorToken.cs` (`Emitir(Usuario)` → token + expiração)
- [X] T016 [P] Criar `backend/src/Bank.Application/Observabilidade/MetricasUsuarios.cs` com o
  meter `Bank.Usuarios` e os counters `bank.usuarios.cadastros` (tag `tipo_conta`) e
  `bank.autenticacao.logins` (tag `resultado` = `sucesso` | `falha`) via `IMeterFactory`;
  registrar o meter em `backend/src/Bank.Api/Observability/TelemetryExtensions.cs` (R9)
- [X] T017 Criar `backend/src/Bank.Infrastructure/Persistencia/BankDbContext.cs` e
  `Configuracoes/UsuarioConfiguracao.cs` / `CarteiraConfiguracao.cs` com o mapeamento exato de
  [data-model.md](data-model.md#persistência-postgresql): `usuarios` (`id uuid` PK
  `pk_usuarios`; `nome_completo varchar(150) NOT NULL`; `email varchar(150) NOT NULL UNIQUE`
  `ux_usuarios_email`; `documento varchar(14) NOT NULL UNIQUE` `ux_usuarios_documento`;
  `tipo_conta varchar(20) NOT NULL` com `ck_usuarios_tipo_conta`; `senha_hash text NOT NULL`;
  `criado_em timestamptz NOT NULL`) e `carteiras` (`id uuid` PK `pk_carteiras`; `usuario_id`
  `NOT NULL UNIQUE` `ux_carteiras_usuario_id` e FK `fk_carteiras_usuarios` `ON DELETE
  RESTRICT`; `saldo numeric(18,2) NOT NULL` com `ck_carteiras_saldo_nao_negativo`;
  `moeda char(3) NOT NULL DEFAULT 'BRL'` com `ck_carteiras_moeda`; `atualizado_em timestamptz
  NOT NULL`; `xmin` como token de concorrência) (R7)
- [X] T018 Criar `backend/src/Bank.Infrastructure/Persistencia/PersistenciaOptions.cs`
  (`AplicarMigracoesNaInicializacao`), `backend/src/Bank.Infrastructure/MigracoesNaInicializacao.cs`
  (`IHostedService` que executa `MigrateAsync` só quando a opção está ligada) e registrar
  `AddDbContext<BankDbContext>` com `UseNpgsql(ConnectionStrings:Bank)` em
  `backend/src/Bank.Infrastructure/DependencyInjection.cs` (a assinatura passa a receber
  `IConfiguration`; atualizar `backend/src/Bank.Api/Program.cs`) (R7)
- [X] T019 Gerar a migration `CriarUsuariosECarteiras` em
  `backend/src/Bank.Infrastructure/Persistencia/Migrations/` com `dotnet ef migrations add`
  (startup `Bank.Api`) e revisar o SQL (`dotnet ef migrations script`) contra o data-model
- [X] T020 [P] Criar `backend/src/Bank.Infrastructure/Seguranca/HasherSenhaIdentity.cs`
  (`IHasherSenha` sobre `PasswordHasher<Usuario>`, formato V3) e registrá-lo como singleton
  (R3)
- [X] T021 Criar `backend/src/Bank.Infrastructure/Persistencia/UsuarioRepositorio.cs` e
  `CarteiraConsultas.cs`; `AdicionarAsync` persiste usuário e carteira em um único
  `SaveChangesAsync` e traduz `PostgresException` `23505` por `ConstraintName`
  (`ux_usuarios_email` → `EmailDuplicado`, `ux_usuarios_documento` → `DocumentoDuplicado`);
  registrar ambos em `DependencyInjection.cs` (R7)
- [X] T022 Criar `backend/src/Bank.Api/ErrorHandling/ResultadoHttpExtensions.cs`: converte
  `Erro`/`Resultado` em `TypedResults.ValidationProblem` (400, `type`
  `urn:bank:problema:validacao`, `errors` por campo) ou `TypedResults.Problem` com status por
  `TipoErro` e `type` `urn:bank:problema:<codigo>`, `title` fixo por código e `detail` sem
  e-mail/documento (tabela "Tipos de erro" do contrato) (R6)
- [X] T023 [P] Criar `backend/tests/Bank.IntegrationTests/PostgresCompartilhado.cs`
  (`PostgreSqlContainer` com `postgres:18.7-alpine`, um contêiner por execução; cada
  `BankApiFactory` recebe um banco exclusivo — isola as classes que rodam em paralelo sem
  `TRUNCATE`; ajustado na implementação) e atualizar
  `backend/tests/Bank.IntegrationTests/BankApiFactory.cs` para usar a connection string do
  contêiner, `Persistencia:AplicarMigracoesNaInicializacao=true`,
  `Carteiras:SaldoInicial:PessoaFisica=10.00` e uma `Jwt:ChaveAssinatura` aleatória de 48
  bytes; garantir que os testes da spec 002 continuam passando
- [X] T024 Atualizar `backend/src/Bank.Api/appsettings.json` (base, vale em `Production`:
  `Carteiras:SaldoInicial` `PessoaFisica` `0.00` / `PessoaJuridica` `0.00`; `Jwt:Emissor`
  `bank-api`, `Jwt:Audiencia` `bank-api`, `Jwt:ExpiracaoSegundos` `3600`, sem chave;
  `Persistencia:AplicarMigracoesNaInicializacao` `false`),
  `backend/src/Bank.Api/appsettings.Development.json` (`ConnectionStrings:Bank`
  `Host=localhost;Port=5432;Database=bank;Username=bank;Password=bank_dev_password`; migrações
  ligadas; `Carteiras:SaldoInicial:PessoaFisica` `10.00`) e o serviço `api` de
  `docker-compose.yml` (`ConnectionStrings__Bank` com host `postgres` e as variáveis
  `POSTGRES_*` com os mesmos padrões do serviço `postgres`), para o CI `infra-local` continuar
  subindo a API (R7, R8)
- [X] T025 Executar `dotnet test --solution src/backend/Bank.slnx` e confirmar T005–T008
  verdes e testes da spec 002 verdes com o banco em contêiner

**Checkpoint**: domínio, persistência e infraestrutura de testes prontos

---

## Phase 3: User Story 1 - Abrir conta e receber uma carteira (Priority: P1) 🎯 MVP

**Goal**: `POST /api/v1/autenticacao/registrar` cria usuário PF/PJ e carteira atomicamente,
com saldo inicial configurável e erros 400/409 padronizados

**Independent Test**: cadastrar PF e PJ válidos (201, saldo `10.00`/`0.00`), cadastros
inválidos (400 com todos os campos) e duplicados (409), conforme quickstart cenário 3

### Testes (escrever primeiro, devem falhar)

- [X] T026 [P] [US1] Criar `backend/tests/Bank.UnitTests/Application/RegistrarUsuarioTests.cs`
  com dublês de `IUsuarioRepositorio`/`IHasherSenha`: sucesso PF (saldo `10.00`) e PJ (`0.00`)
  pelas opções; todos os campos inválidos devolvidos de uma vez; e-mail e documento já
  existentes → `email-ja-cadastrado` / `documento-ja-cadastrado`; `DocumentoDuplicado` vindo
  do repositório (corrida) → `documento-ja-cadastrado`; senha nunca chega ao repositório em
  texto; counter `bank.usuarios.cadastros` incrementado com `tipo_conta`
- [X] T027 [P] [US1] Criar `backend/tests/Bank.UnitTests/Application/SaldoInicialOptionsTests.cs`:
  valores negativos ou com mais de 2 casas decimais falham na validação; em `Production`,
  qualquer saldo inicial diferente de `0.00` falha; fora de `Production`, `10.00` é aceito
  (FR-013)
- [X] T028 [P] [US1] Criar `backend/tests/Bank.IntegrationTests/RegistrarTests.cs`: 201 PF com
  CPF mascarado (corpo do contrato, `documento` `52998224725`, `saldoInicial` `10.00`, `moeda`
  `BRL`, e-mail em minúsculas, cabeçalho `Location`); 201 PJ com `12.abc.345/01de-35`
  (`documento` `12ABC34501DE35`, `saldoInicial` `0.00`); 400 com `errors` para cada campo
  (incluindo `tipoConta` `PF`, CPF com tipo PJ, `12ABC34501DE36`); 400 para JSON malformado e
  corpo vazio; campo `saldo` extra ignorado; 409 para e-mail com outra caixa e para documento
  com/sem máscara; todas as respostas de erro `application/problem+json` com `traceId` e o
  `type` do contrato; após cada erro, nenhuma linha nova em `usuarios`/`carteiras`
- [X] T029 [P] [US1] Criar `backend/tests/Bank.IntegrationTests/CadastroConcorrenteTests.cs`
  (SC-004): 50 requisições paralelas com o mesmo e-mail e 50 com o mesmo documento → exatamente
  1 `201` e 49 `409` em cada grupo
- [X] T030 [P] [US1] Criar `backend/tests/Bank.IntegrationTests/AtomicidadeCadastroTests.cs`
  (SC-002): fábrica dedicada com `MaxBatchSize(1)` no Npgsql (um comando por INSERT) e um
  `DbCommandInterceptor` de teste que lança exceção quando o comando é o `INSERT` em
  `carteiras` (depois de o `INSERT` em `usuarios` já ter sido executado); o cadastro responde
  500 `ProblemDetails` e não existe usuário nem carteira na base; sem a falha, toda linha de
  `usuarios` tem exatamente uma em `carteiras`

### Implementação

- [X] T031 [P] [US1] Criar `backend/src/Bank.Application/Usuarios/SaldoInicialOptions.cs`
  (seção `Carteiras:SaldoInicial`, `PessoaFisica`/`PessoaJuridica` decimais "≥ 0, até 2
  casas"; em `Production` ambos "MUST ser `0.00`") com validador `IValidateOptions` que recebe
  `IHostEnvironment` e validação na inicialização (`ValidateOnStart`) (R8)
- [X] T032 [US1] Criar `backend/src/Bank.Application/Usuarios/RegistrarUsuario.cs`: comando
  com todos os campos `string?`, resposta (`UsuarioId`, `CarteiraId`, `NomeCompleto`, `Email`,
  `Documento`, `TipoConta`, `SaldoInicial` escala 2, `Moeda`) e serviço que acumula os erros
  de `TipoConta`, `Email`, `Documento`, `PoliticaSenha` e nome, verifica unicidade, gera hash,
  chama `Usuario.Criar` com `TimeProvider` e o saldo das opções, persiste, traduz
  `ResultadoPersistencia` e incrementa a métrica; logs só com `UsuarioId` e `TipoConta`
  (depende de T013–T016, T031)
- [X] T033 [US1] Registrar `RegistrarUsuario`, `SaldoInicialOptions` e `TimeProvider.System`
  em `backend/src/Bank.Application/DependencyInjection.cs` (assinatura com `IConfiguration`
  se necessário; ajustar `Program.cs`)
- [X] T034 [US1] Criar `backend/src/Bank.Api/Endpoints/AutenticacaoEndpoints.cs` com
  `MapAutenticacaoEndpoints` (grupo `/api/v1/autenticacao`, tag OpenAPI `Autenticacao`) e
  `POST /registrar`: DTO de requisição com `string?`, chama o serviço, `201 Created` com
  `Location: /api/v1/carteiras/minha` ou erro via `ResultadoHttpExtensions`; declarar
  `Produces<RegistrarResposta>(201)`, `ProducesValidationProblem()`, `ProducesProblem(409)`;
  mapear em `Program.cs`
- [X] T035 [US1] Executar os testes de T026–T030 e o quickstart cenário 3

**Checkpoint**: cadastro PF/PJ funcional e testado (MVP)

---

## Phase 4: User Story 2 - Entrar no sistema e obter credencial de acesso (Priority: P1)

**Goal**: `POST /api/v1/autenticacao/login` devolve JWT HS256 com `sub`, `email`,
`tipo_conta`, `carteira_id` e validade de 3600 s; 401 idêntico para e-mail inexistente e
senha errada

**Independent Test**: cadastrar um usuário, fazer login (200, claims corretas, `exp − iat =
3600`), login com e-mail em maiúsculas (200), senha errada e e-mail inexistente (401
idênticos)

### Testes (escrever primeiro, devem falhar)

- [X] T036 [P] [US2] Criar `backend/tests/Bank.UnitTests/Application/AutenticarTests.cs`:
  sucesso emite token; e-mail inexistente chama `IHasherSenha.Verificar` contra o hash
  fictício (anti-enumeração, FR-019) e devolve `credenciais-invalidas`; senha errada devolve o
  mesmo erro; `SucessoRehashNecessario` regrava o hash; e-mail/senha ausentes → validação;
  counter `bank.autenticacao.logins` com `resultado` `sucesso`/`falha`
- [X] T037 [P] [US2] Criar `backend/tests/Bank.UnitTests/Infrastructure/EmissorTokenJwtTests.cs`
  (adicionar referência de `Bank.UnitTests` a `Bank.Infrastructure` se necessário): token
  HS256 com `sub`, `email`, `tipo_conta`, `carteira_id`, `jti`, `iat`, `exp = iat +
  ExpiracaoSegundos`, `iss`/`aud` `bank-api`; `JwtOptions` com chave < 32 bytes ou ausente
  falha na validação
- [X] T038 [P] [US2] Criar `backend/tests/Bank.IntegrationTests/LoginTests.cs`: 200 com
  `tipoToken` `Bearer` e `expiraEmSegundos` `3600` e claims decodificadas iguais ao cadastro;
  e-mail em maiúsculas aceito; senha errada e e-mail inexistente → 401 `application/problem+json`
  com `type` `urn:bank:problema:credenciais-invalidas` e corpos iguais (exceto `traceId`);
  e-mail ou senha ausentes → 400 com `errors`

### Implementação

- [X] T039 [P] [US2] Criar `backend/src/Bank.Infrastructure/Seguranca/JwtOptions.cs` (seção
  `Jwt`: `Emissor`, `Audiencia`, `ExpiracaoSegundos` > 0, `ChaveAssinatura` "≥ 32 bytes",
  `ValidateOnStart`) e `EmissorTokenJwt.cs` (`JsonWebTokenHandler`, `HS256`, claims do
  contrato; recebe `TimeProvider` por injeção para `iat`/`exp`, permitindo emitir tokens
  expirados nos testes); registrar em `backend/src/Bank.Infrastructure/DependencyInjection.cs` (R4)
- [X] T040 [US2] Criar `backend/src/Bank.Application/Autenticacao/Autenticar.cs`: comando
  (`Email`, `Senha` `string?`), resposta (`TokenAcesso`, `TipoToken` `Bearer`,
  `ExpiraEmSegundos`) e serviço com normalização do e-mail, hash fictício gerado uma vez
  (singleton) para e-mail inexistente, rehash quando necessário, métrica e logs sem e-mail;
  registrar em `backend/src/Bank.Application/DependencyInjection.cs` (R3)
- [X] T041 [US2] Adicionar `POST /login` em `backend/src/Bank.Api/Endpoints/AutenticacaoEndpoints.cs`
  com `Produces<LoginResposta>(200)`, `ProducesValidationProblem()`, `ProducesProblem(401)`
- [X] T042 [US2] Executar `dotnet user-secrets init --project src/backend/src/Bank.Api`
  (adiciona `UserSecretsId` a `backend/src/Bank.Api/Bank.Api.csproj`); no serviço `api` de
  `docker-compose.yml` adicionar `Jwt__ChaveAssinatura: ${JWT_CHAVE_ASSINATURA:-<padrão de
  desenvolvimento com ≥ 32 bytes>}` e em `.env.example` a variável `JWT_CHAVE_ASSINATURA` com
  aviso "somente desenvolvimento" (R4); executar os testes de T036–T038

**Checkpoint**: cadastro + login funcionais

---

## Phase 5: User Story 3 - Consultar a própria carteira (Priority: P2)

**Goal**: `GET /api/v1/carteiras/minha` autenticado por JWT, com 401/403/404 padronizados

**Independent Test**: com o token de um usuário recém-cadastrado, consultar a carteira
(200, saldo do perfil); sem token, token expirado, adulterado, de outra chave ou outro
algoritmo → 401; token válido sem `carteira_id` → 403

### Testes (escrever primeiro, devem falhar)

- [X] T043 [P] [US3] Criar `backend/tests/Bank.UnitTests/Application/ObterMinhaCarteiraTests.cs`:
  devolve a carteira do `usuarioId` informado; usuário inexistente → `carteira-nao-encontrada`
- [X] T044 [P] [US3] Criar `backend/tests/Bank.IntegrationTests/MinhaCarteiraTests.cs`: 200 com
  corpo do contrato (`saldo` `10.00` PF / `0.00` PJ, `moeda` `BRL`, `atualizadoEm` UTC) e
  dados apenas do próprio usuário (dois usuários, cada token vê só a sua); 401
  `application/problem+json` (`type` `urn:bank:problema:nao-autenticado`, `WWW-Authenticate:
  Bearer`) para: sem token, token expirado há 1 s (emitido com `TimeProvider` de teste),
  assinatura adulterada, chave diferente, `alg` `none`/`HS512`, emissor ou audiência errados;
  403 (`urn:bank:problema:acesso-negado`) para token válido sem `carteira_id` ou sem
  `tipo_conta`; 404 `carteira-nao-encontrada` para token válido de usuário removido da base;
  saldo alterado direto na base depois do login aparece atualizado na consulta (edge case
  "saldo atual")

### Implementação

- [X] T045 [P] [US3] Criar `backend/src/Bank.Application/Carteiras/ObterMinhaCarteira.cs`
  (consulta via `ICarteiraConsultas`, resposta `CarteiraId`, `UsuarioId`, `NomeTitular`,
  `TipoConta`, `Saldo` escala 2, `Moeda`, `AtualizadoEm`) e registrá-lo em
  `backend/src/Bank.Application/DependencyInjection.cs`
- [X] T046 [US3] Criar `backend/src/Bank.Api/Seguranca/AutenticacaoExtensions.cs`:
  `AddAutenticacaoJwt` com `AddJwtBearer` (`MapInboundClaims = false`, `ValidAlgorithms =
  [HS256]`, emissor, audiência, tempo de vida e chave de `JwtOptions`, `ClockSkew =
  TimeSpan.Zero`) e política `UsuarioAutenticado` exigindo `sub`, `tipo_conta` e
  `carteira_id`; `UseAuthentication()`/`UseAuthorization()` em `backend/src/Bank.Api/Program.cs`
  depois de `UseTratamentoDeErros` (R4)
- [X] T047 [US3] Atualizar `backend/src/Bank.Api/ErrorHandling/ProblemDetailsTraceIdCustomizer.cs`
  para atribuir `type` `urn:bank:problema:nao-autenticado` (401) e
  `urn:bank:problema:acesso-negado` (403) quando o `type` for o padrão do framework, e
  atualizar `backend/tests/Bank.UnitTests/ErrorHandling/ProblemDetailsTraceIdCustomizerTests.cs`
- [X] T048 [US3] Criar `backend/src/Bank.Api/Endpoints/CarteirasEndpoints.cs` com
  `GET /api/v1/carteiras/minha` (`RequireAuthorization("UsuarioAutenticado")`), lendo `sub`
  de `HttpContext.User` e chamando o serviço; `Produces<MinhaCarteiraResposta>(200)`,
  `ProducesProblem(401)`, `ProducesProblem(403)`, `ProducesProblem(404)`; mapear em `Program.cs`
- [X] T049 [US3] Executar os testes de T043–T044 e o quickstart cenário 4

**Checkpoint**: cadastro, login e consulta funcionais

---

## Phase 6: User Story 4 - Permissões por perfil (Priority: P3)

**Goal**: matriz RN-06 no Domain e políticas `Permissao:*` disponíveis para as issues
seguintes

**Independent Test**: testes unitários da matriz PF/PJ e do handler de autorização

### Testes (escrever primeiro, devem falhar)

- [X] T050 [P] [US4] Criar `backend/tests/Bank.UnitTests/Domain/PermissoesPorPerfilTests.cs`:
  PF tem `RealizarTransferencia`, `ReceberTransferencia`, `PagarCobrancaCartao` e não tem
  `GerarCobrancaCartao`; PJ tem `ReceberTransferencia`, `GerarCobrancaCartao` e não tem
  `RealizarTransferencia` nem `PagarCobrancaCartao`
- [X] T051 [P] [US4] Criar `backend/tests/Bank.UnitTests/Api/PermissaoRequirementHandlerTests.cs`:
  claim `tipo_conta` PF/PJ autoriza conforme a matriz; claim ausente ou valor desconhecido
  não autoriza

### Implementação

- [X] T052 [P] [US4] Criar `backend/src/Bank.Domain/Permissoes/Permissao.cs` e
  `PermissoesPorPerfil.cs` (R5)
- [X] T053 [US4] Criar `backend/src/Bank.Api/Seguranca/PermissaoRequirementHandler.cs`
  (`PermissaoRequirement` + handler que lê `tipo_conta` e consulta o Domain) e
  `PoliticasAutorizacao.cs` registrando `Permissao:RealizarTransferencia`,
  `Permissao:ReceberTransferencia`, `Permissao:PagarCobrancaCartao` e
  `Permissao:GerarCobrancaCartao` em `AddAutenticacaoJwt`
- [X] T054 [US4] Executar os testes de T050–T051

**Checkpoint**: todas as histórias funcionais

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T055 [P] Adicionar regras a `backend/tests/Bank.ArchitectureTests/LayerDependencyTests.cs`
  (R10): Domain não depende de `Microsoft.Extensions.Identity`, `Microsoft.IdentityModel` nem
  `Npgsql`; Application não depende de `Microsoft.IdentityModel`,
  `Microsoft.AspNetCore.Authentication` nem `Npgsql`; Endpoints não dependem de `Npgsql`
- [X] T056 [P] Criar `backend/tests/Bank.IntegrationTests/DadosSensiveisTelemetriaTests.cs`
  (SC-006, FR-025): capturar logs (provedor em memória) e spans (`OpenTelemetry.Exporter.InMemory`)
  durante cadastro, login com falha e com sucesso e consulta; nenhum contém a senha, o token,
  o documento (com ou sem máscara) nem o e-mail; métricas `bank.usuarios.cadastros` e
  `bank.autenticacao.logins` registradas com as tags corretas
- [X] T057 [P] Atualizar `backend/src/Bank.Api/OpenApi/OpenApiExtensions.cs` com o esquema de
  segurança `Bearer` (HTTP, `bearerFormat: JWT`) aplicado aos endpoints com autorização, e
  estender `backend/tests/Bank.IntegrationTests/OpenApiTests.cs` para verificar os três
  endpoints, os esquemas de resposta e o esquema `Bearer`
- [X] T058 [P] Atualizar o cabeçalho de `docker-compose.yml` para citar
  `specs/003-usuarios-carteiras/contracts/usuarios-carteiras-api.md` e executar
  `docker compose up -d --build --wait` confirmando a API saudável com banco e chave JWT do
  Compose (quickstart cenário 6; as variáveis foram adicionadas em T024 e T042)
- [X] T059 [P] Atualizar `docs/desenvolvimento.md` com `dotnet tool restore`, user-secrets da
  chave JWT, migrations (`dotnet ef migrations add` / aplicação automática em
  `Development`) e link para o contrato desta spec
- [X] T060 Executar `dotnet build` e `dotnet test --solution src/backend/Bank.slnx -c Release`
  completos e todos os cenários de [quickstart.md](quickstart.md) (1 a 6), registrando
  divergências na spec/plan/contrato (constituição, Princípio I); medir o p95 de 100
  requisições de cada endpoint no ambiente local (SC-007) e validar manualmente o tempo de
  cadastro + login pelo Scalar (SC-001), registrando os resultados neste arquivo

### Registro da validação (2026-11-17)

- **Build/testes** (`-c Release`): 0 avisos; 213 testes aprovados (unitários, integração com
  PostgreSQL via Testcontainers e arquitetura).
- **Compose** (cenário 6): `docker compose up -d --build --wait` com os 4 serviços
  saudáveis; cenários 3 e 4 executados na porta 8080 com os resultados esperados (201 PF
  `10.00` / PJ `12ABC34501DE35` `0.00`, 409 `email-ja-cadastrado`, 400 para
  `12ABC34501DE36`, login, carteira, 401 `nao-autenticado` com `WWW-Authenticate: Bearer`);
  OpenAPI com o esquema `Bearer`.
- **SC-007** (100 requisições sequenciais por endpoint, Compose local): registrar p95 0,042 s;
  login p95 0,033 s; minha carteira p95 0,004 s — todos < 1 s. Os 100 cadastros gerados têm
  exatamente uma carteira cada.
- **SC-001** (cadastro + login pelo Scalar em < 2 min): validação manual pendente pela pessoa
  revisora.
- **Cenário 5** (métricas e ausência de dados sensíveis): coberto automaticamente por
  `DadosSensiveisTelemetriaTests`; conferência visual no Aspire Dashboard pendente.

### Ajustes feitos na implementação

- `Usuario.Criar` não recebe `tipoConta`: o tipo vem do `Documento` já validado, o que torna
  impossível criar PF com CNPJ (T014).
- Configuração do ambiente `Testing` em `backend/src/Bank.Api/appsettings.Testing.json`
  (saldo PF `10.00`, migrações ligadas) em vez da `BankApiFactory`, para que o teste de
  `Production` da spec 002 continue usando os valores base `0.00` (T023/T024).
- `AddApplication(ambienteDeProducao)` recebe o ambiente da raiz de composição, evitando
  depender de `Microsoft.Extensions.Hosting.Abstractions` na Application (T031/T033).
- Pacotes adicionados com justificativa no R1: `Microsoft.Extensions.Logging.Abstractions`
  (logs estruturados na Application) e `Microsoft.EntityFrameworkCore.Relational` (fixa o EF
  Core em 10.0.15 — o Npgsql 10.0.3 puxava 10.0.4, gerando conflito MSB3277).
- `ClaimsToken` (Infrastructure) centraliza os nomes das claims usados pelo emissor e pelas
  políticas da API.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências
- **Foundational (Phase 2)**: depende do Setup; bloqueia todas as histórias
- **US1 (Phase 3)**: depende da Foundational — MVP
- **US2 (Phase 4)**: depende da Foundational; os testes de integração cadastram usuários pelo
  endpoint da US1 (ou inserem direto na base, se a US2 for feita antes)
- **US3 (Phase 5)**: depende da US2 (precisa do token emitido e de `JwtOptions`)
- **US4 (Phase 6)**: depende só da Foundational para o Domain (T050, T052); o handler (T053)
  depende de T046 (US3)
- **Polish (Phase 7)**: depois das histórias desejadas

### Within Each Phase

- Testes antes da implementação, falhando primeiro
- Domain → Application → Infrastructure → Api
- T017 → T018 → T019 (migration depende do mapeamento e do registro do DbContext)

### Parallel Opportunities

- Setup: T003 paralelo a T001/T002
- Foundational: T005–T008 em paralelo; T009–T012 em paralelo; T015, T016, T020, T023 em
  paralelo com o domínio
- US1: T026–T030 em paralelo; T031 paralelo aos testes
- US2: T036–T038 em paralelo; T039 paralelo a T040 até o registro
- US3: T043, T044, T045 em paralelo
- US4: T050–T052 em paralelo e pode correr junto com US2/US3
- Polish: T055–T059 em paralelo

---

## Parallel Example: Foundational

```text
T005 CpfTests.cs        T006 CnpjTests.cs       T007 Email/Senha/TipoConta tests
T011 Cpf.cs             T012 Cnpj.cs            T010 TipoConta/Email/PoliticaSenha
```

## Parallel Example: User Story 1

```text
T026 RegistrarUsuarioTests.cs   T028 RegistrarTests.cs   T029 CadastroConcorrenteTests.cs
T030 AtomicidadeCadastroTests.cs   T031 SaldoInicialOptions.cs
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 e Phase 2
2. Phase 3 (US1) → validar cadastro PF/PJ, 400 e 409 (quickstart cenário 3)
3. **PARAR e VALIDAR**

### Incremental Delivery

1. US1 → cadastro
2. US2 → login (entregável mínimo da issue #6 junto com US1)
3. US3 → primeiro endpoint protegido (401/403)
4. US4 → políticas prontas para a issue #8
5. Polish → arquitetura, telemetria sensível, OpenAPI, Compose e docs

### Commits

Conventional Commits por fase, no padrão das specs anteriores (por exemplo
`feat(usuarios-carteiras): ...`, `test(usuarios-carteiras): ...`); PR com `Closes #5` e
`Closes #6`.
