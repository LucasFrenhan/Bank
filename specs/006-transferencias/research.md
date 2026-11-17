# Research: Motor de transferências e extrato

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-17

Sem dependências NuGet novas: EF Core + Npgsql, JwtBearer e o restante vêm das specs 002–005.
Constituição v1.1.0 (SQL puro em arquivos `.sql`).

## R1. Modelo de dados

- **Decision**: três tabelas novas (detalhes em [data-model.md](data-model.md)):
  - `transferencias` — exatamente o modelo da #8 (id UUIDv7, `chave_idempotencia`, origem,
    destino, `valor numeric(18,2) > 0`, `descricao varchar(200)`, `status`, `criado_em`), com
    `UNIQUE (carteira_origem_id, chave_idempotencia)`.
  - `lancamentos` — o **razão** da carteira (crédito/débito por carteira): abertura e os dois
    lados de cada transferência. Fonte do extrato (Clarifications: a abertura aparece no
    extrato) e da invariante "saldo = soma dos lançamentos" (FR-015).
  - `outbox_eventos` — eventos de integração gravados na mesma transação (FR-017; Princípio IV,
    Transactional Outbox). A entrega ao RabbitMQ fica para a TASK-09.
- **Rationale**: o extrato com a abertura e a invariante do saldo pedem um razão por carteira;
  `transferencias` sozinha não representa a abertura (não tem origem).
- **Alternatives considered**: extrato só sobre `transferencias` (rejeitado no clarify — não
  fecha com o saldo); abertura como transferência de uma "carteira do banco" (cria uma conta
  fictícia e distorce os totais).

## R2. Imutabilidade garantida pelo banco

- **Decision**: trigger `BEFORE UPDATE OR DELETE` em `transferencias` e `lancamentos` que lança
  erro (`impedir_alteracao_de_registro_imutavel()`), criada por migration a partir de
  `Persistencia/Sql/Procedures/ImpedirAlteracaoDeRegistrosImutaveis.sql` (regra de SQL puro). O
  `Down` da migration usa `Procedures/RemoverImpedimentoDeAlteracao.sql` — nenhum SQL como
  string no C#.
- **Rationale**: o Princípio IV diz que lançamentos MUST NOT ser alterados nem apagados; a
  trigger torna a regra verificável por teste, independentemente do código.
- **Impacto em testes existentes**: `MinhaCarteiraTests` (spec 003) e `UsuariosMeTests` (spec
  005) apagam carteira e usuário para simular "conta removida"; com lançamentos de abertura
  (FK `RESTRICT` + trigger) isso deixa de ser possível. Passam a usar um token **válido**
  (assinado com a chave de teste) com `sub` de um usuário que nunca existiu — mesmo cenário de
  negócio, sem apagar dados.

## R3. Concorrência: bloqueio pessimista ordenado

- **Decision**: transação explícita (`ReadCommitted`) e bloqueio das duas carteiras com
  `SELECT id, usuario_id, saldo, moeda, atualizado_em, xmin FROM carteiras WHERE id = ANY(@ids)
  ORDER BY id FOR UPDATE` — arquivo `Persistencia/Sql/Consultas/BloquearCarteirasParaTransferencia.sql`
  carregado como recurso embutido e executado com `FromSqlRaw` (entidades rastreadas; `xmin`
  incluído porque é o token de concorrência mapeado). A ordem por `id` (UUIDv7, comparação
  determinística no PostgreSQL) evita impasse entre A→B e B→A (#8, seção 2).
- Saldo verificado **depois** do bloqueio; débito/crédito pelo domínio (`Carteira.Debitar`,
  `Carteira.Creditar`), transferência, lançamentos e evento no mesmo `SaveChanges`, depois
  `Commit`. Qualquer falha → rollback (nada muda).
- **Alternatives considered**: concorrência otimista por `xmin` com retentativas (sob alta
  disputa gera muitas retentativas e não atende a estratégia exigida na #8);
  `SERIALIZABLE` (aborta transações e exige retentativa).

## R4. Idempotência

- **Decision**: cabeçalho `Idempotency-Key` (UUID; Clarifications). Fluxo:
  1. Antes do bloqueio: busca `(carteira_origem_id, chave)`. Existe e confere (mesma carteira de
     destino e mesmo valor; descrição não entra, como na #13) → 200 com o comprovante original;
     não confere → 409 `idempotencia-divergente`.
  2. Depois do bloqueio da carteira de origem: repete a busca (requisições simultâneas com a
     mesma chave ficam em fila no `FOR UPDATE` da origem; a segunda encontra a primeira).
  3. Rede de segurança: violação de `ux_transferencias_origem_chave` (`23505`) → rollback e
     volta ao passo 1.
- O replay não reavalia saldo nem gera evento/métrica de conclusão (conta como `replay`).

## R5. Lançamento de abertura

- **No cadastro** (spec 003 alterada): quando o saldo inicial é > 0, `Usuario.Criar` gera o
  lançamento `Abertura` (Entrada, descrição "Saldo inicial de demonstração") e o repositório o
  grava no mesmo `SaveChanges` do usuário e da carteira. Saldo inicial zero (PJ; `Production`)
  não gera lançamento — a soma vazia é zero.
- **Na inicialização** (carteiras criadas antes desta spec): serviço hospedado
  `LancamentosDeAberturaNaInicializacao`, registrado depois das migrations, executa
  `Persistencia/Sql/Consultas/CriarLancamentosDeAbertura.sql` — `INSERT ... SELECT` para cada
  carteira sem abertura e com `saldo − soma(lançamentos) > 0`. Idempotente (índice único
  parcial: uma abertura por carteira). Ligado por `Persistencia:CriarLancamentosDeAberturaNaInicializacao`
  (`true` em `Development` e `Testing`; padrão `false` — nunca roda em `Production`, FR-016).

## R6. Camadas

- **Domain**: `Transferencia` (criação com valor e descrição validados), `Lancamento`
  (`Abertura`, `DebitoDeTransferencia`, `CreditoDeTransferencia`), `ValorTransferencia`
  (≥ 0,01, ≤ 2 casas, ≤ 9.999.999.999.999.999,99), `Carteira.Debitar/Creditar` (saldo nunca
  negativo), `ErrosTransferencia`, evento `TransferenciaConcluida`. Novo `TipoErro.RegraDeNegocio`
  → 422.
- **Application**: casos de uso `ConsultarDestinatario`, `RealizarTransferencia` e
  `ObterExtrato`; abstrações `IUsuarioConsultas.ObterDestinatarioAsync(documento)`,
  `ITransferenciaRepositorio` (busca por chave, comprovante) e `IUnidadeDeTransferencia`
  (transação + bloqueio ordenado + gravação + commit), `IExtratoConsultas`; `MetricasTransferencias`.
  O caso de uso orquestra; as regras ficam no domínio; transação e SQL na Infrastructure.
- **Infrastructure**: configurações EF das tabelas novas, repositório/unidade de transferência,
  consultas de extrato (LINQ), `SqlEmbutido` (leitor dos `.sql` como recursos embutidos),
  serialização do evento para o outbox (`System.Text.Json`, camelCase).
- **Api**: `TransferenciasEndpoints` — `POST /api/v1/transferencias`,
  `POST /api/v1/transferencias/destinatarios/consultar`, `GET /api/v1/transferencias/extrato`.

## R7. Consulta do destinatário sem documento na URL

- **Decision**: `POST /api/v1/transferencias/destinatarios/consultar` com o documento no corpo.
- **Rationale**: um `GET .../{documento}` poria o CPF/CNPJ no caminho, que vai para os spans
  (`url.path`, `http.route` com valor) e logs de acesso — violaria FR-009b/FR-019.
- **Autorização**: políticas `UsuarioAutenticado` + `Permissao:RealizarTransferencia` (PJ → 403).
- **Métrica**: `bank.transferencias.destinatario.consultas` por `resultado`
  (`encontrado`, `nao-encontrado`, `autotransferencia`, `invalido`).

## R8. Extrato

- **Decision**: consulta LINQ em `lancamentos` da carteira (filtro opcional `direcao`),
  `ORDER BY criado_em DESC, id DESC`, `Skip/Take` e `Count` com o mesmo filtro; junção com
  `transferencias` → carteira da contraparte → `usuarios` para nome e documento. Índice
  `(carteira_id, criado_em DESC, id DESC)`.
- Documento da contraparte: CNPJ completo, CPF mascarado pela Application (FR-014) com a mesma
  regra do frontend (`•••.982.247-••`).
- Parâmetros: `pagina ≥ 1` (padrão 1), `1 ≤ tamanhoPagina ≤ 100` (padrão 20), `direcao`
  `Entrada|Saida` — inválidos → 400 por parâmetro.

## R9. Evento e outbox

- **Decision**: `outbox_eventos (id, tipo, versao, payload jsonb, ocorrido_em, publicado_em)`;
  evento `transferencia.concluida` versão 1 com o payload da #8 (`eventoId`, `transferenciaId`,
  `pagadorUsuarioId`, `recebedorUsuarioId`, `valor`, `ocorridoEm`), gravado no mesmo
  `SaveChanges`. `publicado_em` nulo até a TASK-09 publicar (única coluna que poderá ser
  atualizada — o outbox não é lançamento financeiro).
- **Teste de contrato** (Princípio VII): serialização do evento confere com o schema
  documentado em [contracts/evento-transferencia-concluida.md](contracts/evento-transferencia-concluida.md).

## R10. Métricas (FR-020) — meter `Bank.Transferencias`

| Instrumento | Tipo | Tags |
| --- | --- | --- |
| `bank.transferencias.concluidas` | contador | `tipo_destino` (`PessoaFisica`/`PessoaJuridica`) |
| `bank.transferencias.recusadas` | contador | `motivo` |
| `bank.transferencias.replays` | contador | — |
| `bank.transferencias.valor` | histograma (BRL) | `tipo_destino` |
| `bank.transferencias.destinatario.consultas` | contador | `resultado` |

Nenhuma tag com documento, nome, e-mail ou identificador de usuário.

## R11. Testes

- **Unitários**: `ValorTransferencia`, `Carteira.Debitar/Creditar`, `Transferencia`/`Lancamento`,
  máscara de CPF do extrato, casos de uso com dublês (todas as recusas, replay, divergência,
  corrida tratada pela violação de unicidade), serialização do evento.
- **Integração** (Testcontainers): P2P e P2B com saldos e lançamentos; cada erro do contrato;
  idempotência sequencial e 20 em paralelo; **5 × R$ 300 com saldo R$ 1.000** (fábrica com
  `Carteiras:SaldoInicial:PessoaFisica=1000` — a abertura fica consistente com o saldo, sem
  editar lançamentos); 50 cruzadas A↔B em < 10 s; extrato (paginação, filtro, abertura,
  máscara); invariante saldo = soma dos lançamentos; trigger de imutabilidade; outbox (1 por
  transferência concluída, 0 por recusa); abertura na inicialização para carteira pré-existente
  e idempotência dela; consulta do destinatário; 401/403; OpenAPI; dados sensíveis.
- **Arquitetura**: regras existentes cobrem as camadas; novo teste garante que os `.sql` de
  `Persistencia/Sql` estão embutidos no assembly da Infrastructure.
