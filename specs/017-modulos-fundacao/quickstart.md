# Quickstart: validar a fundação do monólito modular

Pré-requisitos: SDK .NET 10, Docker (Testcontainers e Compose), Node 26 para o frontend.

## 1. Build e testes do backend

```bash
dotnet build src/backend/Bank.slnx -c Release
dotnet test --solution src/backend/Bank.slnx -c Release
```

Esperado: tudo verde, com a mesma contagem de testes anteriores mais os testes novos de arquitetura
(regras MOD-01 a MOD-07, prova de detecção, lista de exceções e registro do módulo de exemplo).

## 2. Regras de fronteira

```bash
dotnet test --project src/backend/tests/Bank.ArchitectureTests -c Release
```

Esperado: verde. Para ver a regra falhar, acrescente temporariamente em `Bank.Domain` um uso de um
tipo de `Bank.SharedKernel.Infrastructure` (por exemplo, `IdentificadorConverter<UsuarioId>`): a falha
lista `MOD-07 … → Bank.SharedKernel.Infrastructure.Persistencia.IdentificadorConverter…`. Desfaça a
alteração.

## 3. Sem mudança de comportamento

```bash
# Modelo do EF Core igual às migrations
dotnet ef migrations has-pending-model-changes \
  --project src/backend/src/Bank.Infrastructure --startup-project src/backend/src/Bank.Api
# Esperado: "No changes have been made to the model since the last migration."

git diff --stat main -- src/backend/src/Bank.Infrastructure/Persistencia/Migrations
# Esperado: vazio

# OpenAPI igual (API rodando no Compose)
docker compose up -d --build
cd src/frontend && npm run api:tipos && git diff --exit-code src/api/schema.d.ts
# Esperado: nenhum diff
```

## 4. Imagem e e2e

```bash
docker compose up -d --build        # API e Worker sobem healthy
cd src/frontend && npx playwright test
```

Esperado: e2e verde (mesmo resultado da `main`).

## 5. Estrutura

- `src/backend/src/SharedKernel/Bank.SharedKernel` sem `PackageReference`.
- `src/backend/src/SharedKernel/Bank.SharedKernel.Infrastructure` só com EF Core.
- Nenhuma pasta `src/backend/src/Modulos/` ainda (FR-008).
- `Bank.Api/Modulos/RegistroModulos.cs` e `Bank.Worker/Modulos/RegistroModulos.cs` presentes e
  chamados no `Program.cs`.
- `docs/desenvolvimento.md` com a seção sobre módulos, regras e exceções.
