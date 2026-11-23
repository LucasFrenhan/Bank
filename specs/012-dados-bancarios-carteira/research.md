# Research: Dados bancários da carteira

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

## R1 — Dígito verificador: Luhn (módulo 10) sobre agência + número

- **Decision**: o dígito é calculado pelo algoritmo de Luhn sobre os 12 dígitos de
  `agencia (4) + numero (8)`: da direita para a esquerda, dobra-se um dígito sim, outro não
  (começando pelo mais à direita), subtrai-se 9 dos resultados maiores que 9, soma-se tudo e o
  dígito é `(10 - soma % 10) % 10`. A mesma regra existe em C# (Domain, `ContaBancaria`) e em
  uma function SQL `IMMUTABLE` (`calcular_digito_conta`), usada pela migration, pelos scripts e
  por um `CHECK` nas tabelas. Um teste compara as duas implementações.
- **Rationale**: FR-004 exige que um erro em um único dígito seja sempre detectado. Luhn
  detecta 100% dos erros de um dígito e quase todas as trocas de dígitos vizinhos, e o
  resultado é sempre um único dígito de 0 a 9. O módulo 11, citado como referência na spec,
  produz 10 ou 11 para alguns números; mapear esses casos para 0 (ou usar "X") faz alguns erros
  de um dígito passarem despercebidos ou exige um caractere não numérico.
- **Alternatives considered**: módulo 11 com pesos 2–9 (resto 10/11 → 0: perde a garantia de
  FR-004; "X": quebra "1 dígito"); Verhoeff/Damm (detectam mais trocas, mas são pouco
  conhecidos e mais difíceis de reproduzir em SQL).

## R2 — Numeração sequencial por `SEQUENCE` do PostgreSQL

- **Decision**: uma sequence `seq_numero_conta_one_bank` (início 1, sem ciclo, máximo
  99.999.999) dá o próximo número do banco 001. O cadastro obtém o número por
  `IGeradorNumeroConta.ProximoAsync` (Application), implementado na Infrastructure com uma
  consulta nativa `Sql/Consultas/ObterProximoNumeroConta.sql` (`SELECT nextval(...)`). A
  migration de backfill e o script de poupança usam a mesma sequence.
- **Rationale**: atende à clarificação (numeração sequencial, contador único). `nextval` é
  atômico e não transacional: cadastros simultâneos nunca recebem o mesmo número e não há
  nova tentativa; o número de um cadastro que falha fica sem uso (lacuna aceita em FR-008).
  O índice único é a garantia final (FR-003).
- **Alternatives considered**: `MAX(numero) + 1` (corrida entre cadastros simultâneos, exige
  bloqueio); valor padrão da coluna com `nextval` (o dígito depende do número e teria de ser
  calculado por trigger, levando regra para o banco); tabela contadora com `UPDATE ...
  RETURNING` (serializa os cadastros na mesma linha).

## R3 — Onde a regra vive: value object `ContaBancaria` no Domain

- **Decision**: o Domain ganha `Bancos/Banco` (catálogo fixo: `Codigo`, `Nome`; `Banco.OneBank`
  e `Banco.Todos`), `Bancos/TipoContaBancaria` (`ContaCorrente = 1`, `ContaPoupanca = 2`) e o
  value object `Bancos/ContaBancaria` (banco, agência, número, dígito, tipo) com a fábrica
  `ContaBancaria.Nova(Banco banco, string agencia, long numero, TipoContaBancaria tipo)`, que
  formata com zeros à esquerda, valida (4 e 8 dígitos, número entre 1 e 99.999.999) e calcula o
  dígito (R1). `Carteira.Criar` passa a receber a `ContaBancaria` e recusa banco diferente do
  001 (FR-014). `Usuario.Criar` recebe a `ContaBancaria` e a repassa. `RegistrarUsuario` obtém o
  número (R2) e monta `ContaBancaria.Nova(Banco.OneBank, "0001", numero, ContaCorrente)` —
  o tipo não vem do comando (FR-006).
- **Rationale**: Princípio II (regra no Domain, orquestração na Application, SQL/EF na
  Infrastructure); a geração do número é infraestrutura, o formato e o dígito são regra.
- **Alternatives considered**: gerar o número dentro de `Carteira.Criar` (o Domain não acessa o
  banco); gerar tudo no repositório (regra do dígito fora do Domain).

## R4 — Mapeamento: colunas planas na tabela `carteiras`

- **Decision**: `Carteira` guarda os dados em propriedades próprias (`CodigoBanco`, `Agencia`,
  `NumeroConta`, `DigitoConta`, `TipoContaBancaria`), mapeadas para `codigo_banco char(3)`,
  `agencia char(4)`, `numero_conta char(8)`, `digito_conta char(1)` e
  `tipo_conta_bancaria varchar(20)` (`conta_corrente` / `conta_poupanca`, por conversor, como os
  enums da 011); a propriedade `ContaBancaria` da entidade monta o value object a partir delas
  e é ignorada pelo EF. Restrições: índice único `ux_carteiras_conta_bancaria (codigo_banco,
  agencia, numero_conta)`; FK `fk_carteiras_bancos_codigo_banco` (RESTRICT);
  `ck_carteiras_codigo_banco` (`= '001'`); `ck_carteiras_tipo_conta_bancaria`;
  `ck_carteiras_formato_conta` (regex de 4/8/1 dígitos); `ck_carteiras_digito_conta`
  (`digito_conta = calcular_digito_conta(agencia, numero_conta)`).
- **Rationale**: índice único e `CHECK` sobre colunas comuns são diretos no EF Core/Npgsql;
  os `CHECK` protegem também os dados gravados por script (Edge Cases). Texto (`char`)
  preserva zeros à esquerda, como o contrato exige.
- **Alternatives considered**: `ComplexProperty` do EF (índice único e `CHECK` em membros de
  tipo complexo são menos diretos e a migração de backfill fica mais frágil); tabela separada
  1:1 (join a mais sem ganho).

## R5 — Catálogo de bancos: tabela `bancos` com dados da migration

- **Decision**: tabela `bancos (codigo char(3) PK, nome varchar(60))` mapeada para a entidade
  de leitura `Banco` (chave natural `Codigo`), com os cinco registros inseridos por `HasData`
  a partir de `Banco.Todos` (FR-001). `carteiras` e `contas_externas` têm FK para ela. A
  consulta da carteira faz join para obter o nome.
- **Rationale**: o nome exibido vem do cadastro de bancos (Edge Case: mudar o nome não muda as
  contas); a FK recusa código fora dos cinco em scripts. A chave é o código de 3 dígitos,
  um dado natural e não um `Guid`: a regra de identificadores tipados (`IIdentificador<T>`
  sobre `Guid`) não se aplica, e o teste de arquitetura (propriedades `*Id` do tipo `Guid`)
  continua passando.
- **Alternatives considered**: só o catálogo em código, sem tabela (sem FK; scripts poderiam
  gravar código inválido); `Guid` como chave com o código à parte (identificador artificial
  para dados fixos).

## R6 — Contas de outros bancos: entidade `ContaExterna`

- **Decision**: tabela `contas_externas` (`id uuid`, `codigo_banco`, `agencia`, `numero_conta`,
  `digito_conta`, `tipo_conta_bancaria`, `titular_nome varchar(150)`, `titular_documento
  varchar(14)`, `criado_em`) mapeada para a entidade `Bancos/ContaExterna` com
  `ContaExternaId` tipado. Mesmas restrições de formato, tipo e dígito de `carteiras`, índice
  único `ux_contas_externas_conta_bancaria`, FK para `bancos` e `ck_contas_externas_codigo_banco`
  (`<> '001'`, FR-014). Sem FK para `usuarios`, sem senha, sem saldo (FR-011). Nenhum caso de
  uso a lê ou grava nesta entrega; só o script.
- **Rationale**: clarificação "registro independente"; ter a entidade no modelo mantém o schema
  dentro das migrations do EF (constituição) e dá aos testes acesso tipado.
- **Alternatives considered**: tabela criada só por SQL na migration, sem entidade (schema fora
  do snapshot do EF); reutilizar `carteiras` (misturaria contas sem titular cadastrado).

## R7 — Migration `AdicionarDadosBancariosCarteira` com backfill

- **Decision**: uma migration, nesta ordem: (1) cria `bancos` e insere os cinco; (2) cria a
  sequence; (3) cria a function `calcular_digito_conta` lendo
  `Sql/Procedures/CalcularDigitoConta.sql`; (4) adiciona as colunas de `carteiras` como
  anuláveis; (5) executa `Sql/Migracoes/PreencherDadosBancariosCarteiras.sql`, que numera as
  carteiras existentes com `row_number() OVER (ORDER BY id)` (UUIDv7 = ordem de criação), banco
  001, agência 0001, dígito pela function e tipo `conta_corrente`, e depois ajusta a sequence
  com `setval` para o maior número atribuído (`setval(..., 1, false)` se não houver carteiras),
  de modo que o próximo cadastro continue dali (FR-008). `nextval` não é usado no backfill
  porque, numa subconsulta, uma função volátil pode ser avaliada antes do `ORDER BY` e a ordem
  dos números não seria garantida; (6) torna as colunas obrigatórias
  e cria índice, FK e `CHECK`; (7) cria `contas_externas`. O `Down` desfaz na ordem inversa
  (a numeração atribuída é descartada).
- **Rationale**: FR-007 (preencher antes de tornar obrigatório); tudo numa transação de
  migration; base vazia passa sem erro (US2-4). A pasta `Sql/Migracoes/` é nova: comandos de
  dados executados uma única vez por migration. A constituição exige que SQL puro fique em
  `.sql` sob `Persistencia/Sql/` (atendido) e lista `Procedures/` e `Consultas/` para
  functions e consultas de execução; um `UPDATE` de backfill não é nenhum dos dois, por isso a
  subpasta própria (registrada no plano).
- **Alternatives considered**: backfill em C# dentro da migration (lento e fora da regra de SQL
  em arquivo); backfill na inicialização da API, como os lançamentos de abertura da 006
  (deixaria as colunas anuláveis até a primeira partida).

## R8 — Scripts de outros bancos e poupança em `scripts/sql/`

- **Decision**: dois scripts manuais, fora da aplicação, em
  `scripts/sql/dados-bancarios/`:
  - `registrar-contas-outros-bancos.sql`: insere contas fixas (duas por banco 002–005, uma
    corrente e uma poupança), com titulares fictícios, `id = uuidv7()` (PostgreSQL 18) e dígito
    pela function; `ON CONFLICT ON CONSTRAINT ux_contas_externas_conta_bancaria DO NOTHING`.
  - `criar-conta-poupanca.sql`: cria, se o e-mail ainda não existir, um usuário PF de
    demonstração (e-mail, CPF e nome fixos, hash de senha V3 do Identity pré-calculado para a
    senha documentada no quickstart) com carteira no banco 001, agência 0001, número da
    sequence, tipo `conta_poupanca` e saldo 0, numa transação.
  Execução: `docker compose exec -T postgres psql -U bank -d bank < scripts/...`. Os testes de
  integração executam os mesmos arquivos (duas vezes) contra o banco do Testcontainers.
- **Rationale**: FR-010/SC-005 (reexecutáveis), clarificação da poupança (novo titular, única
  conta). São scripts operacionais, não código de produção carregado pela aplicação, por isso
  ficam fora de `Persistencia/Sql/` (a regra de SQL em arquivo é atendida). Saldo 0 dispensa o
  lançamento de abertura (FR-016 da 006 só vale para saldo positivo).
- **Alternatives considered**: seed automático na partida da API (contas de outros bancos
  apareceriam em todo ambiente, inclusive nos testes, sem pedido); endpoint administrativo
  (fora do escopo; a spec diz "por script").

## R9 — Contrato de `GET /api/v1/carteiras/minha`

- **Decision**: `DadosCarteira` ganha `ContaBancaria` (dados de leitura com o nome do banco) e
  `MinhaCarteiraResposta` ganha `contaBancaria: { banco: { codigo, nome }, agencia, numero,
  digito, tipo }`, todos `string` (FR-009). Mudança aditiva: nenhum campo atual muda.
  Documentado em `contracts/carteira-minha.md` (1.1.0). O `schema.d.ts` do frontend não é
  regenerado nesta entrega (task futura do frontend).
- **Rationale**: clarificação do formato; compatível com o frontend atual (campo a mais é
  ignorado).
- **Alternatives considered**: ver Clarifications da spec.

## R10 — Testes

- **Decision**:
  - Unitários: `ContaBancaria` (dígito com vetores conhecidos, zeros à esquerda, limites,
    banco ≠ 001 recusado em `Carteira.Criar`), detecção de erro de um dígito (para todo dígito
    de um número, toda troca é detectada), `RegistrarUsuario` (pede um número e cria conta
    corrente no 001).
  - Integração: cadastro PF/PJ gera dados válidos; 50 cadastros simultâneos sem número
    repetido (SC-003); `GET /carteiras/minha` com `contaBancaria` e campos antigos; backfill
    (banco próprio no contêiner, migra até `AdicionarNotificacoesAuditoria`, insere usuários,
    carteiras, transferências; migra até o fim; confere dados, unicidade, saldos e lançamentos
    iguais; cadastro novo depois não colide); base vazia; `CHECK`/FK/único recusam dados
    inválidos; function SQL = Luhn em C#; os dois scripts executados duas vezes (SC-005), conta
    poupança visível em `/carteiras/minha` após login; OpenAPI com o objeto novo.
  - Regressão: suíte inteira das specs 001–011 (SC-006).
- **Rationale**: Princípio VII; cobre US1–US4 e SC-001–SC-006.
