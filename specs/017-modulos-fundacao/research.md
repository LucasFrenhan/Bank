# Research: Fundação do monólito modular

**Spec**: [spec.md](spec.md) · **Issue**: #60 (parte da #58) · **Constituição**: v1.3.0

## R1 — Projetos e namespaces do Shared Kernel

**Decisão**: dois projetos em `src/backend/src/SharedKernel/`:

| Projeto | Conteúdo | Depende de |
| --- | --- | --- |
| `Bank.SharedKernel` (puro) | `Dinheiro`, `Erro`/`TipoErro`, `Resultado`/`Resultado<T>`, `IIdentificador<TSelf>`, `EventoId` (namespace `Bank.SharedKernel`); `CalendarioBancario` (`Bank.SharedKernel.Calendario`); `Paginacao`, `PaginaDe<T>`, `Entradas`, `ErrosIdempotencia` (`Bank.SharedKernel.Aplicacao`) | só a BCL |
| `Bank.SharedKernel.Infrastructure` | `IdentificadorConverter<TId>` (`Bank.SharedKernel.Infrastructure.Persistencia`), agora `public` | `Bank.SharedKernel`, `Microsoft.EntityFrameworkCore.Relational` (já central) |

Mapeamento de namespaces: `Bank.Domain.Comum` → `Bank.SharedKernel`;
`Bank.Domain.Calendario` → `Bank.SharedKernel.Calendario`; `Bank.Application.Comum` →
`Bank.SharedKernel.Aplicacao`; `Bank.Infrastructure.Persistencia.IdentificadorConverter` →
`Bank.SharedKernel.Infrastructure.Persistencia`. Os arquivos são movidos com `git mv` (histórico
preservado) e os nomes dos tipos não mudam. `ErrosIdempotencia` referenciava
`ErrosTransferencia.IdempotenciaDivergente.Codigo` (dependência de contexto); o código passa a ser a constante
`ErrosIdempotencia.CodigoDivergente` (`"idempotencia-divergente"`, mesmo valor) no kernel, usada também por
`ErrosTransferencia`. Os testes do kernel acompanham os tipos (`CalendarioBancarioTests` em
`Bank.UnitTests/SharedKernel/`).

`Bank.Domain` passa a referenciar `Bank.SharedKernel`; `Bank.Infrastructure` passa a referenciar
também `Bank.SharedKernel.Infrastructure`. As demais referências chegam transitivamente.

**Racional**: os nomes dos tipos ficam iguais, então o documento OpenAPI (nomes de schema vêm do nome
do tipo), a serialização dos eventos e o modelo do EF Core não mudam. A parte pura sem pacote nenhum
satisfaz FR-002; a infraestrutura comum isola o único item que precisa do EF Core (clarificação
2026-11-27). `Aplicacao` evita um namespace com o mesmo nome da classe `Entradas` (CA1724).

**Alternativas rejeitadas**: um projeto só com EF Core (viola FR-002 e a regra de Domain sem pacotes
de persistência); um projeto por camada no kernel (`.Domain`/`.Application`) — sem ganho, os tipos de
`Application.Comum` não dependem de nada além de `Resultado`/`Erro`; manter namespaces antigos com
reexportação (FR-003 proíbe).

## R2 — `IUnidadeDeTrabalho` fica no núcleo antigo

**Decisão**: `IUnidadeDeTrabalho` e `ResultadoGravacao` continuam em `Bank.Application.Abstracoes`.

**Racional**: `ResultadoGravacao` enumera duplicidades de contextos específicos
(`CartaoDoUsuarioDuplicado`, `NumeroDeCartaoDuplicado`, `CodigoDeCobrancaDuplicado`). Movê-lo para o
kernel poria regra de contexto no Shared Kernel (Princípio VIII); generalizá-lo agora (por exemplo,
`Duplicidade(restricao)`) altera 11 pontos de chamada sem necessidade, contra RN-05. A #58 prevê a
unidade de trabalho como conexão compartilhada entre módulos ao extrair Contas: é lá que o contrato
genérico nasce, com um caso de uso real para validá-lo. A spec (FR-001) foi ajustada.

**Alternativas rejeitadas**: mover como está (kernel com regra de cartão e cobrança); generalizar já
(mudança de comportamento sem consumidor entre módulos).

## R3 — Formato de um módulo: dois projetos

**Decisão**: cada módulo `<Nome>` tem, em `src/backend/src/Modulos/<Nome>/`:

- `Bank.Modulos.<Nome>` — Domain, Application, Infrastructure e Endpoints como namespaces
  (`Bank.Modulos.<Nome>.Domain`, `.Application`, `.Infrastructure`, `.Endpoints`), todos os tipos
  `internal`; a única classe `public` é o ponto de registro `Modulo<Nome>` no namespace raiz
  `Bank.Modulos.<Nome>`. `InternalsVisibleTo` só para os projetos de teste.
- `Bank.Modulos.<Nome>.Contratos` — contrato público: interfaces de consulta e comando, DTOs,
  identificadores tipados do módulo e eventos (`Bank.Modulos.<Nome>.Contratos`). Depende só de
  `Bank.SharedKernel` e de contratos de outros módulos.

**Racional**: com um assembly por módulo, "o resto é `internal`" (Princípio VIII) vale no compilador:
outro módulo não consegue nem referenciar um tipo interno. Os contratos num assembly próprio permitem
referenciar só o contrato sem carregar a implementação. A direção das camadas dentro do módulo
(Princípio II) passa a ser verificada pelas regras de arquitetura (R6, MOD-04), não por referência de
projeto.

**Alternativas rejeitadas**: quatro ou cinco projetos por módulo (um por camada) — os tipos de
Domain e Application teriam de ser `public` para a camada de cima, e o `internal` deixaria de proteger
a fronteira entre módulos; um projeto só, com contratos num namespace — outro módulo teria de
referenciar o assembly inteiro, e só a regra de arquitetura protegeria os internos.

## R4 — Registro nos hosts: ponto único por host

**Decisão**: convenção de métodos de extensão na classe pública `Modulo<Nome>`:

| Método | Quem chama | Registra |
| --- | --- | --- |
| `AddModulo<Nome>(this IServiceCollection, IConfiguration)` | API e Worker | serviços do módulo (casos de uso, repositórios, `DbContext`, opções) |
| `MapModulo<Nome>(this IEndpointRouteBuilder)` | API | endpoints do módulo |
| `AddModulo<Nome>Worker(this IServiceCollection, IConfiguration)` | Worker | serviços hospedados e consumidores (só se o módulo tiver) |

Cada host tem um único ponto, `Modulos/RegistroModulos.cs`: na API, `AddModulos(configuração)` e
`MapModulos()`; no Worker, `AddModulos(configuração)`. Nesta spec, os dois ficam vazios (nenhum
módulo real, FR-008) e são chamados no `Program.cs` **depois** do registro atual (`AddApplication`,
`AddInfrastructure`, `Map*Endpoints`), sem mudar a ordem do que existe (FR-007). Incluir um módulo
novo é uma linha em cada `RegistroModulos` (SC-006).

**Racional**: os hosts só conhecem `Modulo<Nome>` e o contrato público; não há interface `IModulo`
no kernel porque ela exigiria `IServiceCollection`/`IEndpointRouteBuilder` na parte pura. A
convenção é verificada pela regra MOD-06 (R6) e demonstrada pelos módulos de exemplo (R5).

**Alternativas rejeitadas**: descoberta por reflexão de todos os módulos carregados (registro
implícito e ordem não determinística); interface `IModulo` num projeto `Bank.Modulos.Abstracoes`
(mais um projeto sem ganho enquanto a convenção é verificada por teste).

## R5 — Módulos de exemplo para provar as regras e o registro

**Decisão**: projeto `tests/Bank.ArchitectureTests.Exemplos` (referenciado só pelos testes de
arquitetura), com `FrameworkReference` `Microsoft.AspNetCore.App` e `Microsoft.EntityFrameworkCore.Relational`,
contendo:

- `Bank.Modulos.Alfa.*` — módulo conforme: contratos (`AlfaId`, `IAlfaConsultas`), Domain,
  Application, Infrastructure (`AlfaDbContext`), Endpoints e `ModuloAlfa`.
- `Bank.Modulos.Beta.*` — módulo conforme que usa só `Bank.Modulos.Alfa.Contratos`.
- `Bank.Modulos.Gama.*` — módulo violador: um tipo por regra (usa um tipo de `Alfa.Domain`, usa
  `AlfaDbContext`, depende de um tipo em `Bank.Domain.Exemplo`, `Gama.Domain` depende de
  `Gama.Infrastructure`, contrato que depende do interno do próprio módulo).
- Tipos-isca em `Bank.SharedKernel.Exemplo`, `Bank.Api.Exemplo` e `Bank.Domain.Exemplo` para as
  regras de kernel, host e infraestrutura comum.
- `Bank.Exemplos.Host.RegistroModulos` — registro de exemplo que chama `AddModuloAlfa`/`MapModuloAlfa`.

Tudo num assembly: as regras são por namespace (R6), então a separação física não é necessária para
avaliar. O assembly nunca é carregado na arquitetura real, só na arquitetura dos exemplos.

**Racional**: FR-008 (sem módulo real) e FR-014 (cada regra com prova de detecção, sem passar por
vazio). O mesmo conjunto prova o mecanismo da lista de exceções (R7) e o registro (FR-006): um teste
monta um `ServiceCollection` e um `WebApplication` com `RegistroModulos` de exemplo, resolve
`IAlfaConsultas` e confere o endpoint mapeado — sem servidor de teste e sem pacote novo.

**Alternativas rejeitadas**: um projeto por módulo de exemplo (sete projetos para provar regras por
namespace); módulo real vazio (decidido contra na spec, clarificação 2026-11-27).

## R6 — Regras de fronteira: avaliador próprio sobre o modelo do ArchUnitNET

**Decisão**: as regras do Princípio VIII são avaliadas por `RegrasFronteira.Avaliar(Architecture)`,
que percorre `Architecture.Types` e as dependências de cada tipo (`IType.Dependencies` →
`Target`) e devolve `Violacao(Regra, Origem, Destino)` com os nomes completos dos tipos. Cada
dependência proibida recebe **uma** regra, a mais específica, na ordem abaixo:

| Id | Regra | Origem → destino proibido |
| --- | --- | --- |
| MOD-02 | Persistência de outro módulo | `Bank.Modulos.M.*` → `Bank.Modulos.N.Infrastructure.*` ou tipo derivado de `DbContext` de N (N ≠ M) |
| MOD-01 | Só o contrato público de outro módulo | `Bank.Modulos.M.*` → `Bank.Modulos.N.*` fora de `Bank.Modulos.N.Contratos.*` (N ≠ M) |
| MOD-03 | Módulo não depende do núcleo antigo nem dos hosts | `Bank.Modulos.*` → `Bank.(Domain\|`Bank.Modulos.*.(Domain|Application|Endpoints|Contratos).*` → `Bank.SharedKernel.Infrastructure|Infrastructure\|Api\|Worker).*` |
| MOD-04 | Camadas dentro do módulo (Princípio II) | `.Domain` → `.Application`/`.Infrastructure`/`.Endpoints`/EF Core/ASP.NET Core/RabbitMQ/OpenTelemetry/Npgsql; `.Application` → `.Infrastructure`/`.Endpoints`/EF Core/ASP.NET Core/RabbitMQ/OpenTelemetry/Npgsql; `.Endpoints` → `.Infrastructure`/EF Core/Npgsql; `.Contratos` → interno do próprio módulo e qualquer framework |
| MOD-05 | Shared Kernel isolado | `Bank.SharedKernel` (puro) → módulos, núcleo antigo, hosts, `Bank.SharedKernel.Infrastructure` e frameworks (EF Core, ASP.NET Core, RabbitMQ, OpenTelemetry, Npgsql, `Microsoft.Extensions.*`); `Bank.SharedKernel.Infrastructure` → módulos, núcleo antigo e hosts |
| MOD-06 | Hosts e núcleo antigo só usam contrato e registro | `Bank.(Api\|Worker\|Domain\|`Bank.Modulos.*.(Domain|Application|Endpoints|Contratos).*` → `Bank.SharedKernel.Infrastructure|Infrastructure).*` → `Bank.Modulos.N.*` fora de `.Contratos.*` e da classe `Modulo<N>` |
| MOD-07 | Infraestrutura comum só em infraestrutura | `Bank.Domain.*`, `Bank.Application.*`, `Bank.Modulos.*.(Domain|Application|Endpoints|Contratos).*` → `Bank.SharedKernel.Infrastructure|Application\|Contratos).*` → `Bank.SharedKernel.Infrastructure.*` |

Os módulos são descobertos pelo prefixo `Bank.Modulos.<Nome>` dos tipos carregados; um módulo novo
entra nas regras sem configuração. O carregamento da arquitetura real inclui os assemblies
`Bank.SharedKernel*.dll` e `Bank.Modulos.*.dll` presentes na saída dos testes (chegam pelas
referências da API e do Worker), além dos cinco atuais.

A mensagem de falha lista uma linha por violação: `MOD-0X <descrição>: <Origem> → <Destino>` (FR-015).

**Racional**: as regras fluentes do ArchUnitNET informam o tipo de origem que falhou, mas não o par
origem → destino, que é a chave da lista de exceções (R7). O avaliador usa o mesmo modelo carregado
pelo ArchUnitNET (sem dependência nova) e mantém os testes fluentes atuais (`LayerDependencyTests`)
para o núcleo antigo (FR-013).

**Alternativas rejeitadas**: regras fluentes com `Because`/exceções por tipo (sem o destino, a lista
não consegue detectar exceção obsoleta por par); NetArchTest (dependência nova sem ganho).

## R7 — Lista de exceções conhecidas

**Decisão**: `tests/Bank.ArchitectureTests/Modulos/ExcecoesConhecidas.cs` com uma lista estática de
`ExcecaoConhecida(Regra, Origem, Destino, Motivo, Issue)`, **vazia** nesta spec (FR-017). O teste
compara as violações reais com a lista:

- violação sem exceção correspondente → falha "violação nova" (FR-018);
- exceção sem violação correspondente → falha "exceção obsoleta: remova da lista" (FR-019);
- exceção sem `Motivo` ou sem `Issue` (formato `#N`) → falha (SC-005).

A correspondência é exata por `(Regra, Origem, Destino)`. A documentação fica em
`docs/desenvolvimento.md` (FR-020).

**Racional**: arquivo C# compila junto com os testes (sem parser de JSON/YAML), aparece no diff de
qualquer PR que o altere e não permite campo faltando. Detectar exceção obsoleta é o que garante que a
lista "só diminui" sem depender de revisão manual.

**Alternativas rejeitadas**: arquivo JSON (parser e esquema próprios); exceções por tipo de origem
sem destino (uma exceção encobriria violações novas do mesmo tipo).

## R8 — Prova de "sem mudança de comportamento"

**Decisão**:

1. **OpenAPI**: regenerar `src/frontend/src/api/schema.d.ts` (`npm run api:tipos`, que lê
   `/openapi/v1.json` da API) e exigir `git diff` vazio; os `OpenApiTests` existentes seguem verdes.
2. **Migrations e modelo**: `dotnet ef migrations has-pending-model-changes` sem mudanças e nenhum
   arquivo alterado em `Persistencia/Migrations/`.
3. **Eventos**: os testes de serialização e de schema dos contratos de mensageria existentes seguem
   verdes sem alteração.
4. **Suítes**: unidade, integração, arquitetura, Vitest e e2e verdes; mudanças nos testes limitadas
   a `using`, referências de projeto e aos testes novos (FR-022).
5. **Telemetria**: nenhum tipo movido tem `ILogger<T>` (categoria por nome completo) nem nome de
   `Meter`/`ActivitySource` derivado de namespace — verificado por busca antes da mudança.

**Racional**: cobre SC-001 e SC-002 com verificações que já existem no repositório, sem ferramenta
nova.

## R9 — Imagem Docker e solução

**Decisão**: `src/backend/Dockerfile` copia os dois novos `.csproj` do kernel antes do
`dotnet restore` (a camada de restore lista cada projeto); `Bank.slnx` ganha a pasta
`/src/SharedKernel/` com os dois projetos e `tests/Bank.ArchitectureTests.Exemplos`. O CI já usa a
solução inteira, sem mudança.

**Racional**: sem isso a imagem quebra no restore; a pasta `Modulos/` só entra na solução e no
Dockerfile quando existir o primeiro módulo.

## R10 — Dependências novas

**Decisão**: nenhuma. `Microsoft.EntityFrameworkCore.Relational` já está no `Directory.Packages.props`
(usado pela Infrastructure) e passa a ser referenciado também por `Bank.SharedKernel.Infrastructure` e
pelo projeto de exemplos, na mesma versão central; `Microsoft.AspNetCore.App` é `FrameworkReference`.

**Racional**: Restrições Técnicas (toda dependência nova justificada no plano) — não há.
