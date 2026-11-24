# Contrato HTTP: Cartões, cobranças, faturas e recebíveis — v1.0.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/003 — usuarios-carteiras-api.md](../../003-usuarios-carteiras/contracts/usuarios-carteiras-api.md)

Erros em `application/problem+json` com `type` (`urn:bank:problema:<codigo>`), `title`, `status` e
`traceId`; em 400, `errors` por campo. Valores em BRL com 2 casas. Instantes em UTC; datas
`AAAA-MM-DD` são dias de Brasília. Todo endpoint exige `Bearer` e responde 401 `nao-autenticado` sem
token válido e 403 `acesso-negado` sem a permissão. Paginação: `pagina` (padrão 1) e `tamanhoPagina`
(padrão 20, máximo 100), com `itens`, `paginaAtual`, `tamanhoPagina` e `totalItens`, como no extrato.
`Idempotency-Key` é um UUID obrigatório onde indicado.

| Método e rota | Permissão |
| --- | --- |
| `POST /api/v1/cartoes` | `UsarCartaoCredito` (PF) |
| `GET /api/v1/cartoes/meu` | `UsarCartaoCredito` |
| `GET /api/v1/cartoes/meu/dados` | `UsarCartaoCredito` |
| `POST /api/v1/cobrancas` | `GerarCobrancaCartao` (PJ) |
| `GET /api/v1/cobrancas/minhas` | `GerarCobrancaCartao` |
| `GET /api/v1/cobrancas/{cobrancaId}` | autenticado |
| `POST /api/v1/cobrancas/{cobrancaId}/cancelar` | `GerarCobrancaCartao` |
| `POST /api/v1/cobrancas/{cobrancaId}/pagar` | `PagarCobrancaCartao` (PF) |
| `GET /api/v1/faturas` | `UsarCartaoCredito` |
| `GET /api/v1/faturas/{competencia}` | `UsarCartaoCredito` |
| `POST /api/v1/faturas/{competencia}/pagar` | `UsarCartaoCredito` |
| `GET /api/v1/recebiveis` | `GerenciarRecebiveis` (PJ) |
| `GET /api/v1/recebiveis/{recebivelId}` | `GerenciarRecebiveis` |
| `POST /api/v1/recebiveis/{recebivelId}/antecipar` | `GerenciarRecebiveis` |

## Cartões

### `POST /api/v1/cartoes`

```json
{ "bandeira": "001" }
```

**201** — corpo de `GET /cartoes/meu`. **Erros**: 400 (`bandeira` ausente ou diferente de `001`/`002`),
409 `cartao-ja-existente`.

### `GET /api/v1/cartoes/meu`

```json
{
  "cartaoId": "019a8f77-5555-7890-abcd-ef1234567890",
  "bandeira": { "codigo": "001", "nome": "Visa" },
  "nomeImpresso": "LUCAS H P FRENHAN",
  "ultimosDigitos": "7899",
  "validade": "11/31",
  "limiteTotal": 5000.00,
  "limiteDisponivel": 4700.10,
  "comprasALancar": 299.90,
  "criadoEm": "2026-11-24T12:00:00Z"
}
```

**Erros**: 404 `cartao-nao-encontrado`.

### `GET /api/v1/cartoes/meu/dados`

Cabeçalho de resposta `Cache-Control: no-store`.

```json
{
  "nomeImpresso": "LUCAS H P FRENHAN",
  "numero": "4000001234567899",
  "validade": "11/31",
  "cvv": "123",
  "bandeira": { "codigo": "001", "nome": "Visa" }
}
```

**Erros**: 404 `cartao-nao-encontrado`.

## Cobranças

### `POST /api/v1/cobrancas`

```json
{ "valor": 299.90, "descricao": "Assinatura Mensal Café Especial - Kit 3 Pacotes" }
```

**201** — corpo da consulta. **Erros**: 400 (`valor`, `descricao`).

### `GET /api/v1/cobrancas/{cobrancaId}`

```json
{
  "cobrancaId": "019a8f77-1111-7890-abcd-ef1234567890",
  "lojista": {
    "usuarioId": "019a8f99-8888-7890-abcd-ef1234567890",
    "nome": "Cafeteria Grão Especial LTDA",
    "documento": "98765432000199"
  },
  "valor": 299.90,
  "descricao": "Assinatura Mensal Café Especial - Kit 3 Pacotes",
  "status": "Pendente",
  "criadoEm": "2026-11-17T16:30:00Z",
  "pagaEm": null,
  "canceladaEm": null,
  "pagamento": null,
  "ultimaRecusa": null
}
```

- `status`: `Pendente`, `Paga`, `Cancelada`.
- `pagamento` (`bandeira { codigo, nome }`, `ultimosDigitosCartao`, `codigoAutorizacao`): só para o
  emissor e o pagador de cobrança `Paga`; nulo para os demais.
- `ultimaRecusa` (`motivo`: `CartaoNaoAutorizado`, `FaturaVencida` ou `LimiteInsuficiente`; `em`): só
  para o emissor; nulo para os demais.
- A identidade do pagador nunca é devolvida.

**Erros**: 400 (`cobrancaId` não é UUID), 404 `cobranca-nao-encontrada`.

### `GET /api/v1/cobrancas/minhas?status=&pagina=&tamanhoPagina=`

Cobranças do emissor, das mais novas para as mais antigas, cada item com o corpo da consulta.
**Erros**: 400 (`status`, `pagina`, `tamanhoPagina`).

### `POST /api/v1/cobrancas/{cobrancaId}/cancelar`

**200** — corpo da consulta. **Erros**: 400, 404 `cobranca-nao-encontrada` (inexistente ou de outra
PJ), 409 `cobranca-nao-pendente`.

### `POST /api/v1/cobrancas/{cobrancaId}/pagar`

`Idempotency-Key` obrigatório.

```json
{
  "nomeTitularCartao": "LUCAS H P FRENHAN",
  "numeroCartao": "4000001234567899",
  "validade": "11/31",
  "cvv": "123"
}
```

**200** (também na repetição com a mesma chave)

```json
{
  "cobrancaId": "019a8f77-1111-7890-abcd-ef1234567890",
  "status": "Paga",
  "valor": 299.90,
  "bandeira": { "codigo": "001", "nome": "Visa" },
  "ultimosDigitosCartao": "7899",
  "codigoAutorizacao": "AUT84K",
  "lojistaNome": "Cafeteria Grão Especial LTDA",
  "pagaEm": "2026-11-24T16:35:00Z"
}
```

Ordem das verificações: permissão; 400 (chave e campos); repetição da chave; 404; 409; 422.

| Status | `type` | Quando |
| --- | --- | --- |
| 400 | `validacao` | chave, `cobrancaId`, titular, número (Luhn), validade (formato ou vencida) ou CVV |
| 404 | `cobranca-nao-encontrada` | |
| 409 | `cobranca-nao-pendente` | já paga ou cancelada |
| 409 | `idempotencia-divergente` | mesma chave, outra cobrança ou outros dados de cartão |
| 422 | `cartao-nao-autorizado` | dados que não são do cartão do pagador; não diz qual |
| 422 | `cartao-bloqueado-fatura-vencida` | há fatura vencida não paga |
| 422 | `limite-cartao-insuficiente` | valor maior que o disponível |

A repetição da chave exige a mesma cobrança e os dados válidos do mesmo cartão. Uma recusa (422) não
consome a chave. As respostas nunca repetem número ou CVV.

## Faturas

`competencia` é `AAAA-MM`, o mês do fechamento e do vencimento.

### `GET /api/v1/faturas?pagina=&tamanhoPagina=`

Lista da mais nova para a mais antiga, sem lançamentos; inclui a do ciclo atual, mesmo sem compras. PF
sem cartão: 404 `cartao-nao-encontrado`.

### `GET /api/v1/faturas/{competencia}`

```json
{
  "competencia": "2026-12",
  "inicio": "2026-11-08",
  "fechamento": "2026-12-07",
  "vencimento": "2026-12-10",
  "situacao": "Disponivel",
  "total": 299.90,
  "diasAtraso": 0,
  "juros": 0.00,
  "valorAPagar": 299.90,
  "pagaEm": null,
  "valorPago": null,
  "lancamentos": [
    {
      "estabelecimento": "Cafeteria Grão Especial LTDA",
      "descricao": "Assinatura Mensal Café Especial - Kit 3 Pacotes",
      "valor": 299.90,
      "dataCompra": "2026-11-24"
    }
  ]
}
```

- `situacao`: `Aberta`, `Fechada`, `Disponivel`, `Vencida`, `Paga`.
- `valorAPagar`: total + juros de hoje em `Disponivel`/`Vencida`; nulo nas demais. Em `Paga`,
  `diasAtraso`, `juros` e `valorPago` são os do pagamento.
- Mês sem compras, fora o ciclo atual, não aparece na lista e responde 404 `fatura-nao-encontrada`.
- Lançamentos por data da compra, do mais novo para o mais antigo.

**Erros**: 400 (`competencia`), 404 `cartao-nao-encontrado` ou `fatura-nao-encontrada`.

### `POST /api/v1/faturas/{competencia}/pagar`

`Idempotency-Key` obrigatório; sem corpo. **200** — corpo da fatura paga (também na repetição).
**Erros**: 400; 404 `cartao-nao-encontrado`, `fatura-nao-encontrada`; 409 `fatura-nao-disponivel`
(aberta, fechada ou já paga), `idempotencia-divergente`; 422 `saldo-insuficiente`.

## Recebíveis

### `GET /api/v1/recebiveis?situacao=&pagina=&tamanhoPagina=` e `GET /api/v1/recebiveis/{recebivelId}`

```json
{
  "recebivelId": "019a8f77-9999-7890-abcd-ef1234567890",
  "cobrancaId": "019a8f77-1111-7890-abcd-ef1234567890",
  "descricao": "Assinatura Mensal Café Especial - Kit 3 Pacotes",
  "situacao": "Pendente",
  "valorBruto": 299.90,
  "taxa": 0.00,
  "valorLiquido": 299.90,
  "dataPrevista": "2027-01-07",
  "recebidoEm": null,
  "criadoEm": "2026-11-24T16:35:00Z"
}
```

`situacao`: `Pendente`, `Liquidado`, `Antecipado`. Lista dos mais novos para os mais antigos.
**Erros**: 400, 404 `recebivel-nao-encontrado` (inexistente ou de outra PJ).

### `POST /api/v1/recebiveis/{recebivelId}/antecipar`

`Idempotency-Key` obrigatório; sem corpo. **200** — recebível `Antecipado` com `taxa`,
`valorLiquido` e `recebidoEm` (também na repetição). **Erros**: 400; 404; 409
`recebivel-nao-antecipavel` (já liquidado, já antecipado ou data prevista alcançada),
`idempotencia-divergente`.

## Exemplos de conferência

| Caso | Resultado |
| --- | --- |
| Antecipar 299,90 | taxa 6,00 (5,998); líquido 293,90 |
| Antecipar 0,01 | taxa 0,00; líquido 0,01 |
| Antecipar 100,00 | taxa 2,00; líquido 98,00 |
| Fatura de 1.000,00 com 10 dias de atraso | juros 50,00; valor a pagar 1.050,00 |
| Fatura de 299,90 com 1 dia de atraso | juros 1,50 (1,4995); valor a pagar 301,40 |

## Histórico

- **1.0.0** (2026-11-24): versão inicial (issue #22).
