# Contrato HTTP: Autenticação e sessão — usuários-carteiras v1.1.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/003-usuarios-carteiras/contracts/usuarios-carteiras-api.md](../../003-usuarios-carteiras/contracts/usuarios-carteiras-api.md) (v1.0.0)

Mudança **aditiva** sobre a v1.0.0: o corpo do login ganha dois campos, o login passa a emitir um cookie e
surgem `renovar` e `sair`. `registrar` não muda. Erros seguem `application/problem+json` com `traceId`
(spec 002).

## Cookie de renovação

| Atributo | Valor |
| --- | --- |
| Nome | `bank_renovacao` |
| Valor | 43 caracteres Base64Url (32 bytes aleatórios), opaco |
| `Path` | `/api/v1/autenticacao` |
| `HttpOnly`, `Secure` | sempre (inclusive `http://localhost`) |
| `SameSite` | `Strict` |
| `Max-Age` | segundos até o vencimento da credencial (≤ inatividade e ≤ limite absoluto) |
| `Domain` | ausente |

Remoção: `bank_renovacao=; Path=/api/v1/autenticacao; Max-Age=0; HttpOnly; Secure; SameSite=Strict`.

O valor nunca aparece em corpo de resposta, log, trace, métrica ou mensagem de erro.

## `POST /api/v1/autenticacao/login` (alterado)

**Acesso**: público. **Requisição**: inalterada (`email`, `senha`). Se o navegador enviar um
`bank_renovacao` válido, essa sessão é revogada (motivo `novo-login`) antes de criar a nova.

**200 OK**

```http
Set-Cookie: bank_renovacao=<valor>; Path=/api/v1/autenticacao; Max-Age=900; HttpOnly; Secure; SameSite=Strict
Cache-Control: no-store
```

```json
{
  "tokenAcesso": "eyJhbGciOi…",
  "tipoToken": "Bearer",
  "expiraEmSegundos": 300,
  "sessaoExpiraPorInatividadeEmSegundos": 900,
  "sessaoExpiraEmSegundos": 28800
}
```

| Campo | Novo? | Significado |
| --- | --- | --- |
| `tokenAcesso`, `tipoToken`, `expiraEmSegundos` | não | como na v1.0.0; `expiraEmSegundos` passa a 300 por padrão |
| `sessaoExpiraPorInatividadeEmSegundos` | sim | segundos até a sessão vencer se não houver renovação |
| `sessaoExpiraEmSegundos` | sim | segundos até o limite absoluto da sessão |

Claims do token: as da v1.0.0 mais `sid` (identificador da sessão, UUID).

**Erros**: inalterados (400 validação, 401 `urn:bank:problema:credenciais-invalidas`).

## `POST /api/v1/autenticacao/renovar` (novo)

**Acesso**: público; autentica pelo cookie. **Requisição**: sem corpo; não exige `Authorization`.

**200 OK**: mesmo corpo e cabeçalhos do login, com uma credencial **nova** no `Set-Cookie`. A credencial
enviada deixa de valer (aceita de novo apenas dentro da janela de tolerância de 30 s, e sempre devolvendo
uma credencial nova).

**401 Unauthorized** — único resultado de recusa, qualquer que seja o motivo (cookie ausente, desconhecido,
vencido, sessão revogada, inatividade, limite absoluto ou reuso detectado):

```http
Set-Cookie: bank_renovacao=; Path=/api/v1/autenticacao; Max-Age=0; HttpOnly; Secure; SameSite=Strict
Content-Type: application/problem+json
```

```json
{
  "type": "urn:bank:problema:nao-autenticado",
  "title": "Não autenticado.",
  "status": 401,
  "traceId": "00-…"
}
```

Efeito colateral do reuso (credencial rotacionada há mais de 30 s): a sessão inteira é revogada.

## `POST /api/v1/autenticacao/sair` (novo)

**Acesso**: público; usa o cookie se houver. **Requisição**: sem corpo.

**204 No Content** sempre (idempotente), com o `Set-Cookie` de remoção. Se o cookie corresponder a uma sessão
ativa, ela é revogada (motivo `logout`). O token de acesso já emitido continua válido até expirar.

## Tipos de erro

Nenhum novo. `urn:bank:problema:nao-autenticado` (401) passa a ser também a resposta de `renovar`.

## Configuração

| Chave | Padrão | Compose (`.env`) |
| --- | --- | --- |
| `Jwt:ExpiracaoSegundos` | 300 | `JWT_EXPIRACAO_SEGUNDOS` |
| `Sessao:InatividadeSegundos` | 900 | `SESSAO_INATIVIDADE_SEGUNDOS` |
| `Sessao:LimiteAbsolutoHoras` | 8 | `SESSAO_LIMITE_ABSOLUTO_HORAS` |
| `Sessao:ToleranciaReusoSegundos` | 30 | `SESSAO_TOLERANCIA_REUSO_SEGUNDOS` |
| `Sessao:RetencaoDias` | 7 | — |
| `LimpezaSessoes:IntervaloMinutos` (Worker) | 60 | — |
| `LimpezaSessoes:Habilitada` (Worker) | true (false em `Testing`) | — |
