# Data Model: Limite de transferência

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md)

Esta spec **não cria tabelas**. Os limites vêm da configuração (R2) e o uso diário é derivado
das transferências já gravadas (R3).

## Mudanças no banco

| Objeto | Mudança | Motivo |
| --- | --- | --- |
| `transferencias` | Índice `ix_transferencias_origem_criado_em (carteira_origem_id, criado_em)` | Soma do dia por pagador sob bloqueio (R3, SC-007) |

Migration: `AdicionarIndiceTransferenciasOrigemCriadoEm`. Sem alteração de dados; a imutabilidade
da tabela (trigger da spec 006) não é afetada.

## Domain

### `TipoLimite` (enum)

| Valor | Significado |
| --- | --- |
| `PorOperacao` | Valor máximo de uma transferência |
| `Diario` | Soma máxima das transferências concluídas enviadas no dia de Brasília |

Novos limites (noturno, por modalidade — #31) acrescentam valores aqui (FR-007).

### `LimitesTransferencia` (objeto de valor)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `PorOperacao` | `decimal?` | `null` = sem limite; se presente, > 0 e até 2 casas |
| `Diario` | `decimal?` | `null` = sem limite; se presente, > 0, até 2 casas e ≥ `PorOperacao` |

Operações:

- `VerificarOperacao(valor)`: recusa se `PorOperacao` existe e `valor > PorOperacao`.
  `valorDisponivel = PorOperacao`.
- `VerificarDiario(valor, transferidoHoje)`: recusa se `Diario` existe e
  `transferidoHoje + valor > Diario`. `valorDisponivel = max(0, Diario − transferidoHoje)`.
- `DisponivelHoje(transferidoHoje)`: `max(0, Diario − transferidoHoje)` ou `null`.

Os limites são inclusivos (FR-004): igualdade é permitida.

### `Erro` (alterado)

Ganha `Detalhes` opcional (`IReadOnlyDictionary<string, object?>`), copiado para as extensões do
`ProblemDetails` (R5). Erros existentes não têm detalhes.

### `ErrosTransferencia.LimiteExcedido(tipo, limite, valorDisponivel)`

| Atributo | Valor |
| --- | --- |
| Código | `limite-excedido` |
| Tipo | `RegraDeNegocio` (422) |
| Mensagem | por tipo: "O valor passa do limite por transferência." / "O valor passa do limite diário de transferências." |
| Detalhes | `tipoLimite`, `limite`, `valorDisponivel` |

## Application

### `LimitesTransferenciaOptions` — seção `Transferencias:Limites`

| Chave | Tipo | Padrão (`appsettings.json`) |
| --- | --- | --- |
| `PessoaFisica:PorOperacao` | `decimal?` | `1000.00` |
| `PessoaFisica:Diario` | `decimal?` | `5000.00` |

Validada na inicialização (R2). Lida por `IOptionsMonitor`. Valor vazio sobrescreve o padrão e
desliga o limite (`null`).

### `CalendarioBrasilia`

| Operação | Resultado |
| --- | --- |
| `Hoje(agoraUtc)` | `DiaBrasilia { Data (DateOnly), InicioUtc, FimUtc }`, com `FimUtc` exclusivo |

Fuso: `America/Sao_Paulo` → `E. South America Standard Time` → falha na inicialização (R4).

### `ITransferenciaRepositorio.SomarEnviadasAsync(carteiraOrigemId, inicioUtc, fimUtc)`

Soma de `valor` das transferências com a origem e `criado_em` no intervalo `[inicio, fim)`;
0 quando não há nenhuma. Chamada dentro da transação, depois do bloqueio, no envio (R3); fora de
transação na consulta (R6).

### `ResumoLimites` (leitura)

| Campo | Tipo | Observação |
| --- | --- | --- |
| `PorOperacao` | `decimal?` | `null` = sem limite |
| `Diario` | `decimal?` | `null` = sem limite |
| `TransferidoHoje` | `decimal` | Concluídas enviadas no dia de Brasília |
| `DisponivelHoje` | `decimal?` | `max(0, Diario − TransferidoHoje)`; `null` se sem limite diário |
| `Dia` | `DateOnly` | Data de Brasília usada no cálculo |

## Fluxo de `RealizarTransferencia` (FR-008)

```text
validação (400)
→ idempotência: replay (200) ou divergente (409)          ← replay não verifica limites
→ destinatário (404) → autotransferência (422)
→ limite por operação (422 limite-excedido/PorOperacao)  ← antes do bloqueio
→ início da transação + bloqueio ordenado das carteiras
→ idempotência de novo (requisições simultâneas)
→ soma do dia de Brasília → limite diário (422 limite-excedido/Diario)
→ saldo (422 saldo-insuficiente) → grava tudo → 201
```

Recusa por limite: desfaz a transação; nenhum registro, lançamento, evento ou chave gravados
(FR-010).
