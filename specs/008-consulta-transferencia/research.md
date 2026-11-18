# Research: Consulta de transferência por identificador

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Base: o backend da spec [006](../006-transferencias/plan.md) (.NET 10, Minimal APIs, EF Core +
Npgsql, xUnit v3 + Testcontainers, ArchUnitNET). Nenhuma dependência nova.

## R1 — Caso de uso e leitura

- **Decision**: novo caso de uso `ConsultarTransferencia` em `Bank.Application/Transferencias`,
  que recebe o usuário (claims) e o identificador como texto. Ele valida o UUID, lê o comprovante
  por identificador com um novo método `ObterComprovantePorIdAsync(Guid)` em
  `ITransferenciaRepositorio` (mesma projeção LINQ de `ObterComprovantePorChaveAsync`, que passa a
  usar uma consulta base compartilhada) e decide o acesso comparando o usuário com
  `Pagador.UsuarioId` e `Recebedor.UsuarioId`.
- **Rationale**: endpoint só transporte (Princípio II); a regra de quem pode ver fica testável em
  unidade; uma única projeção evita divergência entre o comprovante do envio e o da consulta
  (SC-001).
- **Alternatives considered**: filtrar por participante na própria consulta SQL (mistura a regra
  de acesso com a persistência e dificulta o teste unitário); reaproveitar `IExtratoConsultas`
  (o extrato não traz a chave nem os dois lados).

## R2 — Identificador inválido e rota

- **Decision**: rota `GET /api/v1/transferencias/{transferenciaId}` **sem** restrição de tipo
  (`{transferenciaId}`, texto); o caso de uso faz `Guid.TryParse` e devolve 400 `validacao` com
  erro no parâmetro `transferenciaId`. A rota literal `/extrato` continua com precedência sobre o
  parâmetro (regra do roteamento do ASP.NET Core).
- **Rationale**: FR-005 pede 400; uma restrição `:guid` faria rotas inválidas caírem em 404
  genérico, sem `ProblemDetails` de validação.
- **Alternatives considered**: `{transferenciaId:guid}` (404 sem corpo padronizado).

## R3 — Sem revelar existência (FR-004)

- **Decision**: inexistente e "não participa" retornam o **mesmo** `Erro`
  (`ErrosTransferencia.TransferenciaNaoEncontrada`, `TipoErro.NaoEncontrado`) pelo mesmo caminho de
  código; a leitura é feita uma vez nos dois casos. Os testes de integração comparam status,
  `type`, `title` e `detail` das duas respostas.
- **Rationale**: nenhuma diferença de corpo; o tempo de resposta também fica equivalente porque a
  mesma consulta é executada.
- **Alternatives considered**: 403 para "não participa" (revela a existência).

## R4 — Formato único do comprovante (Clarifications 1 e 2)

- **Decision**:
  - `ChaveIdempotencia` passa a `Guid?` no `Comprovante` (Application) e em `ComprovanteResposta`
    (Api). O envio continua sempre preenchendo; a consulta preenche só para o pagador.
  - `PagadorComprovante`/`PagadorResposta` ganham `Documento`, **mascarado** com
    `Documento.Ocultar` (regra do extrato). No envio, o documento vem de `IUsuarioConsultas`
    (titular), já injetado em `RealizarTransferencia`; no repositório, de `pagador.Documento` na
    mesma projeção.
  - A máscara é aplicada na Application (como em `ObterExtrato`), nunca no endpoint.
- **Rationale**: um único tipo de comprovante para o frontend (spec 007) e para os dois endpoints.
  As mudanças nos endpoints existentes são compatíveis: um campo novo e um campo que, no envio,
  continua sempre preenchido (Princípio V, versão MINOR 1.1.0).
- **Alternatives considered**: esquema separado para a consulta (rejeitado na Clarification 1);
  documento do pagador sem máscara (exporia CPF completo ao recebedor).

## R5 — Métrica e telemetria (FR-007)

- **Decision**: contador `bank.transferencias.consultas` (`{consulta}`) em
  `MetricasTransferencias`, com o rótulo `resultado` = `encontrada` | `nao-encontrada` |
  `invalida`. Nenhum rótulo com identificador, documento ou nome. Sem log por consulta bem-sucedida;
  recusas não geram log de aplicação (já há trace HTTP com status).
- **Rationale**: detecta varredura (muitos `nao-encontrada`) sem alta cardinalidade; segue o padrão
  de `bank.transferencias.destinatario.consultas`.
- **Alternatives considered**: rótulo por usuário (cardinalidade e dado pessoal); log por consulta
  (ruído).

## R6 — Testes

- **Decision**:
  - **Unidade** (`ConsultarTransferenciaTests`, com os dublês de `DublesTransferencia.cs`): id
    inválido → 400 e métrica `invalida`; inexistente e terceiro → mesmo erro e métrica
    `nao-encontrada`; pagador → comprovante com chave; recebedor → chave nula; documento do pagador
    mascarado; métrica `encontrada`.
  - **Integração** (`ConsultaTransferenciaTests`, Testcontainers): pagador 200 igual ao comprovante
    do envio (inclusive após nova sessão); recebedor PJ e PF 200 com chave nula; terceiro e
    inexistente com respostas idênticas; 400; 401 sem token/token inválido; 403 com token sem
    claims (`TokensDeTeste`); saldos e lançamentos inalterados após consultas.
  - **Ajustes**: testes do envio passam a conferir `pagador.documento` mascarado;
    `OpenApiTests` confere a operação nova e os códigos; `DadosSensiveisTelemetriaTests` inclui a
    consulta; `TelemetryTests` confere a métrica por resultado.
- **Rationale**: Princípio VII (unidade para o caso de uso; integração com 200/400/401/403/404).

## R7 — Impacto na spec 007 (fase 10 da issue #32)

- **Decision**: registrado para o PR que finaliza a #32, sem mudar o frontend agora:
  - o comprovante passa a usar `pagador.documento` (mascarado) para pagador e recebedor, e a FR-011
    da spec 007 é atualizada;
  - o tipo gerado `chaveIdempotencia` passa a `string | null`;
  - o teste de telemetria do frontend que proíbe o identificador da transferência nos sinais deve
    ser revisto, pois a chamada `GET /api/v1/transferencias/{id}` leva o identificador no caminho
    (permitido pela FR-007 desta spec).
