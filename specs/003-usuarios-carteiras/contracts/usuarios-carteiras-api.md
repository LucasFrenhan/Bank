# Contrato HTTP: Usuários, Carteiras e Autenticação — v1.0.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/002-setup-api/contracts/api-base.md](../../002-setup-api/contracts/api-base.md)

Todos os corpos são `application/json` (UTF-8), com nomes em `camelCase`. Todo erro é
`application/problem+json` com `type`, `title`, `status`, `traceId` e, em 400 de validação,
`errors` (spec 002). Valores monetários são números JSON com duas casas decimais, em BRL.

## `POST /api/v1/autenticacao/registrar`

**Acesso**: público.

**Requisição**

```json
{
  "nomeCompleto": "Empresa Exemplo Ltda",
  "email": "Contato@Exemplo.com.br",
  "documento": "12.ABC.345/01DE-35",
  "tipoConta": "PessoaJuridica",
  "senha": "SenhaForte@123"
}
```

| Campo | Obrigatório | Regra |
| --- | --- | --- |
| `nomeCompleto` | sim | 1–150 caracteres após remover espaços nas extremidades |
| `email` | sim | E-mail válido, ≤ 150; normalizado para minúsculas |
| `documento` | sim | CPF (PF) ou CNPJ numérico/alfanumérico (PJ), com ou sem máscara — [data-model](../data-model.md#value-objects-e-enums) |
| `tipoConta` | sim | `PessoaFisica` \| `PessoaJuridica` (exatamente) |
| `senha` | sim | 8–128; maiúscula, minúscula, dígito e caractere especial |

Campos desconhecidos são ignorados (inclusive `saldo`).

**201 Created** — `Location: /api/v1/carteiras/minha`

```json
{
  "usuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "carteiraId": "019a8f12-9999-7890-abcd-ef1234567890",
  "nomeCompleto": "Empresa Exemplo Ltda",
  "email": "contato@exemplo.com.br",
  "documento": "12ABC34501DE35",
  "tipoConta": "PessoaJuridica",
  "saldoInicial": 0.00,
  "moeda": "BRL"
}
```

Fora de `Production`, `saldoInicial` é `10.00` para `PessoaFisica` e `0.00` para
`PessoaJuridica` (configurável — RN-05); em `Production` é sempre `0.00`. Os exemplos da
issue #5 com `1000.00` estão substituídos por este contrato.

**Erros**: `400` (validação, todos os campos inválidos de uma vez), `409` (`email-ja-cadastrado`
ou `documento-ja-cadastrado`).

## `POST /api/v1/autenticacao/login`

**Acesso**: público.

```json
{ "email": "lucas@exemplo.com.br", "senha": "SenhaForte@123" }
```

**200 OK**

```json
{
  "tokenAcesso": "eyJhbGciOiJIUzI1NiIs...",
  "tipoToken": "Bearer",
  "expiraEmSegundos": 3600
}
```

**Token (JWT, `HS256`)**

| Claim | Valor |
| --- | --- |
| `sub` | `usuarioId` |
| `email` | e-mail normalizado |
| `tipo_conta` | `PessoaFisica` \| `PessoaJuridica` |
| `carteira_id` | `carteiraId` |
| `jti`, `iat`, `exp` | identificador do token, emissão, expiração (`iat` + `expiraEmSegundos`) |
| `iss`, `aud` | `bank-api` (configurável) |

**Erros**: `400` (`email`/`senha` ausentes ou vazios), `401` `credenciais-invalidas` —
idêntico para e-mail inexistente e senha incorreta.

## `GET /api/v1/carteiras/minha`

> **Versão atual: 1.1.0** — desde a spec 012 a resposta também traz o objeto `contaBancaria`
> (banco, agência, número, dígito e tipo). Ver
> [specs/012-dados-bancarios-carteira/contracts/carteira-minha.md](../../012-dados-bancarios-carteira/contracts/carteira-minha.md).

**Acesso**: autenticado — `Authorization: Bearer <tokenAcesso>`; política
`UsuarioAutenticado` (claims `sub`, `tipo_conta`, `carteira_id`).

**200 OK**

```json
{
  "carteiraId": "019a8f12-9999-7890-abcd-ef1234567890",
  "usuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "nomeTitular": "Lucas Silva",
  "tipoConta": "PessoaFisica",
  "saldo": 10.00,
  "moeda": "BRL",
  "atualizadoEm": "2026-11-17T18:00:00Z"
}
```

O saldo é sempre lido no momento da consulta (não vem do token).

**Erros**: `401` (sem token, expirado, adulterado, emissor/audiência/algoritmo inválidos —
com cabeçalho `WWW-Authenticate: Bearer`), `403` (token válido sem as claims exigidas),
`404` `carteira-nao-encontrada` (token válido de usuário inexistente na base).

## Tipos de erro (`type`)

Consumidores dependem de `type`, nunca de `detail` (Princípio III).

| `type` | Status | Quando |
| --- | --- | --- |
| `urn:bank:problema:validacao` | 400 | Corpo com campos inválidos (`errors` por campo: `nomeCompleto`, `email`, `documento`, `tipoConta`, `senha`) |
| padrão RFC 9110 (spec 002) | 400 | JSON malformado ou corpo vazio |
| `urn:bank:problema:email-ja-cadastrado` | 409 | E-mail já existe |
| `urn:bank:problema:documento-ja-cadastrado` | 409 | Documento já existe |
| `urn:bank:problema:credenciais-invalidas` | 401 | Login com e-mail ou senha incorretos |
| `urn:bank:problema:nao-autenticado` | 401 | Endpoint protegido sem token válido |
| `urn:bank:problema:acesso-negado` | 403 | Token válido sem claims/política exigidas |
| `urn:bank:problema:carteira-nao-encontrada` | 404 | Usuário do token não existe mais |

Exemplo (`409`):

```json
{
  "type": "urn:bank:problema:documento-ja-cadastrado",
  "title": "Documento já cadastrado",
  "status": 409,
  "detail": "Já existe uma conta com este documento.",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

O `detail` nunca repete o e-mail ou o documento enviados.

## Políticas de autorização (para outras specs)

| Política | Permitido para |
| --- | --- |
| `UsuarioAutenticado` | Qualquer token válido com `sub`, `tipo_conta` e `carteira_id` |
| `Permissao:RealizarTransferencia` | `PessoaFisica` |
| `Permissao:ReceberTransferencia` | `PessoaFisica`, `PessoaJuridica` |
| `Permissao:PagarCobrancaCartao` | `PessoaFisica` |
| `Permissao:GerarCobrancaCartao` | `PessoaJuridica` |

## OpenAPI

Os três endpoints aparecem no documento `v1` com os esquemas acima, as respostas de erro
(`ProducesProblem`/`ProducesValidationProblem`) e o esquema de segurança `Bearer` (HTTP,
`bearerFormat: JWT`) aplicado a `GET /api/v1/carteiras/minha`, permitindo autenticar pelo
Scalar.

## Histórico

- **1.0.0** (2026-11-17): versão inicial (issue #5).
