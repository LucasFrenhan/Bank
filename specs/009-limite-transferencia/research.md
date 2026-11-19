# Research: Limite de transferência

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Decisões técnicas do plano. Cada item: decisão, motivo e alternativas descartadas.

## R1 — Onde a regra mora

**Decisão**: no Domain, um objeto de valor `LimitesTransferencia` (limite por operação e limite
diário, ambos opcionais) com dois métodos puros:

- `VerificarOperacao(valor)` → recusa `PorOperacao` se `valor > porOperacao`;
- `VerificarDiario(valor, transferidoHoje)` → recusa `Diario` se
  `transferidoHoje + valor > diario`.

Cada recusa devolve o erro `limite-excedido` com os detalhes (R5). O caso de uso
`RealizarTransferencia` chama `VerificarOperacao` antes do bloqueio e `VerificarDiario` depois
do bloqueio (R3). `Transferencia.Executar` não muda.

**Motivo**: regra testável sem infraestrutura (Princípio II e VII). Separar em dois métodos
deixa explícita a ordem do FR-008 e evita ler o uso do dia quando o limite por operação já recusa.
Novos limites (noturno, por modalidade na #31) entram como novos métodos/valores de
`TipoLimite`, sem mudar o erro (FR-007).

**Alternativas**: verificar dentro de `Transferencia.Executar` (misturaria a soma do dia, que
vem da base, com a regra de saldo e obrigaria passar o uso para o domínio de transferência);
uma "política" genérica com lista de regras plugáveis (abstração sem segundo uso real hoje).

## R2 — Configuração

**Decisão**: `LimitesTransferenciaOptions` na Application, seção
`Transferencias:Limites`, com uma subseção por perfil:

```json
"Transferencias": {
  "Limites": {
    "PessoaFisica": { "PorOperacao": 1000.00, "Diario": 5000.00 }
  }
}
```

Valores `decimal?`; ausente, `null` ou **vazio** = sem limite (FR-005). Como o
`appsettings.json` sempre traz os padrões, a forma de desligar um limite num ambiente é sobrescrever
a chave com valor vazio (ex.: `Transferencias__Limites__PessoaFisica__PorOperacao=`); o
`ConfigurationBinder` converte texto vazio em `null` para tipos anuláveis. No Compose, as
variáveis usam `${LIMITE_PF_POR_OPERACAO-1000.00}` (sem `:`), para que valor vazio desligue o
limite e só a variável **não definida** use o padrão. Validação na inicialização
(`IValidateOptions` + `ValidateOnStart`, como `SaldoInicialOptions`): > 0, no máximo 2 casas e
`PorOperacao ≤ Diario` quando ambos existem. Padrões no `appsettings.json` (FR-006), valendo em
todos os ambientes. Leitura por `IOptionsMonitor` para que mudanças de configuração valham para
as próximas transferências (Assumptions da spec).

**Motivo**: mesmo padrão já usado para o saldo inicial; perfil como chave deixa espaço para PJ
sem mudança de formato.

**Alternativas**: tabela de limites no banco (é a base da personalização por conta, que ficou
fora do escopo); constantes no código (viola FR-005).

## R3 — Uso diário sob bloqueio

**Decisão**: o total do dia é uma soma derivada das transferências concluídas
(`SUM(valor) WHERE carteira_origem_id = @carteira AND criado_em >= @inicio AND criado_em <
@fim`), feita **depois** de `BloquearCarteirasAsync` e dentro da mesma transação, por LINQ
(`ITransferenciaRepositorio.SomarEnviadasAsync`). Novo índice
`ix_transferencias_origem_criado_em (carteira_origem_id, criado_em)`.

**Motivo**: a transação é `ReadCommitted` e a carteira de origem fica bloqueada com
`FOR UPDATE`. Uma segunda transferência da mesma PF espera esse bloqueio; quando o obtém, a
primeira já confirmou, e a soma (novo comando, novo snapshot em `ReadCommitted`) já a inclui.
Assim transferências simultâneas nunca somam mais que o limite (FR-009, SC-001) sem contador
extra. Derivar de dados imutáveis evita divergência (Assumptions "Uso diário derivado").
A soma é por LINQ, sem SQL puro (constituição v1.1.0 não exige arquivo `.sql`).

**Alternativas**: contador `transferido_hoje` na carteira (mais uma coluna mutável, reset à
meia-noite e risco de divergir do histórico); `SERIALIZABLE` (retries e falsos conflitos sem
ganho, já que o bloqueio pessimista existe); somar os lançamentos de saída (equivalente, mas
`transferencias` já tem `carteira_origem_id` e `criado_em`).

## R4 — Dia de Brasília

**Decisão**: `CalendarioBrasilia` (Application) converte "agora" (do `TimeProvider`) para o fuso
de Brasília, obtém a data local e devolve o intervalo `[00h00, 00h00 do dia seguinte)` em UTC.
O fuso é resolvido por `America/Sao_Paulo` e, se não existir, por
`E. South America Standard Time`; se nenhum existir, a aplicação não inicia (falha explícita): o
fuso é resolvido no construtor do singleton, e um `IHostedService` da Application pede o
singleton no `StartAsync`.
O `Dockerfile` do backend passa a instalar `tzdata`.

**Motivo** (verificado em 2026-11-18): a imagem `aspnet:10.0.15-alpine3.24` **não** tem
`/usr/share/zoneinfo` (o `FindSystemTimeZoneById("America/Sao_Paulo")` falha); com `apk add
tzdata` o arquivo passa a existir. No Windows, com `InvariantGlobalization=true` (no
`Directory.Build.props`), o ID IANA também falha (sem ICU para converter), mas
`E. South America Standard Time` funciona (UTC−03:00). Os testes de integração rodam a API no
processo do SDK local (Windows ou runner Linux do CI), então os dois caminhos são exercitados.
Usar o fuso de verdade, e não um deslocamento fixo de −3h, mantém a regra certa se o horário de
verão voltar.

**Alternativas**: deslocamento fixo UTC−3 (simples, mas errado se o horário de verão voltar);
NodaTime com base de fusos embutida (dependência nova só para isso); desligar
`InvariantGlobalization` (muda o comportamento de cultura do backend inteiro).

## R5 — Erro com detalhes

**Decisão**: `Erro` ganha uma propriedade opcional `Detalhes`
(`IReadOnlyDictionary<string, object?>`), e `ResultadoHttpExtensions.ParaProblema` copia os
detalhes para as extensões do `ProblemDetails`. O erro `limite-excedido` (422,
`TipoErro.RegraDeNegocio`, título "Limite de transferência excedido") leva `tipoLimite`
(`PorOperacao` | `Diario`), `limite` e `valorDisponivel` como números (mesmo formato de `valor`
no comprovante). Para `PorOperacao`, `valorDisponivel = limite`; para `Diario`,
`valorDisponivel = max(0, diario − transferidoHoje)`.

**Motivo**: Clarification Q3 — um `type` único e extensões tipadas; o frontend não interpreta o
`detail` (Princípio III). É aditivo para os erros existentes (sem detalhes, nada muda).

**Alternativas**: dois `type`s (rejeitado no clarify); detalhes só no texto (frontend teria de
fazer parsing de mensagem).

## R6 — Consulta de limites

**Decisão**: `GET /api/v1/transferencias/limites`, política de pagador (PF; PJ → 403), caso de
uso `ConsultarLimites` que lê as opções, o calendário e a soma do dia **sem** bloqueio (é só
informativa; a verificação que vale é a do envio). Resposta:
`{ porOperacao, diario, transferidoHoje, disponivelHoje, dia }` (limites e disponível `null` =
sem limite; `dia` = data de Brasília). A rota literal tem precedência sobre `/{transferenciaId}`,
como `/extrato`.

**Motivo**: FR-012; mesmo grupo e mesmas políticas dos endpoints de transferência; o frontend
calcula o máximo agora com `min(porOperacao, disponivelHoje)`.

**Alternativas**: incluir os limites em `GET /carteiras/minha` (mistura limites com dados da
carteira e afeta PJ, que não transfere); devolver só o máximo agora (o aviso precisa saber qual
limite restringe).

## R7 — Observabilidade

**Decisão**: a recusa usa o contador existente `bank.transferencias.recusadas` com
`motivo=limite-excedido` e a nova tag `tipo_limite` (`PorOperacao`/`Diario`), só nessa recusa.
Log estruturado `Warning` "Transferência recusada por limite {TipoLimite} na carteira
{CarteiraOrigemId}: valor {Valor}, limite {Limite}, disponível {ValorDisponivel}" (FR-016).
A consulta de limites ganha o contador `bank.transferencias.limites.consultas` por `resultado`
(`consultada`, `usuario-nao-encontrado`), como a consulta por identificador da spec 008
(Princípio VI: caso de uso novo com métrica de operação). Nenhum documento, nome ou e-mail (o teste `DadosSensiveisTelemetriaTests` passa a cobrir a
recusa).

**Motivo**: reaproveita o painel de recusas da spec 006 (FR-015) e mantém a cardinalidade
baixa.

**Alternativas**: contador separado `bank.transferencias.limite.recusas` (duplica a contagem de
recusas); identificador do usuário como tag (cardinalidade alta e dado pessoal indireto).

## R8 — Frontend

**Decisão**:

- Hook `useLimites` (`GET /limites`) carregado na etapa "Dados"; o máximo agora é
  `min(porOperacao, disponivelHoje)` (ignorando os `null`).
- A dica do campo valor passa a "Disponível: R$ {saldo} · Limite agora: R$ {máximo} · mínimo
  R$ 0,01" (contracts/frontend.md); se a consulta falhar, mantém a dica atual (Edge Cases).
- `continuar` recusa localmente valor acima do máximo, com o mesmo aviso do erro da API.
- `traduzirErro` reconhece `limite-excedido` e passa a carregar `limite: { tipo, limite,
  disponivel }`; `AvisoTransferencia` mostra "Limite excedido" com a frase por tipo.
- Tipos regenerados com `npm run api:tipos`.

**Motivo**: FR-013/FR-014 e Clarification Q4 (só a tela Transferir). Reaproveita o padrão do
aviso de saldo insuficiente (spec 007).

**Alternativas**: buscar os limites só na confirmação (a pessoa descobriria o limite tarde).

## R9 — Testes

**Decisão**:

- **Unitários**: `LimitesTransferencia` (inclusivo, centavo acima, sem limite, disponível nunca
  negativo); `CalendarioBrasilia` (23h59 e 00h00 de Brasília, 02h00 UTC ainda é o dia anterior);
  `LimitesTransferenciaOptions` (validação); `RealizarTransferencia` (ordem: limite antes de
  saldo; replay não verifica limite; métrica e log); `ConsultarLimites`.
- **Integração** (Testcontainers): `LimiteTransferenciaTests` — 422 por operação e diário com
  extensões, inclusive no valor exato; limite sem configuração; replay com limite esgotado;
  virada do dia com relógio controlado (dublê de `TimeProvider` na fábrica); `GET /limites` (200,
  401, 403 PJ). `ConcorrenciaLimiteTests` — 5 × R$ 300 com diário de R$ 1.000 e saldo de
  R$ 5.000 → 3 concluídas (repetido). Ajustes em `OpenApiTests` e
  `DadosSensiveisTelemetriaTests`.
- **Frontend**: vitest para `traduzirErro`, `useLimites`, dica e aviso em Transferir e
  Confirmação; e2e de Transferir checando a dica com o limite (o saldo de demonstração não
  alcança o limite padrão, então a recusa fica nos testes de componente).

**Motivo**: critérios de aceite da issue #33 (unitários, integração e concorrência) e
Princípio VII.
