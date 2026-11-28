# Contrato: formato de módulo e registro nos hosts

**Versão**: 1.0.0 · **Spec**: [../spec.md](../spec.md) · **Research**: R3, R4

Contrato interno do backend (Princípio V, contrato público de módulo): define o que todo módulo
extraído na #58 segue. Não há contrato HTTP nem de mensageria novo; os existentes não mudam.

## Estrutura

```text
src/backend/src/Modulos/<Nome>/
├── Bank.Modulos.<Nome>/                    # implementação; tudo internal, exceto Modulo<Nome>
│   ├── Modulo<Nome>.cs                     # ponto de registro (public static)
│   ├── Domain/                             # Bank.Modulos.<Nome>.Domain
│   ├── Application/                        # Bank.Modulos.<Nome>.Application
│   ├── Infrastructure/                     # Bank.Modulos.<Nome>.Infrastructure
│   │   └── Persistencia/                   # DbContext do módulo, mapeamento, Sql/, Migrations/
│   └── Endpoints/                          # Bank.Modulos.<Nome>.Endpoints
└── Bank.Modulos.<Nome>.Contratos/          # contrato público (public)
    └── ...                                 # interfaces, DTOs, identificadores, eventos
```

- `InternalsVisibleTo`: `Bank.UnitTests`, `Bank.IntegrationTests`, `Bank.ArchitectureTests` e
  `DynamicProxyGenAssembly2` só se um teste precisar de dublê de tipo interno.
- Schema, `DbContext` e migrations próprios ficam para a spec de cada extração (Princípio VIII);
  esta spec só reserva o lugar.

## Ponto de registro

```csharp
namespace Bank.Modulos.<Nome>;

public static class Modulo<Nome>
{
    // API e Worker
    public static IServiceCollection AddModulo<Nome>(this IServiceCollection servicos, IConfiguration configuracao);

    // Só API
    public static IEndpointRouteBuilder MapModulo<Nome>(this IEndpointRouteBuilder rotas);

    // Só Worker, apenas se o módulo tiver serviços hospedados ou consumidores
    public static IServiceCollection AddModulo<Nome>Worker(this IServiceCollection servicos, IConfiguration configuracao);
}
```

Regras:

- `Modulo<Nome>` é o único tipo público de `Bank.Modulos.<Nome>`.
- Os métodos não leem nada além da `IConfiguration` recebida e não dependem de ordem entre módulos.
- Endpoints seguem o Princípio II: transporte apenas, chamando a Application do próprio módulo.

## Registro nos hosts

| Host | Arquivo | Métodos | Chamado em `Program.cs` |
| --- | --- | --- | --- |
| `Bank.Api` | `Modulos/RegistroModulos.cs` | `AddModulos(this IServiceCollection, IConfiguration)`, `MapModulos(this IEndpointRouteBuilder)` | depois de `AddInfrastructure(...)`; depois do último `Map*Endpoints()` |
| `Bank.Worker` | `Modulos/RegistroModulos.cs` | `AddModulos(this IServiceCollection, IConfiguration)` | depois do registro atual da Infrastructure |

Nesta spec os dois `RegistroModulos` estão vazios. Incluir um módulo:

```csharp
// Bank.Api/Modulos/RegistroModulos.cs
servicos.AddModulo<Nome>(configuracao);   // em AddModulos
rotas.MapModulo<Nome>();                  // em MapModulos

// Bank.Worker/Modulos/RegistroModulos.cs
servicos.AddModulo<Nome>(configuracao).AddModulo<Nome>Worker(configuracao);
```

Além disso: referência ao projeto `Bank.Modulos.<Nome>` nos dois hosts, inclusão em `Bank.slnx` e
cópia dos `.csproj` no `Dockerfile` antes do restore.

## Verificação

- Regras MOD-01 a MOD-07 ([regras-fronteira.md](regras-fronteira.md)) aplicadas automaticamente a
  todo namespace `Bank.Modulos.<Nome>`.
- O formato e o registro são demonstrados pelo módulo de exemplo `Alfa`
  (`tests/Bank.ArchitectureTests.Exemplos`).
