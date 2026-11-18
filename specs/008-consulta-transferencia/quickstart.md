# Quickstart: Consulta de transferência por identificador

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/transferencias-api.md](contracts/transferencias-api.md)

## Pré-requisitos

- Ambiente da spec 006 (Docker; SDK do .NET 10 para rodar os testes).

## Cenário 1 — Testes automatizados

```bash
cd src/backend
dotnet build
dotnet test
```

**Esperado**: testes unitários de `ConsultarTransferencia`, integração de `ConsultaTransferenciaTests`
(200 pagador/recebedor, 400, 401, 403, 404 terceiro = 404 inexistente) e testes de arquitetura,
OpenAPI e telemetria aprovados.

## Cenário 2 — Manual pela API do Compose

```bash
docker compose up -d --build --wait
```

1. Cadastre e entre com uma PF **A**, uma PJ **B** e uma PF **C** (`POST /api/v1/autenticacao/...`).
2. Com o token de A, `POST /api/v1/transferencias` para o CNPJ de B (com `Idempotency-Key`) →
   201; o comprovante traz `pagador.documento` mascarado. Guarde `transferenciaId`.
3. `GET /api/v1/transferencias/{transferenciaId}`:

| Token | Esperado |
| --- | --- |
| A | 200, mesmo corpo do passo 2 (inclusive `chaveIdempotencia`) |
| B | 200, `chaveIdempotencia: null`, `pagador.documento` mascarado |
| C | 404 `transferencia-nao-encontrada` |
| A, com um UUID aleatório | 404 idêntico ao de C (exceto `traceId`) |
| A, com `abc` | 400 `validacao` em `transferenciaId` |
| sem token | 401 |

4. Confira que os saldos de A e B não mudaram depois das consultas.

## Cenário 3 — OpenAPI e telemetria

- Scalar (<http://localhost:8080/scalar/v1>): operação `ConsultarTransferencia` com 200/400/401/403/404.
- Aspire Dashboard (<http://localhost:18888/metrics>), recurso `bank-api`, meter
  `Bank.Transferencias`: `bank.transferencias.consultas` com os rótulos `encontrada`,
  `nao-encontrada` e `invalida`; nenhum documento, nome ou e-mail nos traces e logs.
