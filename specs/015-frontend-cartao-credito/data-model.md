# Data Model: Frontend de cartão de crédito, cobranças, faturas e recebíveis

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## Backend

### `CodigoCobranca` (Domain, value object)

8 caracteres de `23456789ABCDEFGHJKMNPQRSTVWXYZ`. `Novo()`, `Criar(string?)` (aceita minúsculas, espaços e
hífen), `Valor` (sem hífen), `ToString()` (`XXXX-XXXX`).

### `Cobranca`

Ganha `Codigo` (imutável), recebido em `Emitir`; até 5 tentativas em colisão, depois exceção.
`ResultadoGravacao` ganha `CodigoDeCobrancaDuplicado`.

### PostgreSQL — migration `AdicionarCodigoCobranca`

| Tabela | Mudança |
| --- | --- |
| `cobrancas` | `codigo char(8) NOT NULL`; `ux_cobrancas_codigo`; `ck_cobrancas_codigo` (`codigo ~ '^[2-9A-HJKMNP-TV-Z]{8}$'`) |

`Sql/Migracoes/PreencherCodigosDasCobrancas.sql` entre a criação da coluna e o `NOT NULL`;
`Sql/Consultas/BloquearCobranca.sql` passa a devolver `codigo`.

### Application e API

`DadosCobranca.Codigo`; `ICobrancaConsultas.ObterPorCodigoAsync`; caso de uso `ConsultarCobrancaPorCodigo`;
`CobrancaResposta.codigo`; rota `GET /cobrancas/codigo/{codigo}`.

## Frontend

### Código e link (`src/cobrancas/codigo.ts`)

| Função | Regra |
| --- | --- |
| `extrairCodigo(texto)` | link (`…/c/{codigo}`) ou código; normaliza; `null` se inválido |
| `formatarCodigo(codigo)` | `XXXX-XXXX` |
| `linkDaCobranca(codigo)` | `{origem}/c/XXXX-XXXX` |
| `textoDeCompartilhamento(cobranca)` | `{lojista}: {valor} — {descrição}. Pague com o cartão One Bank: {link}` |

### Dados do cartão (`src/cartao/`)

- `useDadosCartao`: `oculto` \| `buscando` \| `exibido(nome, numero, validade, cvv)` \| `erro`; regras de R5.
- Formulário de pagamento: `nome`, `numero` (máscara em grupos de 4), `validade` (`MM/AA`), `cvv` (3 dígitos);
  só no estado do componente. Validação: titular preenchido; 16 dígitos e Luhn; formato e não vencida
  (Brasília); 3 dígitos. Após recusa, o CVV é limpo e o foco vai para ele; em `pagehide`, tudo é limpo.
  Em falha de rede o CVV é mantido, para repetir com os mesmos dados. "Usar meu cartão" limpa o CVV no clique.

### Operação (`src/operacoes/`)

`bank.operacoes` em `sessionStorage`: `{ [operacao]: chave }`, com `operacao` = `cobranca:{codigo}` \|
`fatura:{AAAA-MM}` \| `recebivel:{id}`. Apagado por `removerSessao()`. Sem armazenamento, vale em memória.

```text
sem chave ──enviar──▶ com chave ──falha de rede / 422 / 5xx──▶ com chave (mesma)
                          └──200 / divergente / não é mais possível──▶ sem chave
```

### Situações e rótulos

| Origem | Valor → rótulo (cor) |
| --- | --- |
| Cobrança | `Pendente` → Pendente (atenção); `Paga` → Paga (entrada); `Cancelada` → Cancelada (neutro) |
| Fatura | `Aberta` → Aberta (info); `Fechada` → Fechada (neutro); `Disponivel` → Disponível (info); `Vencida` → Vencida (erro); `Paga` → Paga (entrada) |
| Recebível | `Pendente` → Pendente (atenção); `Liquidado` → Liquidado (entrada); `Antecipado` → Antecipado (info) |
| Recusa | `CartaoNaoAutorizado` → Cartão não autorizado; `FaturaVencida` → Fatura vencida; `LimiteInsuficiente` → Limite insuficiente |
| Desconhecido | o próprio texto da API, sem cor |

### Aviso de fatura

A fatura mais antiga `Vencida` ou `Disponivel` da lista; `Vencida`: "Fatura vencida — R$ {valorAPagar} hoje. O
cartão está bloqueado."; `Disponivel`: "Fatura de {mês} — R$ {valorAPagar}, vence em {dia}.". Link para a fatura.
