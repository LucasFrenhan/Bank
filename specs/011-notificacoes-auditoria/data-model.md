# Data Model: Notificações de transferência persistidas (auditoria)

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Migration única: `AdicionarNotificacoesAuditoria` (Infrastructure, aplicada pela API na partida;
spec 010, R9).

## `notificacoes_auditoria` — nova

| Coluna | Tipo | Nulo | Regra |
| --- | --- | --- | --- |
| `id` | uuid | não | `NotificacaoAuditoriaId`, UUIDv7 gerado na criação |
| `evento_id` | uuid | não | `eventoId` de `transferencia.concluida` |
| `usuario_destino_id` | uuid | não | Pagador (comprovante) ou recebedor (aviso de crédito) |
| `tipo` | varchar(30) | não | `comprovante_envio` ou `aviso_credito` |
| `canal` | varchar(30) | não | `simulado` (canais reais entram como valores novos, por migration) |
| `mensagem` | varchar(500) | não | Texto em português ([contrato](contracts/notificacoes-auditoria.md)) |
| `criado_em` | timestamptz | não | Instante UTC do processamento (relógio do Worker) |

- **Chave primária**: `pk_notificacoes_auditoria (id)`.
- **Unicidade**: `ux_notificacoes_auditoria_evento_tipo (evento_id, tipo)` (FR-006,
  Clarification Q1). Serve também à consulta `ExistemParaEventoAsync` (prefixo `evento_id`).
- **Chave estrangeira**: `fk_notificacoes_auditoria_usuarios_usuario_destino_id`
  (`usuario_destino_id` → `usuarios.id`), `ON DELETE RESTRICT` (Clarification Q4), com índice
  `ix_notificacoes_auditoria_usuario_destino_id`.
- **Checks**: `ck_notificacoes_auditoria_tipo` (`tipo IN ('comprovante_envio', 'aviso_credito')`)
  e `ck_notificacoes_auditoria_canal` (`canal IN ('simulado')`).
- **Imutável**: só `INSERT`; sem update nem delete pela aplicação (FR-007); sem expurgo.
- **Volume**: 2 linhas por transferência concluída.

Não há chave estrangeira para `eventos_processados` nem para `outbox_eventos`: `evento_id` é o
identificador de integração, e o outbox pode, no futuro, ser expurgado independentemente.

## Domain — `NotificacaoAuditoria` (novo, `Bank.Domain/Notificacoes`)

```text
NotificacaoAuditoriaId(Guid Valor) : IIdentificador<NotificacaoAuditoriaId>   // De, Novo(agora)

enum TipoNotificacao { ComprovanteEnvio, AvisoCredito }
enum CanalNotificacao { Simulado }

NotificacaoAuditoria
    NotificacaoAuditoriaId Id
    EventoId EventoId
    UsuarioId UsuarioDestinoId
    TipoNotificacao Tipo
    CanalNotificacao Canal
    string Mensagem
    DateTimeOffset CriadoEm

    static ComprovanteEnvio(EventoId, UsuarioId pagador, decimal valor, string? recebedorNome, DateTimeOffset agora)
    static AvisoCredito(EventoId, UsuarioId recebedor, decimal valor, string? pagadorNome, DateTimeOffset agora)
```

Sem setters públicos nem métodos de alteração; construtor privado para o EF Core. Conversão dos
enums para texto `snake_case` na `NotificacaoAuditoriaConfiguracao` (Infrastructure).

## Application — alterações

```text
NotificacaoTransferencia(
    EventoId EventoId, TransferenciaId TransferenciaId,
    UsuarioId PagadorUsuarioId, UsuarioId RecebedorUsuarioId,
    decimal Valor, DateTimeOffset OcorridoEm,
    string? PagadorNome = null,      // novo (R4)
    string? RecebedorNome = null)    // novo (R4)

INotificacoesAuditoria                // novo (R2)
    Task<bool> ExistemParaEventoAsync(EventoId, CancellationToken)
    Task AdicionarAsync(IReadOnlyCollection<NotificacaoAuditoria>, CancellationToken)
```

`NotificarTransferenciaConcluida` passa a depender de `INotificacoesAuditoria` e `TimeProvider`:

```text
se ExistemParaEventoAsync(eventoId) → log Debug, retorna (R3)
cria ComprovanteEnvio(pagador) e AvisoCredito(recebedor) com agora = relógio
AdicionarAsync([comprovante, aviso])
log 5100 por destinatário + métrica bank.notificacoes.enviadas por papel (R6)
```

## Fluxo de uma entrega (inalterado, com o efeito novo)

```text
transação (escopo do consumidor)
├── INSERT eventos_processados ON CONFLICT DO NOTHING
│     └── 0 linhas → duplicado: commit, ACK (nenhuma notificação)
└── 1 linha → NotificarTransferenciaConcluida
      ├── INSERT notificacoes_auditoria (comprovante_envio, aviso_credito)
      └── exceção → rollback de tudo → espera 1 s/2 s/4 s ou DLQ (spec 010)
commit → ACK
```
