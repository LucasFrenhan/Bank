# Implementation Plan: Fundação do monólito modular (Shared Kernel e regras de fronteira)

**Branch**: `017-modulos-fundacao` | **Date**: 2026-11-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/017-modulos-fundacao/spec.md` (issue #60, parte da #58)

## Summary

Os conceitos comuns saem do núcleo em camadas para um **Shared Kernel** em dois projetos:

- **`Bank.SharedKernel`**, a parte pura, que depende só da BCL. Recebe `Dinheiro`, `Erro`, `Resultado`, `IIdentificador`, `EventoId`, `CalendarioBancario` e as entradas comuns da Application (`Paginacao`, `PaginaDe`, `Entradas`, `ErrosIdempotencia`).
- **`Bank.SharedKernel.Infrastructure`**, a infraestrutura comum, que recebe `IdentificadorConverter`.

Os nomes dos tipos não mudam; só os namespaces. Contrato HTTP, eventos e migrations ficam idênticos. `IUnidadeDeTrabalho` fica no núcleo antigo (R2).

Fica definido o **formato de módulo**: dois projetos, `Bank.Modulos.<Nome>` (interno) e `.Contratos` (público). Fica definido também o **registro nos hosts**: `Modulo<Nome>.AddModulo<Nome>`, `MapModulo<Nome>` e `AddModulo<Nome>Worker`, chamados de um `RegistroModulos` único em cada host, vazio por enquanto. Nenhum módulo real é criado.

As **regras do Princípio VIII** (MOD-01 a MOD-07) são avaliadas por um avaliador próprio sobre o modelo do ArchUnitNET. Ele produz pares origem → destino e os compara com a **lista de exceções conhecidas**: a lista começa vazia, falha com violação nova e falha com exceção obsoleta. Módulos de exemplo num projeto de teste provam que cada regra detecta a violação e que o registro funciona.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: as das specs 002–016. **Nenhuma dependência nova**: ArchUnitNET 0.13.5 já adotado; `Microsoft.EntityFrameworkCore.Relational` já central (R10).

**Storage**: PostgreSQL 18, **sem mudança**. Nenhuma migration; `has-pending-model-changes` limpo.

**Testing**: xUnit v3, ArchUnitNET, Testcontainers e `WebApplicationFactory` (suítes existentes); Vitest e Playwright como prova de não regressão.

**Target Platform**: contêineres Linux (Compose) e SDK local.

**Project Type**: web-service + worker (backend). Frontend sem mudança.

**Performance Goals**: nenhuma. Sem mudança em tempo de execução.

**Constraints**:
- Contrato HTTP, eventos, migrations e telemetria idênticos (FR-021).
- Testes existentes só mudam em `using` e referências (FR-022).
- Nenhum módulo real (FR-008).
- Lista de exceções vazia e sem acréscimos (FR-017, constituição VII).

**Scale/Scope**:
- 2 projetos novos de produção e 1 de teste.
- 13 tipos movidos, cerca de 130 arquivos com `using` ajustado (110 de `Domain.Comum`, 17 de `Application.Comum`, 7 de `Calendario`).
- 7 regras.
- 2 `RegistroModulos`.
- 4 histórias.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Regra da constituição | Avaliação | Status |
| --- | --- | --- |
| I. Specification-First | Spec com 4 clarificações (FR-001 e FR-017 ajustados no plan); issue #60; branch = pasta | ✅ |
| II. Camadas; testes de arquitetura | `LayerDependencyTests` mantidos para o núcleo antigo; camadas dentro do módulo na MOD-04; hosts só registram (MOD-06) | ✅ |
| III. ProblemDetails | `Erro`/`Resultado` só mudam de namespace; o mapeamento HTTP não muda | ✅ |
| IV. Integridade financeira | Nenhuma operação muda; `IUnidadeDeTrabalho` intocado (R2) | ✅ |
| V. Contratos | OpenAPI e eventos idênticos (R8); contrato interno de módulo documentado em `contracts/formato-modulo.md` | ✅ |
| VI. Observabilidade | Nenhum tipo movido tem logger, `Meter` ou `ActivitySource` (R8) | ✅ |
| VII. Testes; regras do VIII no ArchUnitNET; lista que só diminui | Avaliador sobre o modelo do ArchUnitNET (R6); `ExcecoesConhecidas` vazia, com falha para violação nova, exceção obsoleta e entrada incompleta (R7); prova de detecção por regra (R5) | ✅ |
| VIII. Módulo: Domain, Application e Infrastructure próprios, contrato público, resto `internal` | Formato de dois projetos, com `internal` garantido pelo compilador (R3) | ✅ |
| VIII. Só o contrato público de outro módulo; sem persistência alheia | MOD-01, MOD-02 | ✅ |
| VIII. Dados próprios, sem FK nem join | Reservado no formato (`Infrastructure/Persistencia` do módulo); aplicado a partir da primeira extração. Nada muda no banco nesta spec | ✅ |
| VIII. Shared Kernel mínimo e justificado | Cada item justificado no R1; IDs de contexto e unidade de trabalho ficam fora (clarificação; R2); MOD-05 | ✅ |
| Restrições: estrutura `Modulos/<Nome>` e `SharedKernel/` | `SharedKernel/` criado; `Modulos/` só com o primeiro módulo (FR-008) | ✅ |
| Restrições: período de transição | Núcleo antigo pode usar contrato e registro de módulo (MOD-06), nunca o contrário (MOD-03) | ✅ |
| Restrições: SQL em `.sql` | Nenhum SQL novo; `SqlEmbutidoTests` intacto | ✅ |
| Restrições: identificadores tipados | `IIdentificador` e `EventoId` no kernel; teste de IDs atualizado para carregar o kernel | ✅ |
| Restrições: dependência nova justificada | Nenhuma (R10) | ✅ |
| Fluxo: branch e commits | Branch `017-modulos-fundacao`; commits em Conventional Commits | ✅ |

**Gate do Princípio VIII (template)**:
- Feature de fundação, sem módulo dono.
- Nenhum código novo de contexto.
- Nenhuma dependência entre módulos.
- Nenhum dado novo.
- Shared Kernel justificado item a item.
- Lista de exceções vazia.

**Resultado pré-pesquisa**: aprovado. **Re-check pós-design**: aprovado, sem itens em Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/017-modulos-fundacao/
├── plan.md
├── research.md                 # R1–R10
├── data-model.md               # elementos estruturais, Violacao, ExcecaoConhecida
├── quickstart.md
├── contracts/
│   ├── formato-modulo.md       # estrutura, Modulo<Nome>, RegistroModulos
│   └── regras-fronteira.md     # MOD-01…MOD-07, saída de falha, lista de exceções
├── checklists/requirements.md
└── tasks.md                    # /speckit-tasks
```

### Source Code (repository root)

```text
docs/desenvolvimento.md                          # seção "Módulos e fronteiras"
src/backend/
├── Bank.slnx                                    # + /src/SharedKernel/ (2) e tests/Bank.ArchitectureTests.Exemplos
├── Dockerfile                                   # COPY dos 2 .csproj do kernel antes do restore
├── src/
│   ├── SharedKernel/
│   │   ├── Bank.SharedKernel/                   # sem PackageReference
│   │   │   ├── Dinheiro.cs, Erro.cs, Resultado.cs, IIdentificador.cs, EventoId.cs   # de Bank.Domain/Comum
│   │   │   ├── Calendario/CalendarioBancario.cs                                    # de Bank.Domain/Calendario
│   │   │   └── Aplicacao/Entradas.cs                                               # de Bank.Application/Comum
│   │   └── Bank.SharedKernel.Infrastructure/
│   │       └── Persistencia/IdentificadorConverter.cs                              # de Bank.Infrastructure (public)
│   ├── Bank.Domain/                             # Comum/ e Calendario/ removidos; + ref Bank.SharedKernel
│   ├── Bank.Application/                        # Comum/ removido; usings
│   ├── Bank.Infrastructure/                     # + ref Bank.SharedKernel.Infrastructure; usings
│   ├── Bank.Api/
│   │   ├── Modulos/RegistroModulos.cs           # AddModulos / MapModulos (vazios)
│   │   └── Program.cs                           # chama AddModulos e MapModulos após o registro atual
│   └── Bank.Worker/
│       ├── Modulos/RegistroModulos.cs           # AddModulos (vazio)
│       └── Program.cs
└── tests/
    ├── Bank.ArchitectureTests/
    │   ├── Modulos/RegrasFronteira.cs           # avaliador MOD-01…MOD-07 → Violacao
    │   ├── Modulos/ExcecoesConhecidas.cs        # ExcecaoConhecida + lista vazia + comparação
    │   ├── Modulos/CarregadorArquitetura.cs     # real (núcleo, hosts, Bank.SharedKernel*, Bank.Modulos.*) e exemplos
    │   ├── Modulos/FronteirasTests.cs           # arquitetura real sem violação fora da lista; lista válida
    │   ├── Modulos/DeteccaoRegrasTests.cs       # exemplos: violações esperadas por regra; mecanismo da lista
    │   ├── Modulos/RegistroModuloTests.cs       # Alfa registrado: serviço resolvido e endpoint mapeado
    │   ├── Modulos/PontoDeRegistroTests.cs      # Modulo<Nome> é o único tipo público fora dos contratos
    │   ├── LayerDependencyTests.cs              # inalterado nas regras; carrega o kernel
    │   └── IdentificadoresTipadosTests.cs       # IIdentificador do kernel; inclui o assembly do kernel
    ├── Bank.ArchitectureTests.Exemplos/         # Alfa e Beta (conformes), Gama (violador), iscas, host de exemplo
    ├── Bank.UnitTests/                          # usings (+ testes do kernel continuam onde estão)
    └── Bank.IntegrationTests/                   # usings
```

**Structure Decision**: o backend segue o "Option 0" do template, com o núcleo antigo, `SharedKernel/` e os hosts. `Modulos/` nasce na primeira extração. Os testes do kernel continuam em `Bank.UnitTests`, só com os namespaces ajustados. Um projeto de teste por módulo fica para quando houver módulo.

## Ordem de implementação

1. **Shared Kernel** (US1):
   - Criar os 2 projetos.
   - `git mv` dos arquivos e troca dos namespaces.
   - Atualizar referências, solução e Dockerfile.
   - Build e suíte completa verdes.
   - Rodar `has-pending-model-changes`.
2. **Regras e exceções** (US2, US3):
   - Avaliador, carregador e lista.
   - Projeto de exemplos com Gama violador.
   - Testes de detecção e da arquitetura real.
3. **Registro** (US4):
   - `RegistroModulos` nos hosts.
   - `ModuloAlfa` de exemplo.
   - `RegistroModuloTests`.
4. **Documentação e prova final**:
   - Seção em `docs/desenvolvimento.md`.
   - `schema.d.ts` sem diff.
   - e2e verde.

## Complexity Tracking

Sem violações a justificar.
