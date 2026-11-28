# Contrato: regras de fronteira e lista de exceções

**Versão**: 1.0.0 · **Spec**: [../spec.md](../spec.md) · **Research**: R5, R6, R7

## Regras

Avaliadas sobre todos os tipos carregados (núcleo antigo, hosts, Shared Kernel e
`Bank.Modulos.*`). Cada dependência proibida gera uma violação com a primeira regra que casar, nesta
ordem. `M` e `N` são nomes de módulos diferentes.

| Id | Descrição (texto da falha) | Origem | Destino proibido |
| --- | --- | --- | --- |
| MOD-02 | Módulo usa a persistência de outro módulo | `Bank.Modulos.M.*` | `Bank.Modulos.N.Infrastructure.*`; tipo de `Bank.Modulos.N.*` derivado de `DbContext` |
| MOD-01 | Módulo usa algo de outro módulo fora do contrato público | `Bank.Modulos.M.*` | `Bank.Modulos.N.*` exceto `Bank.Modulos.N.Contratos.*` |
| MOD-03 | Módulo depende do núcleo antigo ou de um host | `Bank.Modulos.*` | `Bank.Domain.*`, `Bank.Application.*`, `Bank.Infrastructure.*`, `Bank.Api.*`, `Bank.Worker.*` |
| MOD-04 | Camada do módulo depende de camada ou framework proibido | ver tabela abaixo | ver tabela abaixo |
| MOD-05 | Shared Kernel depende de módulo, núcleo antigo, host ou framework | `Bank.SharedKernel` (exceto `.Infrastructure`) | `Bank.Modulos.*`, núcleo antigo, hosts, `Bank.SharedKernel.Infrastructure.*`, `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore*`, `Microsoft.Extensions.*`, `RabbitMQ.Client*`, `OpenTelemetry*`, `Npgsql*` |
| MOD-05 | (idem) | `Bank.SharedKernel.Infrastructure.*` | `Bank.Modulos.*`, núcleo antigo, hosts |
| MOD-06 | Host ou núcleo antigo usa interno de módulo | `Bank.Api.*`, `Bank.Worker.*`, núcleo antigo | `Bank.Modulos.N.*` exceto `Bank.Modulos.N.Contratos.*` e `Bank.Modulos.N.Modulo<N>` |
| MOD-07 | Domínio, aplicação, endpoint ou contrato usa a infraestrutura comum | `Bank.Domain.*`, `Bank.Application.*`, `Bank.Modulos.*.Domain.*`, `Bank.Modulos.*.Application.*`, `Bank.Modulos.*.Endpoints.*`, `Bank.Modulos.*.Contratos.*` | `Bank.SharedKernel.Infrastructure.*` |

### MOD-04 — camadas dentro de `Bank.Modulos.M`

| Origem | Destino proibido |
| --- | --- |
| `.Domain` | `.Application`, `.Infrastructure`, `.Endpoints` do próprio módulo; EF Core, ASP.NET Core, RabbitMQ, OpenTelemetry, Npgsql |
| `.Application` | `.Infrastructure`, `.Endpoints` do próprio módulo; EF Core, ASP.NET Core, RabbitMQ, OpenTelemetry, Npgsql |
| `.Endpoints` | `.Infrastructure` do próprio módulo; EF Core, Npgsql |
| `.Contratos` | `Bank.Modulos.M.*` fora de `.Contratos`; qualquer pacote fora da BCL |

As regras atuais do núcleo antigo (`LayerDependencyTests`, `IdentificadoresTipadosTests`,
`SqlEmbutidoTests`) continuam como estão (FR-013).

## Saída de falha

```text
Violações de fronteira (constituição, Princípio VIII):
MOD-01 Módulo usa algo de outro módulo fora do contrato público: Bank.Modulos.Gama.Application.UsaAlfa → Bank.Modulos.Alfa.Domain.Alfa
```

Exceção obsoleta:

```text
Exceções conhecidas sem violação correspondente (remova de ExcecoesConhecidas):
MOD-03 Bank.Modulos.X.Application.Y → Bank.Domain.Z (#123)
```

## Lista de exceções

Arquivo `src/backend/tests/Bank.ArchitectureTests/Modulos/ExcecoesConhecidas.cs`:

```csharp
internal static class ExcecoesConhecidas
{
    public static readonly IReadOnlyList<ExcecaoConhecida> Todas = [];
    // Exemplo de entrada:
    // new("MOD-03", "Bank.Modulos.X.Application.Y", "Bank.Domain.Z", "motivo", "#123"),
}
```

- Comparação exata por `(Regra, Origem, Destino)`; `Motivo` não vazio e `Issue` no formato `#N`.
- Violação sem exceção → falha. Exceção sem violação → falha. Entrada incompleta → falha.
- Nenhum PR acrescenta entradas (constituição, Princípio VII); começa vazia (FR-017).

## Prova de detecção (FR-014)

Contra a arquitetura dos exemplos (`tests/Bank.ArchitectureTests.Exemplos`), o avaliador MUST
devolver exatamente as violações esperadas: ao menos uma por regra (MOD-01 a MOD-07), nenhuma para os
módulos conformes `Alfa` e `Beta`. O mecanismo da lista é provado com listas de exemplo: uma
faltando uma violação (falha "nova"), uma com entrada sem violação (falha "obsoleta") e uma com
`Issue` inválida (falha "incompleta").
