# Data Model: Motor de transferências e extrato

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-11-17

## Domínio

### Transferencia (imutável)

| Atributo | Tipo | Regra |
| --- | --- | --- |
| `Id` | `Guid` (UUIDv7) | Gerado no domínio |
| `ChaveIdempotencia` | `Guid` | Do cabeçalho `Idempotency-Key`; única por carteira de origem |
| `CarteiraOrigemId` | `Guid` | Carteira PF do pagador |
| `CarteiraDestinoId` | `Guid` | Diferente da origem |
| `Valor` | `ValorTransferencia` | ≥ 0,01; ≤ 2 casas; ≤ 9.999.999.999.999.999,99 |
| `Descricao` | `string?` | `Trim`; vazia → nula; ≤ 200 |
| `Status` | `StatusTransferencia` | `Concluida` (único valor) |
| `CriadoEm` | `DateTimeOffset` UTC | |

### Lancamento (imutável)

| Atributo | Tipo | Regra |
| --- | --- | --- |
| `Id` | `Guid` (UUIDv7) | |
| `CarteiraId` | `Guid` | Carteira do razão |
| `Tipo` | `TipoLancamento` | `Abertura` \| `Transferencia` |
| `Direcao` | `DirecaoLancamento` | `Entrada` \| `Saida` |
| `Valor` | `decimal` | > 0, 2 casas |
| `TransferenciaId` | `Guid?` | Obrigatório se `Tipo = Transferencia`; nulo na abertura |
| `Descricao` | `string?` | Abertura: "Saldo inicial de demonstração"; transferência: a da transferência |
| `CriadoEm` | `DateTimeOffset` UTC | Igual ao da transferência |

Cada transferência gera dois lançamentos: `Saida` na origem e `Entrada` no destino.

### Carteira (spec 003, ampliada)

- `Debitar(valor, agora)`: falha com `saldo-insuficiente` se `Saldo < valor`; senão subtrai e
  atualiza `AtualizadoEm`.
- `Creditar(valor, agora)`: soma e atualiza `AtualizadoEm`.
- Invariante: `Saldo = Σ Entradas − Σ Saídas` dos seus lançamentos (FR-015).

### Evento `TransferenciaConcluida` (v1)

`EventoId`, `TransferenciaId`, `PagadorUsuarioId`, `RecebedorUsuarioId`, `Valor`, `OcorridoEm` —
schema em [contracts/evento-transferencia-concluida.md](contracts/evento-transferencia-concluida.md).

### Erros novos (`ErrosTransferencia`)

| Código | `TipoErro` | HTTP |
| --- | --- | --- |
| `destinatario-nao-encontrado` | `NaoEncontrado` | 404 |
| `autotransferencia` | `RegraDeNegocio` (novo) | 422 |
| `saldo-insuficiente` | `RegraDeNegocio` | 422 |
| `idempotencia-divergente` | `Conflito` | 409 |

## Persistência (PostgreSQL) — migration `CriarTransferenciasLancamentosEOutbox`

### `transferencias`

| Coluna | Tipo | Restrições |
| --- | --- | --- |
| `id` | `uuid` | PK `pk_transferencias` |
| `chave_idempotencia` | `uuid` | `NOT NULL` |
| `carteira_origem_id` | `uuid` | `NOT NULL`, FK `fk_transferencias_carteira_origem` → `carteiras(id)` `RESTRICT` |
| `carteira_destino_id` | `uuid` | `NOT NULL`, FK `fk_transferencias_carteira_destino` → `carteiras(id)` `RESTRICT` |
| `valor` | `numeric(18,2)` | `NOT NULL`, `CHECK (valor > 0)` `ck_transferencias_valor_positivo` |
| `descricao` | `varchar(200)` | nulo |
| `status` | `varchar(20)` | `NOT NULL`, `CHECK (status = 'Concluida')` |
| `criado_em` | `timestamptz` | `NOT NULL` |

Índices: `UNIQUE (carteira_origem_id, chave_idempotencia)` `ux_transferencias_origem_chave`;
`CHECK (carteira_origem_id <> carteira_destino_id)` `ck_transferencias_origem_diferente_destino`.

### `lancamentos`

| Coluna | Tipo | Restrições |
| --- | --- | --- |
| `id` | `uuid` | PK `pk_lancamentos` |
| `carteira_id` | `uuid` | `NOT NULL`, FK → `carteiras(id)` `RESTRICT` |
| `tipo` | `varchar(20)` | `CHECK (tipo IN ('Abertura','Transferencia'))` |
| `direcao` | `varchar(10)` | `CHECK (direcao IN ('Entrada','Saida'))` |
| `valor` | `numeric(18,2)` | `CHECK (valor > 0)` |
| `transferencia_id` | `uuid` | nulo, FK → `transferencias(id)` `RESTRICT`; `CHECK ((tipo = 'Transferencia') = (transferencia_id IS NOT NULL))` |
| `descricao` | `varchar(200)` | nulo |
| `criado_em` | `timestamptz` | `NOT NULL` |

Índices: `(carteira_id, criado_em DESC, id DESC)` `ix_lancamentos_extrato`;
único parcial `(carteira_id) WHERE tipo = 'Abertura'` `ux_lancamentos_abertura_por_carteira`.

### `outbox_eventos`

| Coluna | Tipo | Restrições |
| --- | --- | --- |
| `id` | `uuid` | PK (= `eventoId`) |
| `tipo` | `varchar(100)` | `NOT NULL` (`transferencia.concluida`) |
| `versao` | `integer` | `NOT NULL` (1) |
| `payload` | `jsonb` | `NOT NULL` |
| `ocorrido_em` | `timestamptz` | `NOT NULL` |
| `publicado_em` | `timestamptz` | nulo até a publicação (TASK-09) |

Índice parcial `ix_outbox_eventos_pendentes` em `ocorrido_em WHERE publicado_em IS NULL`.

### SQL puro (constituição v1.1.0) — `src/backend/src/Bank.Infrastructure/Persistencia/Sql/`

| Arquivo | Uso |
| --- | --- |
| `Procedures/ImpedirAlteracaoDeRegistrosImutaveis.sql` | Função + triggers `BEFORE UPDATE OR DELETE` em `transferencias` e `lancamentos` (aplicado pela migration) |
| `Consultas/BloquearCarteirasParaTransferencia.sql` | `SELECT ... WHERE id = ANY(@ids) ORDER BY id FOR UPDATE` |
| `Consultas/CriarLancamentosDeAbertura.sql` | `INSERT ... SELECT` das aberturas faltantes (idempotente) |
| `Procedures/RemoverImpedimentoDeAlteracao.sql` | Remove as triggers e a função (usado pelo `Down` da migration) |

## Configuração

| Chave | Padrão | Regra |
| --- | --- | --- |
| `Persistencia:CriarLancamentosDeAberturaNaInicializacao` | `false`; `true` em `Development` e `Testing` | Nunca ligado em `Production` (FR-016) |
