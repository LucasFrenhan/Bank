# Data Model: Cartão de crédito, cobranças, faturas e recebimento do lojista

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md)

Dinheiro: `decimal` / `numeric(18,2)`, BRL. Instantes: `timestamptz` UTC com precisão de
microssegundos. Dias de regra: `date`, dia civil de Brasília. Enums gravados como texto.

## Domain

### `Bandeira` (`Cartoes/`) — catálogo, chave natural

`Codigo` (`char(3)`), `Nome`. `Visa` = `001`, `Mastercard` = `002`; `Todas`, `DeCodigo`.

### `Cartao` (`Cartoes/`)

| Campo | Regra |
| --- | --- |
| `Id` `CartaoId` | UUIDv7 |
| `UsuarioId` | PF; um cartão por usuário |
| `CodigoBandeira` | `001`/`002`; imutável |
| `NomeImpresso` | nome do titular em maiúsculas, 1–150 |
| `UltimosDigitos` | 4 dígitos |
| `Validade` `ValidadeCartao` | mês/ano; mês da criação + 60 meses; vale até o fim do mês (Brasília) |
| `Variacao` | 0–4; entra na derivação do número (R1) |
| `ImpressaoDigital` | base64 de 32 bytes; única |
| `LimiteTotal` | > 0; copiado da configuração na criação; imutável |
| `LimiteUtilizado` | 0 ≤ x ≤ `LimiteTotal` |
| `CriadoEm` | |

Métodos: `Emitir(...)`, `LimiteDisponivel`, `ConsumirLimite(valor)`, `LiberarLimite(valor)`.

### Value objects (`Cartoes/`)

- `NumeroCartao`: 16 dígitos, Luhn; `UltimosDigitos`; `ToString()` mascarado (R2). Aceita espaços na
  entrada.
- `Cvv`: exatamente 3 dígitos; `ToString()` = `***`.
- `ValidadeCartao`: `MM/AA`; `EstaVencida(DateOnly hoje)`.
- `CodigoAutorizacao`: 6 caracteres `[A-Z0-9]`.
- `CompetenciaFatura`: ano e mês; `De(DateOnly dataCompra)` (dia ≥ 08 → mês seguinte); `Inicio` (08 do
  mês anterior), `Fechamento` (07), `VencimentoNominal` (10), `Vencimento`
  (`CalendarioBancario.ProximoDiaUtil`); texto `AAAA-MM`.

### `AutorizacaoCartao` (`Cartoes/`) — imutável

`Id`, `CobrancaId`, `PagadorUsuarioId`, `CartaoId?`, `CodigoBandeira?`, `UltimosDigitos`, `Valor`,
`Resultado` (`Aprovada`, `Recusada`), `Motivo?` (`CartaoNaoAutorizado`, `FaturaVencida`,
`LimiteInsuficiente`), `CodigoAutorizacao?`, `ChaveIdempotencia`, `CriadoEm`.
`AutorizadorBandeira.Decidir(dadosConferem, temFaturaVencida, limiteDisponivel, valor)` (R11).

### `CompraCartao` (`Cartoes/`) — imutável

`Id`, `CartaoId`, `CobrancaId` (única), `AutorizacaoId`, `FaturaId`, `Estabelecimento` (1–150),
`Descricao` (1–200), `Valor`, `DataCompra`, `AutorizadaEm`.

### `Fatura` (`Cartoes/`)

| Campo | Regra |
| --- | --- |
| `Id` `FaturaId`; `CartaoId`; `Competencia` | única por cartão e competência |
| `Inicio`, `Fechamento`, `Vencimento` | datas do ciclo; `Vencimento` pode avançar na liberação tardia |
| `Total` | soma dos lançamentos |
| `LiberadaEm?`, `PagaEm?` | |
| `TaxaJurosMensal?`, `DiasAtraso?`, `Juros?`, `ValorPago?`, `ChaveIdempotencia?` | preenchidos no pagamento |

Métodos: `Abrir(cartao, competencia)`, `Lancar(compra, agora)` → `LancamentoFatura` (só antes da
liberação e de compra da própria fatura), `Liberar(hoje, agora)` (só com hoje > `Fechamento`),
`Situacao(hoje)`, `CalcularPagamento(hoje, taxa)` → total, dias, juros, valor a pagar,
`Pagar(hoje, taxa, chave, agora)`.

```text
Aberta ──(fim do dia 07)──▶ Fechada ──(processamento)──▶ Disponivel ──(fim do vencimento)──▶ Vencida
                                                              └───────(pagamento)──▶ Paga ◀───┘
```

### `LancamentoFatura` (`Cartoes/`) — imutável

`Id`, `FaturaId`, `CompraId` (única), `Estabelecimento`, `Descricao`, `Valor`, `DataCompra`,
`LancadoEm`.

### `JurosAtraso` (`Cartoes/`) — cálculo puro (R13)

### `Cobranca` (`Cobrancas/`)

| Campo | Regra |
| --- | --- |
| `Id` `CobrancaId`; `LojistaUsuarioId`; `CarteiraDestinoId` | |
| `Valor` | mesmas regras de `ValorTransferencia` (0,01 a 9.999.999.999.999.999,99; 2 casas) |
| `Descricao` | 1–200 após remover espaços das pontas |
| `Status` | `Pendente`, `Paga`, `Cancelada` |
| `PagadorUsuarioId?`, `CodigoBandeira?`, `UltimosDigitosCartao?`, `CodigoAutorizacao?`, `PagaEm?` | preenchidos juntos, só em `Paga` |
| `UltimaRecusaMotivo?`, `UltimaRecusaEm?` | juntos |
| `CriadoEm`, `CanceladaEm?` | `CanceladaEm` só em `Cancelada` |

Métodos: `Emitir`, `Cancelar`, `RegistrarRecusa`, `Pagar(autorizacao, agora)`.

```text
Pendente ──pagar──▶ Paga
   │ └─recusa─▶ Pendente (guarda a última recusa)
   └──cancelar──▶ Cancelada
```

`PagamentoCobranca.Executar(...)` reúne os efeitos da aprovação: cobrança, cartão, autorização, compra,
recebível e evento `CobrancaPaga` (FR-018).

### `Recebivel` (`Recebiveis/`)

`Id`, `CobrancaId` (única), `LojistaUsuarioId`, `CarteiraId`, `ValorBruto`, `Taxa` (0 até antecipar),
`ValorLiquido` (= bruto − taxa), `DataPrevista`, `Situacao` (`Pendente`, `Liquidado`, `Antecipado`),
`CriadoEm`, `RecebidoEm?`, `ChaveIdempotencia?`. Métodos: `Criar` (data prevista = 30º dia útil),
`Liquidar(hoje, agora)` (hoje ≥ data prevista), `Antecipar(hoje, chave, agora)` (hoje < data prevista;
taxa de 2%). Constantes `PrazoDiasUteis = 30`, `TaxaPercentual = 2`.

### `CalendarioBancario` (`Calendario/`) — puro (R12)

### Alterações

- `Lancamento`: `FaturaId?`, `RecebivelId?`; fábricas `DePagamentoFatura`, `DeLiquidacao`,
  `DeAntecipacao`. `Descricao` truncada em 200.
- `TipoLancamento`, `TipoNotificacao`, `Permissao`, `PermissoesPorPerfil` (R14–R16).

## Application

- Casos de uso: `Cartoes/` (`SolicitarCartao`, `ObterMeuCartao`, `ExibirDadosCartao`); `Cobrancas/`
  (`EmitirCobranca`, `ConsultarCobranca`, `ListarMinhasCobrancas`, `CancelarCobranca`,
  `PagarCobranca`); `Faturas/` (`ListarFaturas`, `ObterFatura`, `PagarFatura`); `Recebiveis/`
  (`ListarRecebiveis`, `ObterRecebivel`, `AnteciparRecebivel`); `Processamento/ProcessarDia`;
  `Notificacoes/NotificarCobrancaPaga`.
- `Cartoes/CartoesOptions` (`Cartoes:LimitePadrao` 5000.00; `Cartoes:JurosAtrasoMensalPercentual`
  15.00), validadas na partida: limite > 0 com 2 casas; juros ≥ 0 e ≤ 100.
- Abstrações: R9, mais `IDadosCartao` (R1) e `INotificadorCobranca`.
- `Observabilidade/`: `MetricasCartoes`, `MetricasProcessamento` (ver plan).

## PostgreSQL — migration `AdicionarCartoesCobrancasERecebiveis`

| Tabela | Chaves, índices e restrições |
| --- | --- |
| `bandeiras` | `codigo char(3)` PK, `nome varchar(30)`; `HasData` 001/002 |
| `cartoes` | FK usuário (RESTRICT) e bandeira; `ux_cartoes_usuario_id`; `ux_cartoes_impressao_digital`; `ck_cartoes_limite` (`limite_total > 0 AND limite_utilizado BETWEEN 0 AND limite_total`); `ck_cartoes_ultimos_digitos`; `ck_cartoes_validade` (mês 1–12) |
| `cobrancas` | FKs lojista, carteira, pagador, bandeira; `ck_cobrancas_valor_positivo`; `ck_cobrancas_status`; `ck_cobrancas_paga_coerente`; `ck_cobrancas_cancelada_coerente`; `ck_cobrancas_recusa_coerente`; `ck_cobrancas_ultimos_digitos`; `ix_cobrancas_lojista (lojista_usuario_id, criado_em DESC, id DESC)` |
| `autorizacoes_cartao` | FKs; `ck_..._resultado_coerente` (aprovada ⇔ código, cartão e sem motivo); `ux_autorizacoes_cartao_chave` parcial (R10); `ux_autorizacoes_cartao_cobranca_aprovada` parcial; trigger de imutabilidade |
| `faturas` | FK cartão; `ux_faturas_cartao_competencia`; `ux_faturas_chave` parcial; `ck_faturas_datas`; `ck_faturas_pagamento_coerente`; `ix_faturas_nao_liberadas` parcial (`liberada_em IS NULL`); `ix_faturas_em_aberto` parcial (`liberada_em IS NOT NULL AND paga_em IS NULL`) |
| `compras_cartao` | FKs; `ux_compras_cartao_cobranca_id`; `ix_compras_cartao_fatura_id`; `ck_..._valor_positivo`; trigger de imutabilidade |
| `lancamentos_fatura` | FKs; `ux_lancamentos_fatura_compra_id`; `ix_lancamentos_fatura_fatura_id`; `ck_..._valor_positivo`; trigger de imutabilidade |
| `recebiveis` | FKs; `ux_recebiveis_cobranca_id`; `ux_recebiveis_chave` parcial; `ck_recebiveis_valores` (`bruto > 0 AND taxa >= 0 AND liquido = bruto - taxa AND liquido > 0`); `ck_recebiveis_situacao_coerente`; `ix_recebiveis_devidos` parcial (`data_prevista`, `situacao = 'Pendente'`); `ix_recebiveis_lojista` |
| `lancamentos` | + `fatura_id`, `recebivel_id` (FK RESTRICT); `ck_lancamentos_tipo` e `ck_lancamentos_origem_coerente` substituem as atuais; `ux_lancamentos_fatura_id` e `ux_lancamentos_recebivel_id` parciais |
| `notificacoes_auditoria` | `ck_notificacoes_auditoria_tipo` + `comprovante_cobranca`, `cobranca_paga` |

Triggers em `Sql/Procedures/ImpedirAlteracaoDeRegistrosDeCartao.sql` (reutilizam a function
`impedir_alteracao_de_registro_imutavel`) e `RemoverImpedimentoDeRegistrosDeCartao.sql` (Down).
Consultas nativas: `BloquearCobranca.sql`, `BloquearCartao.sql`, `BloquearRecebivel.sql` (R8).

## Identificadores tipados novos

`CartaoId`, `AutorizacaoCartaoId`, `CompraCartaoId`, `FaturaId`, `LancamentoFaturaId`, `CobrancaId`,
`RecebivelId`. `Bandeira` usa chave natural, como `Banco`.
