# Contrato HTTP: Transferências e extrato — v1.1.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/006 — transferencias-api.md v1.0.0](../../006-transferencias/contracts/transferencias-api.md)

Este documento lista só o que muda em relação à v1.0.0; os demais endpoints, regras e erros
continuam valendo. Todas as mudanças nos endpoints existentes são aditivas (MINOR).

## Comprovante (envio e consulta) — alterado

Mesmo corpo para `POST /api/v1/transferencias` (201/200) e `GET /api/v1/transferencias/{id}` (200):

```json
{
  "transferenciaId": "019a8f55-2222-7890-abcd-ef1234567890",
  "chaveIdempotencia": "019a8f50-1111-7890-abcd-ef1234567890",
  "pagador": {
    "usuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
    "nome": "Lucas Silva",
    "documento": "•••.982.247-••"
  },
  "recebedor": {
    "usuarioId": "019a8f99-8888-7890-abcd-ef1234567890",
    "nome": "Cafeteria Grão Especial Ltda",
    "documento": "12ABC34501DE35",
    "tipoConta": "PessoaJuridica"
  },
  "valor": 150.50,
  "descricao": "Pagamento almoço",
  "status": "Concluida",
  "criadoEm": "2026-11-17T18:30:00+00:00"
}
```

| Campo | Mudança |
| --- | --- |
| `pagador.documento` | **Novo**. CPF do pagador mascarado (`•••.982.247-••`), mesma regra do extrato |
| `chaveIdempotencia` | **Passa a aceitar `null`**. No `POST` continua sempre preenchido; no `GET`, `null` quando quem consulta é o recebedor |

## `GET /api/v1/transferencias/{transferenciaId}` — novo

Reabre o comprovante de uma transferência (spec 008).

**Acesso**: `Bearer`; política `UsuarioAutenticado` (PF e PJ). Só o pagador e o recebedor da
transferência a enxergam.

**Rota**: `transferenciaId` — UUID da transferência (texto; formato inválido → 400).

**200 OK**: corpo do comprovante acima.

- Pagador: `chaveIdempotencia` preenchida.
- Recebedor: `chaveIdempotencia` = `null`.

**Erros**

| Status | `type` | Quando |
| --- | --- | --- |
| 400 | `urn:bank:problema:validacao` | `transferenciaId` não é um UUID (`errors.transferenciaId`) |
| 401 | `urn:bank:problema:nao-autenticado` | Token ausente/inválido |
| 403 | `urn:bank:problema:acesso-negado` | Token sem as claims de identidade exigidas |
| 404 | `urn:bank:problema:transferencia-nao-encontrada` | Transferência inexistente **ou** de que o usuário não participa — resposta idêntica nos dois casos |

## OpenAPI

Operação `ConsultarTransferencia` no documento `v1`, com `Bearer` e as respostas 200, 400, 401,
403 e 404. O esquema `ComprovanteResposta` ganha `pagador.documento` e `chaveIdempotencia`
anulável.

## Histórico

- **1.1.0** (2026-11-18): consulta por identificador; `pagador.documento` mascarado no
  comprovante; `chaveIdempotencia` anulável (só na consulta pelo recebedor) — issue #36.
- **1.0.0** (2026-11-17): versão inicial (issues #8 e #13).
