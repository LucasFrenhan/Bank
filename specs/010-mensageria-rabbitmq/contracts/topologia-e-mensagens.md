# Contrato: topologia RabbitMQ, mensagens, novas tentativas e DLQ

**Feature**: [../spec.md](../spec.md) | **Research**: R3–R7, R10 | **Versão do contrato**: 1.0.0

Vhost do ambiente: `RABBITMQ_VHOST` (padrão `bank`). Tudo é declarado pelo `bank-worker`, a cada
canal aberto, de forma idempotente (FR-007). Sistemas externos que quiserem os eventos ligam a
**própria** fila à `banco.eventos.topic`; as filas abaixo são do Bank.

## Exchanges

| Nome | Tipo | Durável | Uso |
| --- | --- | --- | --- |
| `banco.eventos.topic` | topic | sim | Todos os eventos de domínio concluídos |
| `banco.eventos.dlx` | topic | sim | DLQs (republicação pelo consumidor e dead letter das filas principais) |
| `""` (padrão) | direct | — | Entrega nas filas de espera e retorno delas à fila principal |

## Filas (clássicas, duráveis)

| Fila | Binding | Argumentos |
| --- | --- | --- |
| `notificacoes.transferencias.fila` | `banco.eventos.topic` / `transferencia.concluida` | `x-dead-letter-exchange=banco.eventos.dlx`, `x-dead-letter-routing-key=transferencia.concluida.dlq` |
| `notificacoes.transferencias.espera.1000` | — | `x-message-ttl=1000`, `x-dead-letter-exchange=""`, `x-dead-letter-routing-key=notificacoes.transferencias.fila` |
| `notificacoes.transferencias.espera.2000` | — | idem, TTL 2000 |
| `notificacoes.transferencias.espera.4000` | — | idem, TTL 4000 |
| `notificacoes.transferencias.dlq` | `banco.eventos.dlx` / `transferencia.concluida.dlq` | — |
| `notificacoes.cobrancas.fila` | `banco.eventos.topic` / `cobranca.paga` | `x-dead-letter-exchange=banco.eventos.dlx`, `x-dead-letter-routing-key=cobranca.paga.dlq` |
| `notificacoes.cobrancas.dlq` | `banco.eventos.dlx` / `cobranca.paga.dlq` | — |

Os intervalos vêm de `Mensageria:IntervalosNovaTentativaMs`; o sufixo da fila de espera é o
intervalo em ms. As filas de espera de cobranças nascem com o consumidor de cobranças (Fase 4).

## Publicação

| Item | Valor |
| --- | --- |
| Exchange / routing key | `banco.eventos.topic` / `tipo` do evento (`transferencia.concluida`) |
| `mandatory` | `true` (sem rota = falha de publicação; o evento fica pendente) |
| Confirmação | publisher confirm obrigatório, timeout 5 s; sem confirmação o evento fica pendente |
| Ordem | ordem de gravação (`ocorrido_em`, `id`); o lote para na primeira falha |
| Garantia | pelo menos uma vez |

## Propriedades e headers da mensagem

| Propriedade / header | Valor |
| --- | --- |
| `content-type` | `application/json` |
| `content-encoding` | `utf-8` |
| `delivery-mode` | 2 (persistente) |
| `message-id` | `eventoId` |
| `type` | tipo do evento, ex. `transferencia.concluida` |
| `timestamp` | `ocorridoEm` (segundos Unix) |
| `versao` (header, int) | versão do schema, ex. `1` |
| `traceparent` / `tracestate` (header, string) | W3C Trace Context do span Producer; ausentes se não houver trace |

Corpo: JSON camelCase UTF-8 do contrato do evento
([transferencia.concluida](evento-transferencia-concluida.md), [cobranca.paga](evento-cobranca-paga.md)).

## Consumo

| Situação | Resultado | Métrica `status` |
| --- | --- | --- |
| Processada | Efeito + `eventos_processados` na mesma transação; ACK após o commit | `sucesso` |
| `(eventoId, consumidor)` já em `eventos_processados` | ACK sem efeito | `duplicado` |
| Corpo inválido, `type` ou `versao` desconhecidos, campo obrigatório ausente | DLQ, `x-motivo=mensagem-invalida`, sem nova tentativa | `falha_dlq` |
| Exceção na execução 1, 2 ou 3 | Cópia na espera de 1 s, 2 s ou 4 s; ACK após a confirmação; volta sozinha | `nova_tentativa` |
| Exceção na execução 4 | DLQ, `x-motivo=tentativas-esgotadas` | `falha_dlq` |
| Republicação (espera ou DLQ) sem confirmação | `nack` sem requeue → DLQ pela dead letter da fila, só com `x-death` | `falha_dlq` |

Prefetch 10; uma mensagem por vez por Worker. Consumidor `notificacoes-transferencias` (valor de
`nome_consumidor`).

### Headers de controle

| Header | Tipo | Onde | Regra |
| --- | --- | --- | --- |
| `x-tentativas` | int | espera e DLQ | Execuções já feitas (1–3 na espera; 1 ou 4 na DLQ) |
| `x-reprocessamentos` | int | espera e DLQ | Quantas vezes a mensagem voltou da DLQ; ausente = 0 |
| `x-motivo` | string | só DLQ | `mensagem-invalida` ou `tentativas-esgotadas` |
| `x-ultimo-erro` | string | só DLQ | `"<TipoDaExcecao>: <mensagem>"`, até 500 caracteres, sem payload |
| `x-falhou-em` | string | só DLQ | ISO 8601 UTC |

**Reprocessamento manual**: mover as mensagens da DLQ para `notificacoes.transferencias.fila` pela
interface de gestão (Queues → DLQ → *Move messages*). A entrega com `x-falhou-em` recomeça a
contagem e incrementa `x-reprocessamentos`; ao ir para a espera, `x-falhou-em`, `x-motivo` e
`x-ultimo-erro` são removidos. `message-id`, `type`, `timestamp`, `versao` e trace nunca mudam,
então um evento já processado é descartado como duplicado.

## Telemetria

| Sinal | Nome | Atributos / tags |
| --- | --- | --- |
| Span Producer | `banco.eventos.topic publish` | `messaging.system=rabbitmq`, `messaging.operation.type=send`, `messaging.destination.name=banco.eventos.topic`, `messaging.rabbitmq.destination.routing_key`, `messaging.message.id` |
| Span Consumer | `notificacoes.transferencias.fila process` | `messaging.system`, `messaging.operation.type=process`, `messaging.destination.name`, `messaging.message.id`, `bank.mensageria.tentativa`, `bank.mensageria.resultado` |
| Contador | `banco.eventos.processados` → `banco_eventos_processados_total` | `tipo_evento`, `status`, `motivo` (só `falha_dlq`) |
| Contador | `banco.eventos.publicados` | `tipo_evento` |
| Contador | `banco.eventos.publicacao.falhas` | `tipo_evento`, `motivo` (`sem-rota`, `nack`, `timeout`, `canal`) |
| Gauge | `banco.outbox.pendentes` | — |

Nenhum sinal carrega nome, descrição, payload, documento, credencial ou identificador de usuário
em tag de métrica (FR-023).

## Compatibilidade

Mudar nome de exchange, fila, routing key ou argumento de fila é **quebra** (as declarações
divergem com `PRECONDITION_FAILED`): exige versão nova deste contrato e plano de migração.
Acrescentar filas, bindings ou headers é compatível (MINOR).
