# Quickstart: Dados do titular autenticado

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/usuarios-me-api.md](contracts/usuarios-me-api.md)

## Pré-requisitos

Ambiente das specs 001–003 (ver `docs/desenvolvimento.md`).

## Cenário 1 — Testes

```bash
dotnet test --solution src/backend/Bank.slnx
```

**Esperado**: todos aprovados, incluindo os testes do caso de uso e de integração de
`/usuarios/me` (200, 401, 403, 404) e o de dados sensíveis.

## Cenário 2 — Consulta pela API (Compose)

```bash
docker compose up -d --build --wait
EMAIL="titular.$RANDOM@exemplo.com.br"
curl -s -X POST http://localhost:8080/api/v1/autenticacao/registrar -H "Content-Type: application/json" \
  -d "{\"nomeCompleto\":\"Empresa Exemplo Ltda\",\"email\":\"$EMAIL\",\"documento\":\"<CNPJ válido>\",\"tipoConta\":\"PessoaJuridica\",\"senha\":\"SenhaForte@123\"}"
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/autenticacao/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"senha\":\"SenhaForte@123\"}" | sed -E 's/.*"tokenAcesso":"([^"]+)".*/\1/')

curl -i http://localhost:8080/api/v1/usuarios/me -H "Authorization: Bearer $TOKEN"
curl -i http://localhost:8080/api/v1/usuarios/me
```

**Esperado**: `200` com `documento` sem máscara em maiúsculas, `criadoEm` em UTC e
`Cache-Control: no-store`; sem token → `401` `nao-autenticado`.

## Cenário 3 — Documentação

`http://localhost:8080/scalar/v1`: `GET /api/v1/usuarios/me` aparece com o cadeado `Bearer` e
as respostas 200/401/403/404.
