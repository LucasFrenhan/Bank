# Quickstart: Usuários (PF/PJ), Carteiras e Autenticação

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/usuarios-carteiras-api.md](contracts/usuarios-carteiras-api.md) | **Modelo**: [data-model.md](data-model.md)

## Pré-requisitos

- Ambiente da spec 002 funcionando (SDK do .NET 10, Docker em execução) — ver
  `docs/desenvolvimento.md`.
- Ferramentas locais restauradas: `dotnet tool restore` (instala `dotnet-ef`).

## Cenário 1 — Build e testes (FR-024, SC-003, SC-004, SC-005, SC-006)

```bash
dotnet build src/backend/Bank.slnx -c Release
dotnet test --solution src/backend/Bank.slnx -c Release
```

**Esperado**: todos os testes aprovados, incluindo os de integração com PostgreSQL em
contêiner (Testcontainers) e as novas regras de arquitetura.

## Cenário 2 — API pelo SDK com banco local

```bash
docker compose up -d --wait postgres aspire-dashboard
dotnet user-secrets set "Jwt:ChaveAssinatura" "$(openssl rand -base64 48)" --project src/backend/src/Bank.Api
dotnet run --project src/backend/src/Bank.Api
```

**Esperado**: a API sobe aplicando a migration `CriarUsuariosECarteiras` (tabelas `usuarios` e
`carteiras` no banco `bank`). Sem a chave JWT, a API não sobe e informa `Jwt:ChaveAssinatura`.

## Cenário 3 — Cadastro PF e PJ (US1)

```bash
curl -i -X POST http://localhost:5080/api/v1/autenticacao/registrar \
  -H "Content-Type: application/json" \
  -d '{"nomeCompleto":"Lucas Silva","email":"Lucas@Exemplo.com.br","documento":"529.982.247-25","tipoConta":"PessoaFisica","senha":"SenhaForte@123"}'

curl -i -X POST http://localhost:5080/api/v1/autenticacao/registrar \
  -H "Content-Type: application/json" \
  -d '{"nomeCompleto":"Empresa Exemplo Ltda","email":"contato@exemplo.com.br","documento":"12.abc.345/01de-35","tipoConta":"PessoaJuridica","senha":"SenhaForte@123"}'
```

**Esperado**: `201` com `saldoInicial` `10.00` (PF) e `0.00` (PJ); e-mail em minúsculas,
documentos `52998224725` e `12ABC34501DE35`. Repetir qualquer um → `409` com
`type` `urn:bank:problema:email-ja-cadastrado`. Trocar o documento PJ por `12ABC34501DE36` →
`400` com erro em `documento`.

## Cenário 4 — Login e carteira (US2, US3)

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/api/v1/autenticacao/login \
  -H "Content-Type: application/json" \
  -d '{"email":"LUCAS@exemplo.com.br","senha":"SenhaForte@123"}' | jq -r .tokenAcesso)

curl -i http://localhost:5080/api/v1/carteiras/minha -H "Authorization: Bearer $TOKEN"
curl -i http://localhost:5080/api/v1/carteiras/minha
```

**Esperado**: login `200` com `expiraEmSegundos` `3600`; carteira `200` com `saldo` `10.00`
e `moeda` `BRL`; sem token → `401` `application/problem+json`. Senha errada no login → `401`
`credenciais-invalidas`, igual ao de um e-mail inexistente.

## Cenário 5 — Documentação e telemetria

- `http://localhost:5080/scalar/v1`: os três endpoints aparecem; autenticar com o token
  (esquema `Bearer`) e executar `GET /api/v1/carteiras/minha`.
- Aspire Dashboard (`http://localhost:18888`): métricas `bank.usuarios.cadastros` (por
  `tipo_conta`) e `bank.autenticacao.logins` (por `resultado`); nos logs e traces não
  aparecem senha, token nem documento (SC-006).

## Cenário 6 — Compose completo

```bash
docker compose up -d --build --wait
curl -i http://localhost:8080/health
```

**Esperado**: `api` saudável usando `JWT_CHAVE_ASSINATURA` do `.env` (ou o padrão de
desenvolvimento de `.env.example`) e o PostgreSQL do Compose; os cenários 3 e 4 funcionam na
porta `8080`.
