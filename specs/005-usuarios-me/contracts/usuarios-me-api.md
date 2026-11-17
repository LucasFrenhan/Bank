# Contrato HTTP: Dados do titular autenticado — v1.0.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/003 — usuarios-carteiras-api.md](../../003-usuarios-carteiras/contracts/usuarios-carteiras-api.md)

## `GET /api/v1/usuarios/me`

**Acesso**: autenticado — `Authorization: Bearer <tokenAcesso>`; política `UsuarioAutenticado`
(claims `sub`, `tipo_conta`, `carteira_id`). O titular é sempre o do token.

**200 OK** — `Cache-Control: no-store`

```json
{
  "usuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "nomeCompleto": "Empresa Exemplo Ltda",
  "email": "contato@exemplo.com.br",
  "documento": "12ABC34501DE35",
  "tipoConta": "PessoaJuridica",
  "criadoEm": "2026-11-09T12:00:00+00:00"
}
```

| Campo | Tipo | Regra |
| --- | --- | --- |
| `usuarioId` | string (uuid) | Identificador do titular |
| `nomeCompleto` | string | Nome completo (PF) ou razão social (PJ) |
| `email` | string | E-mail normalizado (minúsculas) |
| `documento` | string | Sem máscara: CPF com 11 dígitos; CNPJ com 14 caracteres alfanuméricos em maiúsculas. Máscara de exibição é responsabilidade do cliente |
| `tipoConta` | string | `PessoaFisica` \| `PessoaJuridica` |
| `criadoEm` | string (date-time) | Data/hora do cadastro em UTC |

## Erros (`application/problem+json`, com `traceId`)

| `type` | Status | Quando |
| --- | --- | --- |
| `urn:bank:problema:nao-autenticado` | 401 | Sem token, expirado, adulterado, emissor/audiência/algoritmo inválidos (com `WWW-Authenticate: Bearer`) |
| `urn:bank:problema:acesso-negado` | 403 | Token válido sem `sub`, `tipo_conta` ou `carteira_id`, ou `sub` que não é um identificador |
| `urn:bank:problema:usuario-nao-encontrado` | 404 | Token válido de usuário que não existe mais na base |

## OpenAPI

Publicado no documento `v1` com `TitularResposta`, as respostas 200/401/403/404 e o esquema
de segurança `Bearer`.

## Histórico

- **1.0.0** (2026-11-17): versão inicial (issue #18).
