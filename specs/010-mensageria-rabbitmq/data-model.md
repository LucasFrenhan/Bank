# Data Model: Mensageria RabbitMQ

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Migration única: `AdicionarMensageria` (Infrastructure, aplicada pela API na partida — R9).

## `outbox_eventos` (existente, spec 006) — alterada

| Coluna | Tipo | Nulo | Mudança | Regra |
| --- | --- | --- | --- | --- |
| `id` | uuid | não | — | = `eventoId` |
| `tipo` | varchar(100) | não | — | `transferencia.concluida` |
| `versao` | integer | não | — | `1` |
| `payload` | jsonb | não | conteúdo | Payload v1 agora com `tipoEvento`, `versao`, `pagadorNome`, `recebedorNome`, `descricao` ([contrato](contracts/evento-transferencia-concluida.md)) |
| `ocorrido_em` | timestamptz | não | — | Ordem de publicação |
| `publicado_em` | timestamptz | sim | passa a ser preenchida | Instante da confirmação do broker (FR-002). Única coluna atualizada depois da gravação |
| `trace_parent` | varchar(55) | sim | **nova** | `traceparent` W3C ativo na gravação (FR-005); nulo sem trace ativo e nos eventos antigos |
| `trace_state` | varchar(512) | sim | **nova** | `tracestate` W3C, quando houver |

Índice existente `ix_outbox_eventos_pendentes (ocorrido_em) WHERE publicado_em IS NULL` atende a
leitura do despachante (R4) e a contagem de pendentes (R10).

**Ciclo de vida**: `pendente` (`publicado_em` nulo) → `publicado` (preenchido após a confirmação).
Não há expurgo nesta spec. Um evento pode ser publicado mais de uma vez (queda entre confirmação e
commit); a duplicata é absorvida pelo consumidor.

## `eventos_processados` — nova

| Coluna | Tipo | Nulo | Regra |
| --- | --- | --- | --- |
| `evento_id` | uuid | não | `eventoId` da mensagem |
| `nome_consumidor` | varchar(100) | não | Nome estável do consumidor, ex.: `notificacoes-transferencias` |
| `processado_em` | timestamptz | não | Instante UTC do commit do processamento |

- **Chave primária**: `pk_eventos_processados (evento_id, nome_consumidor)` (FR-019). O mesmo evento
  pode ser processado uma vez por consumidor diferente (notificações, futuramente auditoria).
- **Escrita**: `INSERT ... ON CONFLICT DO NOTHING`, na mesma transação do efeito (R7). Linha
  presente = já processado.
- **Imutável**: sem update nem delete; sem expurgo nesta spec.
- Entidade `EventoProcessado` (Infrastructure/Mensageria), mapeada com `EventoId` e conversor de
  identificador (`IdentificadorConverter`).

## Domain — `TransferenciaConcluida` (alterado)

```text
TransferenciaConcluida(
    EventoId EventoId,
    TransferenciaId TransferenciaId,
    UsuarioId PagadorUsuarioId,
    UsuarioId RecebedorUsuarioId,
    decimal Valor,
    DateTimeOffset OcorridoEm,
    string PagadorNome,          // novo — NomeTitular da carteira de origem
    string RecebedorNome,        // novo — nome/razão social do destinatário
    string? Descricao)           // novo — Transferencia.Descricao
Tipo = "transferencia.concluida"; Versao = 1
```

`Transferencia.Executar` recebe `pagadorNome` e `recebedorNome` (lidos antes pelo caso de uso) e
monta o evento.

## Application — notificação (novo, não persistido)

```text
NotificacaoTransferencia(
    EventoId EventoId, TransferenciaId TransferenciaId,
    UsuarioId PagadorUsuarioId, UsuarioId RecebedorUsuarioId,
    decimal Valor, DateTimeOffset OcorridoEm)
```

Entrada do caso de uso `NotificarTransferenciaConcluida`. Nomes e descrição chegam no evento, mas
não entram no comando: a notificação simulada é um log, e logs não levam nome nem descrição
(FR-023). O envio real (issue futura) acrescentará os campos de que precisar.

## Mensagem em trânsito (não persistida)

Corpo = `payload` do outbox; propriedades e headers em
[contracts/topologia-e-mensagens.md](contracts/topologia-e-mensagens.md). Estados de uma entrega:

```text
fila principal ──sucesso──────────────────────────────▶ ACK (eventos_processados gravado)
      │ ──duplicado───────────────────────────────────▶ ACK
      │ ──inválida────────────────────────────────────▶ DLQ (mensagem-invalida)
      │ ──falha, execução 1/2/3──▶ espera 1 s/2 s/4 s ─▶ fila principal (TTL)
      │ ──falha, execução 4───────────────────────────▶ DLQ (tentativas-esgotadas)
DLQ ──movida à mão (x-falhou-em presente)─────────────▶ fila principal, contagem do zero
```
