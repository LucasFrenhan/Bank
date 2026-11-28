# Data Model: Fundação do monólito modular

Esta spec não cria nem altera tabelas, colunas, migrations ou contratos de dados (FR-021). O
"modelo" aqui é estrutural: os projetos, o que cada um contém e os registros usados pelos testes
de arquitetura.

## Elementos estruturais

### Shared Kernel (parte pura) — `Bank.SharedKernel`

| Tipo | Namespace novo | Origem |
| --- | --- | --- |
| `Dinheiro` | `Bank.SharedKernel` | `Bank.Domain.Comum` |
| `TipoErro`, `Erro` | `Bank.SharedKernel` | `Bank.Domain.Comum` |
| `Resultado`, `Resultado<T>` | `Bank.SharedKernel` | `Bank.Domain.Comum` |
| `IIdentificador<TSelf>` | `Bank.SharedKernel` | `Bank.Domain.Comum` |
| `EventoId` | `Bank.SharedKernel` | `Bank.Domain.Comum` |
| `CalendarioBancario` | `Bank.SharedKernel.Calendario` | `Bank.Domain.Calendario` |
| `Paginacao`, `PaginaDe<T>`, `Entradas`, `ErrosIdempotencia` (com a constante `CodigoDivergente`, usada também por `ErrosTransferencia`) | `Bank.SharedKernel.Aplicacao` | `Bank.Application.Comum` |

Regras: depende só da BCL; nenhum tipo de contexto (FR-001, FR-002, MOD-05).

### Shared Kernel (infraestrutura comum) — `Bank.SharedKernel.Infrastructure`

| Tipo | Namespace novo | Origem |
| --- | --- | --- |
| `IdentificadorConverter<TId>` (`internal` → `public`) | `Bank.SharedKernel.Infrastructure.Persistencia` | `Bank.Infrastructure.Persistencia` |

Regras: depende só de `Bank.SharedKernel` e do EF Core; usado só por Infrastructure (FR-002a, MOD-07).

### Permanecem no núcleo antigo

- Identificadores tipados de contexto (`UsuarioId`, `CarteiraId`, `TransferenciaId`, `CartaoId`,
  `CobrancaId`, `SessaoId` etc.) — ficam no contexto dono (clarificação 2026-11-27).
- `IUnidadeDeTrabalho`, `ResultadoGravacao` — `Bank.Application.Abstracoes` (research R2).
- `Bank.Application.Observabilidade` (métricas por contexto) — não é comum.

### Módulo (formato; nenhum real nesta spec)

| Projeto | Namespaces | Visibilidade |
| --- | --- | --- |
| `Bank.Modulos.<Nome>` | `Bank.Modulos.<Nome>` (só `Modulo<Nome>`), `.Domain`, `.Application`, `.Infrastructure`, `.Endpoints` | tudo `internal`, exceto `Modulo<Nome>` |
| `Bank.Modulos.<Nome>.Contratos` | `Bank.Modulos.<Nome>.Contratos` | `public` |

Dependências permitidas: `Bank.Modulos.<Nome>` → `Bank.Modulos.<Nome>.Contratos`, contratos de outros
módulos, `Bank.SharedKernel`, `Bank.SharedKernel.Infrastructure` (só em `.Infrastructure`) e
frameworks conforme a camada (MOD-04). Contratos → `Bank.SharedKernel` e contratos de outros módulos.

## Registros dos testes de arquitetura

### `Violacao`

| Campo | Tipo | Regra |
| --- | --- | --- |
| `Regra` | `string` | Id `MOD-01`…`MOD-07` (research R6) |
| `Origem` | `string` | nome completo do tipo que depende |
| `Destino` | `string` | nome completo do tipo dependido |

Uma dependência proibida gera uma única violação, com a regra mais específica (ordem do R6).
Duplicatas (vários membros do mesmo tipo usando o mesmo destino) são colapsadas.

### `ExcecaoConhecida`

| Campo | Tipo | Regra |
| --- | --- | --- |
| `Regra` | `string` | obrigatório; id existente |
| `Origem` | `string` | obrigatório; nome completo |
| `Destino` | `string` | obrigatório; nome completo |
| `Motivo` | `string` | obrigatório, não vazio |
| `Issue` | `string` | obrigatório, formato `#N` |

Comparação por `(Regra, Origem, Destino)`:

```text
violação real ∉ exceções      → falha "violação nova"          (FR-018)
exceção ∉ violações reais     → falha "exceção obsoleta"       (FR-019)
exceção com campo inválido    → falha "exceção incompleta"     (SC-005)
```

Estado inicial: lista vazia (FR-017). Transição permitida num PR: só remover entradas
(constituição, Princípio VII: "um PR MUST NOT acrescentar exceções"); uma extração resolve no
próprio PR as violações que revelar.
