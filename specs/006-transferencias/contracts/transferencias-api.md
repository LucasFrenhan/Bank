# Contrato HTTP: Transferências e extrato — v1.0.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/003 — usuarios-carteiras-api.md](../../003-usuarios-carteiras/contracts/usuarios-carteiras-api.md)

Todos os erros: `application/problem+json` com `type`, `title`, `status`, `traceId` e, em 400,
`errors` por campo. Valores monetários em BRL com 2 casas. Datas em UTC.

## `POST /api/v1/transferencias/destinatarios/consultar`

Confirma o destinatário antes do envio (US1a). O documento vai no corpo, nunca na URL.

**Acesso**: `Bearer`; políticas `UsuarioAutenticado` + `Permissao:RealizarTransferencia` (só PF).

```json
{ "documento": "12.abc.345/01de-35" }
```

**200 OK**

```json
{ "nome": "Cafeteria Grão Especial Ltda", "tipoConta": "PessoaJuridica" }
```

**Erros**: 400 `validacao` (`documento` inválido), 401, 403 `acesso-negado` (PJ ou claims
ausentes), 404 `destinatario-nao-encontrado`, 422 `autotransferencia`.

## `POST /api/v1/transferencias`

**Acesso**: `Bearer`; políticas `UsuarioAutenticado` + `Permissao:RealizarTransferencia`.

**Cabeçalhos**: `Idempotency-Key: <uuid>` (obrigatório; o cliente gera e reaproveita em reenvios).

```json
{
  "documentoDestino": "12ABC34501DE35",
  "valor": 150.50,
  "descricao": "Pagamento almoço"
}
```

| Campo | Regra |
| --- | --- |
| `documentoDestino` | CPF ou CNPJ (numérico/alfanumérico), com ou sem máscara |
| `valor` | ≥ 0,01, no máximo 2 casas decimais |
| `descricao` | opcional, até 200 caracteres (espaços nas extremidades removidos) |

**201 Created** (nova) ou **200 OK** (reenvio idempotente) — mesmo corpo:

```json
{
  "transferenciaId": "019a8f55-2222-7890-abcd-ef1234567890",
  "chaveIdempotencia": "019a8f50-1111-7890-abcd-ef1234567890",
  "pagador": { "usuarioId": "019a8f12-3456-7890-abcd-ef1234567890", "nome": "Lucas Silva" },
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

**Erros**

| Status | `type` | Quando |
| --- | --- | --- |
| 400 | `urn:bank:problema:validacao` | `Idempotency-Key` ausente/inválido; `valor`, `descricao` ou `documentoDestino` inválidos |
| 401 | `urn:bank:problema:nao-autenticado` | Token ausente/inválido |
| 403 | `urn:bank:problema:acesso-negado` | Conta PJ ou token sem as claims exigidas |
| 404 | `urn:bank:problema:destinatario-nao-encontrado` | Documento sem conta no Bank |
| 409 | `urn:bank:problema:idempotencia-divergente` | `Idempotency-Key` já usada com outro destino ou valor |
| 422 | `urn:bank:problema:autotransferencia` | Destino é a própria conta |
| 422 | `urn:bank:problema:saldo-insuficiente` | Saldo menor que o valor |

## `GET /api/v1/transferencias/extrato`

**Acesso**: `Bearer`; política `UsuarioAutenticado` (PF e PJ).

**Query**: `pagina` (≥ 1, padrão 1), `tamanhoPagina` (1–100, padrão 20), `direcao`
(`Entrada` | `Saida`; ausente = todas).

**200 OK**

```json
{
  "itens": [
    {
      "lancamentoId": "019a8f55-4444-7890-abcd-ef1234567890",
      "tipo": "Transferencia",
      "direcao": "Saida",
      "transferenciaId": "019a8f55-2222-7890-abcd-ef1234567890",
      "contraparte": { "nome": "Cafeteria Grão Especial Ltda", "documento": "12ABC34501DE35", "tipoConta": "PessoaJuridica" },
      "valor": 150.50,
      "descricao": "Pagamento almoço",
      "criadoEm": "2026-11-17T18:30:00+00:00"
    },
    {
      "lancamentoId": "019a8f12-5555-7890-abcd-ef1234567890",
      "tipo": "Abertura",
      "direcao": "Entrada",
      "transferenciaId": null,
      "contraparte": null,
      "valor": 10.00,
      "descricao": "Saldo inicial de demonstração",
      "criadoEm": "2026-11-09T12:00:00+00:00"
    }
  ],
  "paginaAtual": 1,
  "tamanhoPagina": 20,
  "totalItens": 2
}
```

- Ordem: mais recente primeiro (`criadoEm` desc, desempate por `lancamentoId` desc).
- `contraparte.documento`: CNPJ completo; CPF mascarado (`•••.982.247-••`).
- **Erros**: 400 `validacao` (parâmetro inválido, com `errors` por parâmetro), 401, 403.

## OpenAPI

Os três endpoints no documento `v1` com `Bearer`; o `POST /api/v1/transferencias` declara o
cabeçalho `Idempotency-Key` (obrigatório, formato uuid) e as respostas 200/201/400/401/403/404/409/422.

## Histórico

- **1.1.0** (2026-11-18): versão atual em [specs/008 — transferencias-api.md](../../008-consulta-transferencia/contracts/transferencias-api.md) — consulta por identificador, `pagador.documento` mascarado e `chaveIdempotencia` anulável (issue #36).
- **1.0.0** (2026-11-17): versão inicial (issues #8 e #13).
