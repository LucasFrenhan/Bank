# Guia de desenvolvimento

Como executar, testar e evoluir o projeto Bank. As regras obrigatórias do projeto estão na
[constituição](../.specify/memory/constitution.md).

## Ambiente local

Infraestrutura de apoio ao desenvolvimento, definida em
[`specs/001-infra-local`](../specs/001-infra-local/spec.md).

**Pré-requisito:** Docker Desktop (Windows/macOS) ou Docker Engine (Linux) em execução,
com Docker Compose v2.

```bash
# (opcional) personalizar portas e credenciais — sem .env, valem os mesmos padrões
cp .env.example .env

# subir e aguardar até todos os serviços estarem saudáveis
docker compose up -d --wait

# ver o estado (todos devem estar "(healthy)")
docker compose ps

# parar mantendo os dados
docker compose down

# reset: parar e apagar todos os dados
docker compose down -v
```

| Serviço | Endereço | Credenciais padrão |
| --- | --- | --- |
| PostgreSQL | `localhost:5432`, banco `bank` | `bank` / `bank_dev_password` |
| RabbitMQ (AMQP) | `localhost:5672`, vhost `bank` | `bank` / `bank_dev_password` |
| RabbitMQ Management | <http://localhost:15672> | `bank` / `bank_dev_password` |
| Aspire Dashboard (traces, métricas e logs) | <http://localhost:18888> | sem login |
| OTLP gRPC / HTTP | `http://localhost:4317` / `http://localhost:4318` | sem autenticação |
| API (`/health`, `/scalar/v1`) | <http://localhost:8080> | sem autenticação |
| Frontend | <http://localhost:3000> | conta criada na própria tela |

- O `docker compose up -d --wait` também constrói e sobe a API e o frontend (use `--build` após alterar
  o código). Para subir só a infraestrutura e rodar a API pelo SDK:
  `docker compose up -d --wait postgres rabbitmq aspire-dashboard`.
- As portas escutam apenas em `127.0.0.1` e podem ser alteradas no `.env`, por exemplo
  `POSTGRES_PORT=55432` se a 5432 já estiver ocupada.
- As credenciais são exclusivamente para desenvolvimento local.
- A telemetria do Aspire Dashboard fica só em memória e é perdida ao reiniciar o contêiner.
- Strings de conexão, endpoints e garantias do ambiente:
  [contrato do ambiente local](../specs/001-infra-local/contracts/ambiente-local.md).
- Roteiro completo de validação: [quickstart](../specs/001-infra-local/quickstart.md).

## Backend (API)

Solução .NET 10 em `src/backend/Bank.slnx`, definida em
[`specs/002-setup-api`](../specs/002-setup-api/spec.md): camadas `Bank.Api`,
`Bank.Application`, `Bank.Domain` e `Bank.Infrastructure`, além dos projetos de teste
unitário, de integração e de arquitetura.

**Pré-requisito:** SDK do .NET 10 (`dotnet --version` na raiz deve mostrar `10.0.4xx`; o
`global.json` fixa a versão).

```bash
# ferramentas locais (dotnet-ef, fixado em .config/dotnet-tools.json)
dotnet tool restore

# compilar e executar todos os testes (os de integração exigem Docker: PostgreSQL via Testcontainers)
dotnet build src/backend/Bank.slnx
dotnet test --solution src/backend/Bank.slnx

# rodar a API pelo SDK (com o ambiente local no ar: banco e telemetria)
docker compose up -d --wait postgres rabbitmq aspire-dashboard
dotnet user-secrets set "Jwt:ChaveAssinatura" "$(openssl rand -base64 48)" --project src/backend/src/Bank.Api  # uma vez
dotnet run --project src/backend/src/Bank.Api
```

| Recurso | SDK (`dotnet run`) | Compose |
| --- | --- | --- |
| Saúde | <http://localhost:5080/health> | <http://localhost:8080/health> |
| Documentação interativa (Scalar) | <http://localhost:5080/scalar/v1> | <http://localhost:8080/scalar/v1> |
| Documento OpenAPI | <http://localhost:5080/openapi/v1.json> | <http://localhost:8080/openapi/v1.json> |

- A documentação (Scalar e OpenAPI) só existe fora do ambiente `Production`.
- Toda resposta de erro usa `ProblemDetails` com `traceId`, que pode ser buscado no
  Aspire Dashboard (<http://localhost:18888>), onde a API aparece como `bank-api`.
- Outra porta pelo SDK: `dotnet run --project src/backend/src/Bank.Api --urls http://localhost:5090`.
- Garantias de erro, endpoints e telemetria:
  [contrato da API base](../specs/002-setup-api/contracts/api-base.md).

### Usuários, carteiras e autenticação

Cadastro (`POST /api/v1/autenticacao/registrar`), login com JWT
(`POST /api/v1/autenticacao/login`) e consulta da carteira (`GET /api/v1/carteiras/minha`),
definidos em [`specs/003-usuarios-carteiras`](../specs/003-usuarios-carteiras/spec.md) —
[contrato](../specs/003-usuarios-carteiras/contracts/usuarios-carteiras-api.md) e
[roteiro de validação](../specs/003-usuarios-carteiras/quickstart.md).

- **Chave JWT** (obrigatória; a API não sobe sem ela, com ≥ 32 bytes): pelo SDK, via
  `dotnet user-secrets` (comando acima); no Compose, `JWT_CHAVE_ASSINATURA` do `.env`, com
  um padrão só de desenvolvimento em `.env.example`.
- **Banco**: fora de `Production`, as migrations são aplicadas automaticamente ao subir a
  API (`Persistencia:AplicarMigracoesNaInicializacao`). Para criar uma migration:

  ```bash
  dotnet ef migrations add NomeDaMigration --project src/backend/src/Bank.Infrastructure \
    --startup-project src/backend/src/Bank.Api --output-dir Persistencia/Migrations
  ```

- **Saldo de demonstração**: fora de `Production`, contas PF nascem com R$ 10,00 e PJ com
  R$ 0,00 (`Carteiras:SaldoInicial`); em `Production` ambos são R$ 0,00 e a API recusa subir
  com outro valor. Os dados de carteiras locais existem só para testes.
- No Scalar, use **Authorize** (esquema `Bearer`) com o `tokenAcesso` do login para chamar
  os endpoints protegidos.
- **Dados do titular**: `GET /api/v1/usuarios/me` devolve nome, e-mail, documento (sem
  máscara), tipo de conta e data de cadastro do usuário do token, com `Cache-Control: no-store`
  — [contrato](../specs/005-usuarios-me/contracts/usuarios-me-api.md) (spec 005).

### Transferências e extrato

Definidos em [`specs/006-transferencias`](../specs/006-transferencias/spec.md) —
[contrato](../specs/006-transferencias/contracts/transferencias-api.md),
[evento](../specs/006-transferencias/contracts/evento-transferencia-concluida.md) e
[roteiro de validação](../specs/006-transferencias/quickstart.md).

- **Transferir** (`POST /api/v1/transferencias`, só PF): corpo `documentoDestino` (CPF ou CNPJ,
  com ou sem máscara), `valor` (≥ 0,01, até 2 casas) e `descricao` opcional (até 200). O
  cabeçalho **`Idempotency-Key`** (UUID gerado pelo cliente) é obrigatório: reenviar com a mesma
  chave e os mesmos destino e valor devolve `200` com o comprovante original, sem novo débito;
  com outro destino ou valor, `409`. Saldo insuficiente e autotransferência → `422`.
- **Consultar destinatário** (`POST /api/v1/transferencias/destinatarios/consultar`, só PF):
  confirma nome e tipo de conta antes de enviar. O documento vai no corpo, nunca na URL.
- **Extrato** (`GET /api/v1/transferencias/extrato`, PF e PJ): `pagina` (padrão 1),
  `tamanhoPagina` (padrão 20, máximo 100) e `direcao` (`Entrada`/`Saida`), mais recente
  primeiro; CPF de terceiros mascarado, CNPJ completo.
- **Razão**: transferências e lançamentos são imutáveis (trigger no banco) e toda carteira tem
  saldo = soma dos lançamentos. O evento `transferencia.concluida` é gravado em `outbox_eventos`
  na mesma transação (a publicação no broker fica para a issue #9).
- **Abertura**: o saldo de demonstração vira um lançamento `Abertura` no cadastro; fora de
  `Production`, ao subir a API, carteiras antigas sem abertura a recebem
  (`Persistencia:CriarLancamentosDeAberturaNaInicializacao`).
- **SQL puro** (bloqueio `FOR UPDATE`, trigger, abertura) fica em arquivos `.sql` em
  `src/backend/src/Bank.Infrastructure/Persistencia/Sql/`, embutidos no assembly (constituição
  v1.1.0).

## Frontend

Aplicação React + TypeScript em `src/frontend/`, definida em
[`specs/004-frontend-base`](../specs/004-frontend-base/spec.md) —
[contrato](../specs/004-frontend-base/contracts/frontend.md) e
[roteiro de validação](../specs/004-frontend-base/quickstart.md). Telas de login, cadastro
PF/PJ, início e perfil, em desktop e mobile, seguindo o [protótipo](design/prototipo.md).

**Pré-requisito:** Node.js 26.13.0 (`src/frontend/.nvmrc`). Para só usar o frontend, basta o
Compose: <http://localhost:3000>.

```bash
cd src/frontend
npm ci

# desenvolvimento: http://localhost:5173, com /api → API pelo SDK (5080) e /otlp → coletor (4318)
npm run dev
# usando a API do Compose em vez do SDK:
BANK_API_URL=http://localhost:8080 npm run dev

# qualidade
npm run lint && npm run typecheck && npm test && npm run build

# ponta a ponta (desktop e mobile, API real): Compose no ar
npx playwright install chromium   # uma vez
npm run e2e                        # contra http://localhost:3000 (E2E_BASE_URL muda o alvo)

# tipos da API a partir do OpenAPI (API no ar; BANK_API_URL muda a origem)
npm run api:tipos
```

- A API e o coletor são acessados pela mesma origem: o proxy do Vite (desenvolvimento) e o
  nginx (Compose) encaminham `/api` e `/otlp`. A API não tem CORS.
- A sessão fica só na aba (`sessionStorage`) e expira com o token (1 hora).
- No Compose a telemetria do navegador está ligada: o frontend aparece no Aspire Dashboard
  como `bank-frontend`, no mesmo trace das chamadas à `bank-api`. Em produção ela vem
  desligada (`VITE_TELEMETRIA_HABILITADA`).
- Métricas no Aspire Dashboard (<http://localhost:18888/metrics>): a tela abre sem recurso
  selecionado — escolha `bank-api` (meter `Bank.Usuarios`: cadastros e logins) ou
  `bank-frontend` (carregamento e navegações).
- Depois de alterar o frontend, reconstrua a imagem: `docker compose up -d --build frontend`.

## Fluxo de desenvolvimento (SDD)

1. Crie a branch da spec a partir de `main` atualizada, com o mesmo nome da pasta da
   spec: `NNN-nome-curto` (ex.: `002-setup-api`).
2. Especifique: `/speckit-specify` (e `/speckit-clarify` se houver ambiguidades).
3. Planeje: `/speckit-plan` — o plano passa pelo *Constitution Check*.
4. Gere as tarefas: `/speckit-tasks` (opcional: `/speckit-analyze` para checar
   consistência entre spec, plano e tarefas).
5. Implemente: `/speckit-implement`.
6. Abra o PR para `main` referenciando a issue (`Closes #N`). O merge é feito
   por merge commit (sem squash), preservando o histórico da branch.

Mudanças sem spec (emendas da constituição, documentação, CI, manutenção) usam a
branch `<tipo>/<issue>-nome-curto`, por exemplo `docs/1-constituicao`.

### Commits

[Conventional Commits](https://www.conventionalcommits.org/):
`tipo(escopo): descrição`, com `tipo` em `feat`, `fix`, `docs`, `test`, `refactor`,
`build`, `ci` ou `chore`.
