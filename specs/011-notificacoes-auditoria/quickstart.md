# Quickstart: Notificações de transferência persistidas (auditoria)

**Feature**: [spec.md](spec.md) | **Contrato**: [notificacoes-auditoria.md](contracts/notificacoes-auditoria.md) |
**Modelo**: [data-model.md](data-model.md)

## Pré-requisitos

- Docker (em execução: os testes de integração usam Testcontainers) e SDK do .NET 10.
- Ambiente das specs 001–010 (`docker compose up -d --build --wait`); a API aplica a migration
  `AdicionarNotificacoesAuditoria` na partida.
- Banco: `docker compose exec postgres psql -U bank -d bank` (credenciais do `.env`).
- RabbitMQ em <http://localhost:15672> (como no [quickstart da 010](../010-mensageria-rabbitmq/quickstart.md)).

## Cenário 1 — Testes automatizados

```bash
cd src/backend
dotnet build
dotnet test
```

**Esperado**: unitários das fábricas da notificação (texto com e sem nome, formato `1.234,56`), do
caso de uso (duas notificações; mesmo `eventoId` duas vezes → um par) e do leitor (nomes
opcionais); integração cobrindo US1–US3 ([research R8](research.md)); arquitetura sem violações
(identificador tipado da nova entidade).

## Cenário 2 — Transferência gera duas notificações (US1)

1. Cadastre e entre com uma PF **A** e uma PJ **B**; transfira R$ 1.234,50 de A para B.
2. Em até 5 s:

   ```sql
   SELECT tipo, usuario_destino_id, canal, mensagem, criado_em
   FROM notificacoes_auditoria ORDER BY criado_em DESC, tipo LIMIT 2;
   ```

   **Esperado**: `comprovante_envio` para A (`Você enviou R$ 1.234,50 para <nome de B>.`) e
   `aviso_credito` para B (`Você recebeu R$ 1.234,50 de <nome de A>.`), canal `simulado`, mesmo
   `evento_id` de `eventos_processados`.
3. `docker compose logs worker`: as duas linhas de log de notificação continuam sem nomes e sem o
   texto.

## Cenário 3 — Duplicata não gera linhas (US2)

Pela interface de gestão, republique em `banco.eventos.topic` (routing key
`transferencia.concluida`, `message_id` = `eventoId`, `type=transferencia.concluida`, header
`versao=1`) o `payload` do evento do cenário 2 (copiado de `outbox_eventos`).

**Esperado**: `SELECT count(*) FROM notificacoes_auditoria WHERE evento_id = '<eventoId>';` → 2;
métrica `banco.eventos.processados` com `status=duplicado`.

## Cenário 4 — Falha, DLQ e reprocessamento (US3)

1. `docker compose stop postgres`; publique um payload válido com `eventoId` novo (usuários
   existentes) → a mensagem termina na DLQ com `x-motivo=tentativas-esgotadas`.
2. `docker compose start postgres`; `SELECT count(*) ... WHERE evento_id = '<novo>'` → 0.
3. Na DLQ, **Move messages** para `notificacoes.transferencias.fila` → a contagem passa a 2.

## Cenário 5 — Integridade (FR-006, FR-008)

```sql
-- duplicata do mesmo tipo: rejeitada pelo índice único
INSERT INTO notificacoes_auditoria (id, evento_id, usuario_destino_id, tipo, canal, mensagem, criado_em)
SELECT gen_random_uuid(), evento_id, usuario_destino_id, tipo, canal, mensagem, now()
FROM notificacoes_auditoria LIMIT 1;
```

**Esperado**: falha em `ux_notificacoes_auditoria_evento_tipo`. O `RESTRICT` da chave estrangeira
é verificado no teste de integração (no ambiente, a carteira do usuário já impede a exclusão antes).
