# Research: Cartão de crédito, cobranças, faturas e recebimento do lojista

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

## R1 — Número e CVV derivados por HMAC, nunca gravados

- **Decision**: o cartão grava só `id`, bandeira, 4 últimos dígitos, validade, `variacao` e
  `impressao_digital`. O número e o CVV são recalculados quando necessários, a partir de uma chave
  secreta (`Cartoes:ChaveDerivacao`, mínimo de 32 bytes), com HMAC-SHA256 e separação de domínio:
  - número = prefixo da bandeira (6 dígitos: `400000` Visa, `555555` Mastercard) + 9 dígitos de
    `HMAC("numero|{cartaoId}|{variacao}") mod 10^9` + dígito de Luhn;
  - CVV = `HMAC("cvv|{cartaoId}|{numero}|{MM/AA}") mod 1000`, com 3 dígitos;
  - impressão digital = `HMAC("impressao|{numero}")` em base64, com índice único.

  Se o índice da impressão digital acusar colisão, a solicitação incrementa `variacao` e tenta de
  novo (até 5 vezes). Ao exibir ou conferir, o número derivado é comparado com a impressão digital
  gravada: se divergir (chave trocada), falha com erro 500 e log sem dados do cartão.
  Abstração `IDadosCartao` na Application; implementação `DadosCartaoHmac` na Infrastructure.
- **Rationale**: FR-007 exige exibir os dados quantas vezes o titular quiser e FR-008 proíbe
  gravá-los ou permitir recuperá-los só com o banco. A derivação atende aos dois: sem a chave, o
  banco não revela nada. O espaço de 10⁹ por bandeira torna colisões raras.
- **Alternatives considered**: cifrar número e CVV no banco (continua sendo gravar; viola a RN-04 da
  issue); gravar só o hash e exibir uma única vez (perder os dados inutilizaria o único cartão do
  CPF); HMAC sem chave separada por finalidade (o CVV poderia ser deduzido de outra derivação).
- **Consequência**: trocar a chave muda todos os cartões. Fica documentado em
  `docs/desenvolvimento.md` e no `.env.example`. A rotação de chave está fora do escopo.

## R2 — Tipos que não vazam dados do cartão

- **Decision**: `NumeroCartao` e `Cvv` são value objects do Domain com `ToString()` mascarado
  (`**** **** **** 0366`, `***`). Os records que carregam dados crus (requisição de pagamento e
  comando) sobrescrevem `PrintMembers`. O número nunca vai em URL; a resposta com os dados completos
  leva `Cache-Control: no-store`. Testes varrem banco, logs, spans, métricas, outbox e respostas de
  erro à procura do número e do CVV.
- **Rationale**: FR-008 e SC-009; o `ToString` gerado por `record` é a fonte comum de vazamento.
- **Alternatives considered**: redator de logs global (não cobre spans nem mensagens de exceção).

## R3 — Comparação dos dados informados

- **Decision**: no pagamento, o sistema carrega o cartão do próprio pagador, deriva número e CVV (R1)
  e compara número, validade e CVV em tempo constante (`CryptographicOperations.FixedTimeEquals`),
  sem parar no primeiro erro. Qualquer diferença, ou pagador sem cartão, resulta em
  `cartao-nao-autorizado`. O nome do titular só precisa estar preenchido.
- **Rationale**: FR-016 pede um único erro que não diga qual dado falhou; não há busca por número.
- **Alternatives considered**: localizar o cartão pela impressão digital (permitiria saber se um
  número existe).

## R4 — Limite: coluna `limite_utilizado` sob bloqueio do cartão

- **Decision**: `cartoes` guarda `limite_total` e `limite_utilizado`, com
  `CHECK (limite_utilizado BETWEEN 0 AND limite_total)`. Aprovar soma; pagar fatura subtrai o total
  (sem os juros). Toda mudança ocorre com a linha do cartão bloqueada (`FOR UPDATE`). Testes conferem
  que `limite_utilizado` é igual à soma das compras de faturas não pagas (SC-004).
- **Rationale**: mesmo desenho do saldo da carteira; o `CHECK` é a garantia final do FR-020.
- **Alternatives considered**: somar as compras a cada pagamento (sem garantia no banco e custo
  crescente); concorrência otimista (muitas novas tentativas com pagamentos simultâneos).

## R5 — Compra, fatura e lançamento

- **Decision**: a aprovação insere uma `compras_cartao` (imutável) já ligada à fatura do seu ciclo,
  criada se ainda não existir, com o cartão bloqueado. O processamento insere o
  `lancamentos_fatura` (imutável, `compra_id` único) e soma no total da fatura. "A lançar" é a
  compra sem lançamento em fatura não liberada. A competência é o mês do vencimento nominal:
  compras de 08/M-1 a 07/M → competência M.
- **Situação derivada** de datas: `Paga` (`paga_em`), `Disponivel`/`Vencida` (`liberada_em`,
  conforme hoje > vencimento), `Fechada` (hoje > dia 07) ou `Aberta`. Nada é gravado como status.
- **Rationale**: o ciclo definido na compra cumpre a regra "vale a data da compra" mesmo com
  lançamento atrasado; registros financeiros imutáveis (Princípio IV); status derivado não depende
  de um job para virar "vencida".
- **Alternatives considered**: uma tabela só com `lancada_em` (alteraria registro financeiro);
  gravar o status (precisaria de job para `Fechada` e `Vencida`).

## R6 — Compras na virada do dia

- **Decision**: o pagamento lê o relógio **depois** de bloquear o cartão; o processamento bloqueia o
  mesmo cartão antes de lançar e liberar. Assim, uma compra das 23:59:59 ainda não confirmada segura
  o processamento daquele cartão, que a enxerga ao obter o bloqueio (`ReadCommitted`). Se o relógio da
  API estiver atrasado e a fatura do ciclo já tiver sido liberada, a compra recebe a data do primeiro
  dia do ciclo seguinte; `autorizada_em` mantém o instante real.
- **Rationale**: FR-024 e edge case da virada; nenhuma compra entra em fatura já liberada.
- **Alternatives considered**: margem de minutos após a 00:00 (não elimina a corrida); relógio do
  banco de dados (o projeto inteiro usa `TimeProvider`, e os testes dependem dele).

## R7 — Processamento diário no Worker, por verificação idempotente

- **Decision**: `ProcessarDia` (Application) recebe o instante e decide tudo por datas:
  1. se hoje não é dia útil, termina;
  2. para cada cartão com fatura não liberada que tenha compra a lançar de dia anterior, ou fatura
     fechada: numa transação por cartão, bloqueia, lança e libera as fechadas sem compra pendente;
  3. para cada recebível `Pendente` com `data_prevista <= hoje`: numa transação por recebível,
     bloqueia recebível e carteira, credita e marca `Liquidado`.

  Um `BackgroundService` no Worker (`ProcessamentoDiarioHospedado`) chama `ProcessarDia` ao iniciar,
  à 00:00 de Brasília e a cada `ProcessamentoDiario:IntervaloSegundos` (padrão 60). Falha em um item é
  registrada, os demais seguem e o item volta na próxima rodada.
- **Rationale**: FR-022/FR-023. Como a seleção é por estado e data, repetir não duplica, dias perdidos
  são recuperados sem controle extra e compras confirmadas durante a rodada entram na seguinte.
  Várias instâncias são seguras: o bloqueio reavalia o estado, e `compra_id`, `fatura_id` e
  `recebivel_id` únicos são a garantia final. O Worker já hospeda serviços e tem `tzdata`.
- **Alternatives considered**: Quartz/Hangfire (dependência nova sem ganho); cron no Compose (fora da
  aplicação, sem telemetria); tabela de execuções por dia (controle redundante); advisory lock de
  líder (desnecessário com itens idempotentes); lote único (uma falha bloquearia todos).

## R8 — Ordem de bloqueio

- **Decision**: sempre nesta ordem: **cobrança → cartão → recebível → carteira**. Pagar cobrança:
  cobrança, cartão. Cancelar: cobrança. Pagar fatura: cartão, carteira. Lançar/liberar: cartão.
  Liquidar/antecipar: recebível, carteira. Faturas são protegidas pelo bloqueio do cartão. Arquivos
  `Sql/Consultas/BloquearCobranca.sql`, `BloquearCartao.sql` e `BloquearRecebivel.sql`; a carteira
  reutiliza `BloquearCarteirasParaTransferencia.sql`. Leituras antes do bloqueio usam `AsNoTracking`.
- **Rationale**: ordem única evita deadlock, inclusive com as transferências (que só bloqueiam
  carteiras); `FOR UPDATE` exige SQL puro, que fica em `.sql`.

## R9 — Unidade de trabalho comum

- **Decision**: `IUnidadeDeTrabalho` (`IniciarAsync`, `ConfirmarAsync`, `DesfazerAsync`) em
  `ReadCommitted`; `ConfirmarAsync` devolve `Ok` ou `Conflito` com o nome da restrição única violada.
  Repositórios: `ICartaoRepositorio`, `ICobrancaRepositorio`, `IFaturaRepositorio`,
  `IRecebivelRepositorio` e `ICarteiraRepositorio`. Consultas de leitura: `ICobrancaConsultas`,
  `IFaturaConsultas`, `IRecebivelConsultas`. `IUnidadeDeTransferencia` não muda.
- **Rationale**: cinco casos de uso transacionais com composições diferentes; uma unidade por caso
  (como a de transferência) repetiria o mesmo código.

## R10 — Idempotência

- **Decision**: `Idempotency-Key` (UUID) obrigatório em pagar cobrança, pagar fatura e antecipar.
  - Cobrança: chave na autorização aprovada, índice único parcial
    `(pagador_usuario_id, chave_idempotencia) WHERE resultado = 'Aprovada'`. A repetição exige a mesma
    cobrança e os dados do mesmo cartão (conferidos como em R3); senão, 409
    `idempotencia-divergente`. Recusa não consome a chave (FR-019).
  - Fatura: único parcial `(cartao_id, chave_idempotencia)`; outra competência → divergente.
  - Recebível: único parcial `(lojista_usuario_id, chave_idempotencia)`; outro recebível → divergente.

  Verificada antes do bloqueio e de novo depois dele. Respostas: 200 na primeira vez e na repetição
  de pagamento e antecipação (não criam recurso novo).
- **Rationale**: Princípio IV e o mesmo desenho das transferências (R4 da spec 006).

## R11 — Autorizador simulado da bandeira

- **Decision**: `AutorizadorBandeira.Decidir` (Domain, puro) recebe se os dados conferem, se há fatura
  vencida, o limite disponível e o valor, nesta ordem, e devolve a decisão. Cada decisão vira uma
  `autorizacoes_cartao` imutável. Em recusa, grava-se a autorização e a última recusa da cobrança na
  mesma transação, e só então a API responde 422. Código de autorização: 6 caracteres de `A-Z0-9` por
  `RandomNumberGenerator`. Na recusa por cartão que não é do pagador, a bandeira registrada é a do
  primeiro dígito (4 Visa, 5 Mastercard) ou nula, e `cartao_id` é nulo.
- **Rationale**: RN-02 do contexto; regra pura, testável sem banco.

## R12 — Calendário bancário

- **Decision**: `CalendarioBancario` (Domain, estático e puro, com `DateOnly`): `EhDiaUtil`,
  `ProximoDiaUtil` (a própria data, se útil), `SomarDiasUteis` e `Feriados(ano)` com cache por ano.
  Móveis pela Páscoa (algoritmo de Meeus/Jones/Butcher): Carnaval −48 e −47, Sexta-feira Santa −2,
  Corpus Christi +60. A conversão de instante para dia continua no `CalendarioBrasilia`.
- **Rationale**: FR-022a, sem cadastro e para qualquer ano.
- **Vetores**: 2027 — Páscoa 28/03; Carnaval 08 e 09/02; Sexta-feira Santa 26/03; Corpus Christi
  27/05. 24/11/2026 + 30 dias úteis = 07/01/2027. 10/01/2027 (domingo) → 11/01/2027.

## R13 — Juros e taxa

- **Decision**: `JurosAtraso.Calcular(total, taxaMensal, dias) =
  Math.Round(total * taxaMensal / 100 / 30 * dias, 2, MidpointRounding.AwayFromZero)`, com a
  multiplicação antes das divisões. Dias = dias corridos entre o vencimento (já ajustado) e hoje.
  Taxa em `Cartoes:JurosAtrasoMensalPercentual` (15.00). Antecipação: constantes do Domain
  (`TaxaPercentual = 2`, `PrazoDiasUteis = 30`), mesmo arredondamento.
- **Rationale**: FR-028a e FR-032. A taxa de juros é variável por decisão do responsável; a de
  antecipação é fixa.
- Liberação depois do vencimento: `vencimento = max(vencimento, dia da liberação)` (edge case).

## R14 — Extrato e razão

- **Decision**: `TipoLancamento` ganha `PagamentoFatura` (saída), `LiquidacaoRecebivel` e
  `AntecipacaoRecebivel` (entradas). `lancamentos` ganha `fatura_id` e `recebivel_id`, com `CHECK` de
  coerência e índices únicos parciais. Contrato do extrato 1.3.0: novos valores de `tipo`,
  `contraparte` e `transferenciaId` nulos. No frontend, `lancamentos.ts` e `Inicio.tsx` passam a dar
  rótulo próprio a esses tipos: hoje mostrariam "Saldo inicial" e "Transferência enviada".
- **Rationale**: FR-034 e FR-038; a invariante saldo = razão continua valendo. É o único ajuste de
  tela desta spec e evita informação errada ao cliente.

## R15 — Evento, notificações e topologia

- **Decision**: `cobranca.paga` v1 ganha `lojistaNome` opcional, antes da primeira publicação. A fila
  de cobranças passa a ter filas de espera e o consumidor `ConsumidorNotificacoesCobranca`.
  `TipoNotificacao` ganha `ComprovanteCobranca` (pagador: "Você pagou R$ 299,90 para {lojista} no
  cartão de crédito.") e `CobrancaPaga` (lojista: "A sua cobrança de R$ 299,90 foi paga."). O nome do
  pagador não é exposto (FR-011) e nenhum dígito do cartão entra no texto.
- **Rationale**: FR-021 e FR-035; reutiliza `ConsumidorEventos`, `eventos_processados` e DLQ.

## R16 — Permissões

- **Decision**: `Permissao` ganha `UsarCartaoCredito` (PF: solicitar, consultar, faturas) e
  `GerenciarRecebiveis` (PJ). Pagar cobrança usa `PagarCobrancaCartao` e emitir/listar/cancelar,
  `GerarCobrancaCartao`, já existentes.

## R17 — Testes

- **Decision**: unitários para Domain e casos de uso; integração com PostgreSQL e RabbitMQ em
  contêiner e relógio ajustável (padrão de `LimiteDiarioTests`: token no horário real, regra no
  horário controlado). No ambiente `Testing` o serviço hospedado fica desligado
  (`ProcessamentoDiario:Habilitado=false`) e os testes chamam `ProcessarDia` por escopo do Worker; um
  teste liga o serviço para conferir o agendamento. SC-007 é conferido com 10.000 compras em 1.000
  cartões. Concorrência com 50 requisições (SC-003).
