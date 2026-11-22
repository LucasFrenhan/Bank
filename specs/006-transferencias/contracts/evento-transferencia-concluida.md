# Contrato de evento: `transferencia.concluida` — schema v1

**Feature**: [../spec.md](../spec.md) | **Princípio V** (eventos no passado, com versão de schema)

Gravado em `outbox_eventos` na mesma transação da transferência (FR-017). A publicação no
RabbitMQ (exchange, routing key, cabeçalhos) é definida na spec da mensageria (TASK-09); este
contrato fixa o **conteúdo**.

| Campo do registro | Valor |
| --- | --- |
| `tipo` | `transferencia.concluida` |
| `versao` | `1` |
| `id` | igual a `eventoId` |
| `ocorrido_em` | igual a `ocorridoEm` |

**Payload** (`jsonb`, camelCase):

```json
{
  "eventoId": "019a8f55-3333-7890-abcd-ef1234567890",
  "transferenciaId": "019a8f55-2222-7890-abcd-ef1234567890",
  "pagadorUsuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "recebedorUsuarioId": "019a8f99-8888-7890-abcd-ef1234567890",
  "valor": 150.50,
  "ocorridoEm": "2026-11-17T18:30:00+00:00"
}
```

| Campo | Tipo | Regra |
| --- | --- | --- |
| `eventoId` | uuid | UUIDv7, único |
| `transferenciaId` | uuid | Transferência concluída |
| `pagadorUsuarioId` / `recebedorUsuarioId` | uuid | Titulares (sem documento, nome ou e-mail) |
| `valor` | número | BRL, 2 casas |
| `ocorridoEm` | string (date-time) | Igual a `criadoEm` da transferência, UTC |

Compatibilidade: novos campos opcionais mantêm a versão 1; remoção ou mudança de significado
exige `versao` 2.

## Histórico

- **v1, revisão 2026-11-21**: campos `tipoEvento`, `versao`, `pagadorNome`, `recebedorNome` e
  `descricao` (aditivos) e transporte RabbitMQ — ver
  [specs/010-mensageria-rabbitmq](../../010-mensageria-rabbitmq/contracts/evento-transferencia-concluida.md),
  que passa a ser a referência deste contrato.
- **v1** (2026-11-17): versão inicial (issues #8 e #13).
