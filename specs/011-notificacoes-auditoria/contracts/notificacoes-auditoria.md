# Contrato: notificações de auditoria (`notificacoes_auditoria`) — 1.0.0

**Spec**: [011-notificacoes-auditoria](../spec.md) | **Modelo**: [data-model.md](../data-model.md)

Esta spec não muda contratos de mensageria nem HTTP: o evento `transferencia.concluida` continua
na v1 ([contrato da 010](../../010-mensageria-rabbitmq/contracts/evento-transferencia-concluida.md)),
e a topologia continua na 1.0.0
([topologia](../../010-mensageria-rabbitmq/contracts/topologia-e-mensagens.md)). O que passa a
ser estável, e é consultado direto no banco pela equipe (auditoria e atendimento), é o registro
das notificações descrito aqui. Mudanças nos valores de `tipo`/`canal` ou no texto MUST ser
compatíveis ou gerar nova versão deste documento.

## Origem

| Evento consumido | Consumidor (`eventos_processados.nome_consumidor`) | Linhas por evento |
| --- | --- | --- |
| `transferencia.concluida` v1 | `notificacoes-transferencias` | 2 |

## Mapeamento evento → linhas

| Coluna | `comprovante_envio` | `aviso_credito` |
| --- | --- | --- |
| `evento_id` | `eventoId` | `eventoId` |
| `usuario_destino_id` | `pagadorUsuarioId` | `recebedorUsuarioId` |
| `tipo` | `comprovante_envio` | `aviso_credito` |
| `canal` | `simulado` | `simulado` |
| `mensagem` | ver abaixo | ver abaixo |
| `criado_em` | instante UTC do processamento | instante UTC do processamento |

## Textos (`mensagem`)

| `tipo` | Nome da outra parte presente | Nome ausente ou em branco |
| --- | --- | --- |
| `comprovante_envio` | `Você enviou R$ {valor} para {recebedorNome}.` | `Você enviou R$ {valor}.` |
| `aviso_credito` | `Você recebeu R$ {valor} de {pagadorNome}.` | `Você recebeu R$ {valor}.` |

- `{valor}`: `valor` do evento com 2 casas, separador de milhar `.` e decimal `,`
  (`0,01`, `150,00`, `1.234,56`).
- Nomes: `pagadorNome`/`recebedorNome` do evento, sem espaços nas extremidades.
- Nunca entram: `descricao`, documentos, dados de cartão, identificadores.

## Exemplo

Evento `transferencia.concluida` com `valor` = `1234.5`, `pagadorNome` = `"João Silva"`,
`recebedorNome` = `"Maria Souza"`:

| `tipo` | `usuario_destino_id` | `mensagem` |
| --- | --- | --- |
| `comprovante_envio` | `pagadorUsuarioId` | `Você enviou R$ 1.234,50 para Maria Souza.` |
| `aviso_credito` | `recebedorUsuarioId` | `Você recebeu R$ 1.234,50 de João Silva.` |

Desde a [spec 014](../../014-cartao-credito-cobrancas/contracts/evento-cobranca-paga.md), `cobranca.paga`
grava `comprovante_cobranca` (pagador) e `cobranca_paga` (lojista), com as mesmas garantias.

## Garantias

- Exatamente 2 linhas por evento processado com sucesso, gravadas na mesma transação de
  `eventos_processados`; nenhuma linha em falha (rollback).
- No máximo 1 linha por `(evento_id, tipo)` (índice único).
- Linhas imutáveis; `usuario_destino_id` sempre referencia um usuário existente (`RESTRICT`).
- Eventos processados antes desta spec não têm linhas (sem carga retroativa).
