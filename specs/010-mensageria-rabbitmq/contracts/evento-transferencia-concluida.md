# Contrato de evento: `transferencia.concluida` — schema v1 (revisão 2026-11-21)

**Feature**: [../spec.md](../spec.md) (FR-011) | Contrato original: [specs/006-transferencias](../../006-transferencias/contracts/evento-transferencia-concluida.md)
| Transporte: [topologia-e-mensagens.md](topologia-e-mensagens.md)

Gravado em `outbox_eventos` na mesma transação da transferência e publicado em
`banco.eventos.topic` com routing key `transferencia.concluida`. Esta revisão **acrescenta campos**
à versão 1 (compatível): consumidores da v1 original continuam funcionando.

**Payload** (JSON camelCase, UTF-8):

```json
{
  "eventoId": "019a8f55-3333-7890-abcd-ef1234567890",
  "tipoEvento": "TransferenciaConcluida",
  "versao": 1,
  "transferenciaId": "019a8f55-2222-7890-abcd-ef1234567890",
  "pagadorUsuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "pagadorNome": "Lucas Silva",
  "recebedorUsuarioId": "019a8f99-8888-7890-abcd-ef1234567890",
  "recebedorNome": "Cafeteria Grão Especial LTDA",
  "valor": 150.50,
  "descricao": "Pagamento almoço",
  "ocorridoEm": "2026-11-17T15:00:00+00:00"
}
```

| Campo | Tipo | Obrigatório na leitura | Regra |
| --- | --- | --- | --- |
| `eventoId` | uuid | sim | UUIDv7, único; = `message-id` |
| `tipoEvento` | string | não (novo) | `"TransferenciaConcluida"` |
| `versao` | inteiro | não (novo; o header `versao` é a fonte) | `1` |
| `transferenciaId` | uuid | sim | Transferência concluída |
| `pagadorUsuarioId` | uuid | sim | Titular que enviou |
| `pagadorNome` | string | não (novo) | Nome do titular pagador na data da transferência, até 150 caracteres |
| `recebedorUsuarioId` | uuid | sim | Titular que recebeu |
| `recebedorNome` | string | não (novo) | Nome ou razão social do recebedor, até 150 caracteres |
| `valor` | número | sim | BRL, 2 casas, > 0 |
| `descricao` | string \| null | não (novo) | Descrição opcional da transferência; `null` quando ausente |
| `ocorridoEm` | string (date-time) | sim | = `criadoEm` da transferência, UTC |

Sem documento (CPF/CNPJ) nem e-mail. Os nomes e a descrição podem trafegar no evento, mas **não**
vão para logs, spans nem métricas (spec FR-023).

**Leitura**: campos desconhecidos são ignorados; ausência de um campo "não" obrigatório não é erro
(eventos gravados antes desta revisão só têm os campos da v1 original). Falta de campo
obrigatório, `type` diferente ou `versao` ≠ 1 → DLQ `mensagem-invalida`.

**Compatibilidade**: novos campos opcionais mantêm a versão 1; remoção ou mudança de significado
exige `versao` 2 (com nova routing key ou convivência das duas versões documentada).

## Histórico

- **v1, revisão 2026-11-21** (spec 010, issue #20): `tipoEvento`, `versao`, `pagadorNome`,
  `recebedorNome`, `descricao`; transporte RabbitMQ.
- **v1** (2026-11-17, spec 006): versão inicial (issues #8 e #13).
