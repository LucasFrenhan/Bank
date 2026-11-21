# Quickstart: Mensageria RabbitMQ

**Feature**: [spec.md](spec.md) | **Contratos**: [topologia-e-mensagens.md](contracts/topologia-e-mensagens.md),
[evento-transferencia-concluida.md](contracts/evento-transferencia-concluida.md)

## Pré-requisitos

- Docker; SDK do .NET 10. Ambiente das specs 001–009 (`docker compose up -d --build --wait`).
- Interfaces: RabbitMQ em <http://localhost:15672> (usuário/senha `RABBITMQ_USER`/`RABBITMQ_PASSWORD`,
  padrão `bank`/`bank_dev_password`, vhost `bank`); telemetria no Aspire Dashboard em
  <http://localhost:18888>.

## Cenário 1 — Testes automatizados

```bash
cd src/backend
dotnet build
dotnet test
```

**Esperado**: unitários (evento com nomes e descrição, outbox com trace, política de tentativas,
leitor da mensagem, notificação); contrato (payload v1 e leitura de payload antigo); integração
com PostgreSQL e RabbitMQ em contêiner cobrindo US1–US5 (research R12); arquitetura com o
`Bank.Worker`. Os testes usam intervalos de 100/200/400 ms.

## Cenário 2 — Topologia (FR-006)

Com o ambiente no ar, em **Exchanges** e **Queues** da interface de gestão: `banco.eventos.topic` e
`banco.eventos.dlx`; `notificacoes.transferencias.fila` (argumentos DLX), as 3 filas
`notificacoes.transferencias.espera.*` (TTL e dead letter para a fila principal), as DLQs e as
filas de cobrança, todas *durable*. `docker compose ps` mostra `worker` *healthy*.

## Cenário 3 — Transferência até a notificação (US1, US5)

1. Cadastre e entre com uma PF **A** e uma PJ **B**; transfira R$ 1,00 de A para B.
2. Em até 5 s: `docker compose logs worker` mostra as notificações de pagador e recebedor (com
   `eventoId`, sem nomes); `SELECT * FROM eventos_processados;` tem 1 linha
   (`notificacoes-transferencias`); `SELECT publicado_em, trace_parent FROM outbox_eventos;`
   tem os dois preenchidos.
3. No Aspire Dashboard, o trace do `POST /api/v1/transferencias` tem os spans
   `banco.eventos.topic publish` e `notificacoes.transferencias.fila process` (serviço
   `bank-worker`); a métrica `banco.eventos.processados` tem `status=sucesso`.

## Cenário 4 — Broker fora do ar (US2)

1. `docker compose stop rabbitmq`; faça 3 transferências → todas 201.
2. `SELECT count(*) FROM outbox_eventos WHERE publicado_em IS NULL;` → 3; o Worker registra a
   perda da sessão e tentativas de reabertura.
3. `docker compose start rabbitmq` → em até ~1 min os 3 eventos são publicados e notificados, sem
   reiniciar a API nem o Worker; os traces continuam os das requisições originais.

## Cenário 5 — Duplicata, nova tentativa e DLQ (US3, US4)

Pela interface de gestão, **Publish message** em `banco.eventos.topic`, routing key
`transferencia.concluida`, propriedades `message_id`, `type=transferencia.concluida`,
`content_type=application/json` e header `versao=1`:

1. Republique o payload de um evento já processado (copie de `outbox_eventos.payload`) → ACK sem
   nova notificação; métrica `status=duplicado`.
2. Publique `{"eventoId": "x"}` → a mensagem aparece em `notificacoes.transferencias.dlq` com
   `x-motivo=mensagem-invalida`, `x-tentativas=1`.
3. `docker compose stop postgres`; publique um payload válido com `eventoId` novo → a mensagem
   passa pelas filas de espera (1 s, 2 s, 4 s) e termina na DLQ com
   `x-motivo=tentativas-esgotadas`, `x-tentativas=4`.
4. `docker compose start postgres`; na DLQ, **Move messages** para
   `notificacoes.transferencias.fila` → a notificação é registrada uma vez
   (`x-reprocessamentos=1` no log de diagnóstico).
