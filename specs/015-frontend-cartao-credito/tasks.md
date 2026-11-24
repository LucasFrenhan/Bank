---

description: "Lista de tarefas da feature 015-frontend-cartao-credito"
---

# Tasks: Frontend de cartão de crédito, cobranças, faturas e recebíveis

**Input**: Design documents from `specs/015-frontend-cartao-credito/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: obrigatórios (constituição, Princípio VII). Em cada fase os testes vêm antes da implementação e
devem falhar primeiro. Backend: unitários e integração com PostgreSQL em contêiner, com 401, 403 e cada erro do
endpoint novo. Frontend: Vitest + MSW por estado e recusa, e Playwright + axe contra a API do Compose.

**Organization**: a fundação reúne o código curto da cobrança (backend) e o apoio comum das telas. Cada história
acrescenta as suas telas e os seus testes ponta a ponta.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Backend: `back/` = `src/backend/`; `dom/` = `back/src/Bank.Domain/`, `app/` = `back/src/Bank.Application/`,
`infra/` = `back/src/Bank.Infrastructure/`, `api/` = `back/src/Bank.Api/`, `ut/` = `back/tests/Bank.UnitTests/`,
`it/` = `back/tests/Bank.IntegrationTests/`. SQL em `infra/Persistencia/Sql/`, com cabeçalho
`-- Origem: specs/015-frontend-cartao-credito (...)`.
Frontend: `front/` = `src/frontend/`; `src/` = `front/src/`; `e2e/` = `front/e2e/`; `comp/` =
`src/design/componentes/`. Comandos `npm` rodam em `front/`.
O visual de cada tela segue a fonte correspondente em `docs/design/fontes/cartao/`.

---

## Phase 1: Setup

**Purpose**: ponto de partida verificado; não há dependência, serviço ou configuração nova

- [X] T001 Versionar as fontes do protótipo aprovado em `docs/design/fontes/cartao/` e o índice em
  `docs/design/prototipo.md` (commit 42f6a1c)
- [ ] T002 Subir o Compose a partir da branch, emitir algumas cobranças ainda sem código (base para o quickstart,
  cenário 2) e registrar a contagem atual de testes do backend, do frontend e e2e para o SC-008

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: código curto da cobrança na API e apoio comum do frontend

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Código curto — testes

- [ ] T003 [P] `ut/Domain/CodigoCobrancaTests.cs`: `Novo()` com 8 caracteres de
  `23456789ABCDEFGHJKMNPQRSTVWXYZ`; `Criar` aceita minúsculas, espaços e hífen; recusa nulo, vazio, 7, 9 e os
  caracteres 0, 1, I, L, O e U; `Valor` sem hífen; `ToString()` = `XXXX-XXXX`
- [ ] T004 [P] Ampliar os testes de emissão e de consulta em `ut/Application/` (com `BancoCartoes` de
  `ut/Application/DublesCartoes.cs`): código gravado; `CodigoDeCobrancaDuplicado` gera outro código, até 5
  tentativas, e depois falha sem gravar; consulta por código com a visibilidade da consulta por id (emissora,
  outra PJ, PF); malformado e inexistente = `cobranca-nao-encontrada`
- [ ] T005 [P] Ampliar `it/CobrancasTests.cs`: `codigo` logo após `cobrancaId` na emissão, consulta, lista e
  cancelamento (lista exata de campos); `GET /api/v1/cobrancas/codigo/{codigo}` em maiúsculas, minúsculas, sem
  hífen e com espaços = mesma resposta da consulta por id; 401 (sem token e token inválido), 403 (claims
  ausentes), 404 (malformado e inexistente) com `ProblemDetails` e `traceId`; o código continua o mesmo depois de
  cancelar e de pagar (it/PagamentoCobrancaTests.cs)
- [ ] T006 [P] Criar `it/CodigoCobrancaMigracaoTests.cs`, no padrão de `it/DadosBancariosMigracaoTests.cs`:
  migra até `AdicionarCartoesCobrancasERecebiveis`, insere cobranças `Pendente`, `Paga` e `Cancelada`, aplica
  `AdicionarCodigoCobranca` e confere código válido e único em todas, com as demais colunas iguais; ampliar
  `it/RestricoesCartoesTests.cs` com `ux_cobrancas_codigo`, `ck_cobrancas_codigo` e `NOT NULL`
- [ ] T007 [P] Ampliar `it/OpenApiTests.cs` com a rota nova e o campo `codigo` obrigatório

### Código curto — implementação

- [ ] T008 Criar `dom/Cobrancas/CodigoCobranca.cs` (R1; FR-009a) e acrescentar `Codigo`, imutável, a
  `dom/Cobrancas/Cobranca.cs`, recebido em `Emitir`; ajustar `ut/CartoesDeTeste.cs` e as chamadas existentes
- [ ] T009 Em `app/`: `ResultadoGravacao.CodigoDeCobrancaDuplicado` (`Abstracoes/IUnidadeDeTrabalho.cs`),
  `ObterPorCodigoAsync` e `Codigo` em `DadosCobranca` (`Abstracoes/ICobrancaConsultas.cs`), tentativas em
  `Cobrancas/EmitirCobranca.cs` (R1), `Cobrancas/ConsultarCobrancaPorCodigo.cs` e `Codigo` em
  `Cobrancas/DetalheCobranca.cs`; ajustar `ut/Application/DublesCartoes.cs`
- [ ] T010 Em `infra/Persistencia/`: mapear `codigo char(8) NOT NULL` com `ux_cobrancas_codigo` e
  `ck_cobrancas_codigo` (`codigo ~ '^[2-9A-HJKMNP-TV-Z]{8}$'`) em `Configuracoes/CobrancaConfiguracao.cs`;
  traduzir a violação do índice em `UnidadeDeTrabalho.cs`; consulta em `CobrancaConsultas.cs`; incluir `codigo`
  em `Sql/Consultas/BloquearCobranca.sql`
- [ ] T011 Criar `infra/Persistencia/Sql/Migracoes/PreencherCodigosDasCobrancas.sql` (R2) e a migration
  `AdicionarCodigoCobranca` (coluna anulável → script → `NOT NULL`, índice e `CHECK`; `Down` remove tudo);
  ajustar os `INSERT` de cobranças por SQL em `it/RestricoesCartoesTests.cs` e `it/VolumeProcessamentoTests.cs`
- [ ] T012 Em `api/Endpoints/CobrancasEndpoints.cs`: `codigo` em `CobrancaResposta` e
  `GET /cobrancas/codigo/{codigo}` (FR-009b), conforme [contracts/cartoes-cobrancas-api.md](contracts/cartoes-cobrancas-api.md);
  T003–T007 e os testes anteriores do backend passando

### Apoio do frontend — testes

- [ ] T013 [P] `src/cobrancas/codigo.test.ts`: `extrairCodigo` (código, minúsculas, sem hífen, espaços, link
  completo, barra final; inválidos = `null`), `formatarCodigo`, `linkDaCobranca`, `textoDeCompartilhamento`
- [ ] T014 [P] `src/operacoes/operacoes.test.tsx`: chave criada no primeiro envio, mantida após falha de rede,
  422 e 5xx e ao recarregar; descartada em sucesso, `idempotencia-divergente` e operação não mais possível;
  `removerSessao()` apaga `bank.operacoes`; nada além de operação e chave é gravado; segundo clique não envia;
  sem `sessionStorage`, funciona em memória
- [ ] T015 [P] `src/formatacao/dia.test.ts`: `AAAA-MM-DD` e `AAAA-MM` por extenso e curto sem mudar de dia em
  fusos diferentes; hoje em Brasília; ampliar `src/formatacao/valor.test.ts` com a taxa de 2% (R8: 29990 →
  600/29390; 1 → 0/1; 10000 → 200/9800; 25 → 1/24)
- [ ] T016 [P] Ampliar `src/api/erros.test.ts` com cada linha da tradução de erros do
  [contrato do frontend](contracts/frontend.md), incluindo `proximaAcao`, e `src/telemetria/telemetria.test.ts`
  com `/c/:codigo`, `/cartao/faturas/:competencia` e `esperados` sem log de falha (R10)
- [ ] T017 [P] `comp/apoioCartao.test.tsx`: `Filtros` (`aria-pressed`), `ListaPaginada` (páginas no desktop,
  "Carregar mais" no celular, vazio, erro com tentar de novo, página além da última), `SeloSituacao` (situação
  desconhecida mostra o texto da API), `Confirmacao` (foco no título, `Esc` e cancelar devolvem o foco),
  `SomentePerfil` (texto do FR-002 e link para o início)

### Apoio do frontend — implementação

- [ ] T018 Regenerar `src/api/schema.d.ts` (`npm run api:tipos` com `BANK_API_URL`) e acrescentar em
  `src/api/cliente.ts` as 15 chamadas (contrato 1.1.0), com `Idempotency-Key` nos pagamentos e na antecipação, e
  `esperados` em `chamar`
- [ ] T019 [P] Acrescentar os códigos, contextos (`fatura`, `operacao`) e `proximaAcao` em `src/api/erros.ts` e
  exibir a ação em `AvisoErro` (`comp/Feedback.tsx`)
- [ ] T020 [P] Criar `src/cobrancas/codigo.ts`, `src/formatacao/dia.ts`, e a taxa em `src/formatacao/valor.ts` (FR-024)
- [ ] T021 [P] Criar `src/operacoes/chaves.ts` e `src/operacoes/useOperacao.ts` (R6; FR-016) e apagar `bank.operacoes` em
  `removerSessao()` (`src/sessao/armazenamento.ts`)
- [ ] T022 [P] Criar `src/paginas/useConsulta.ts` (carregando/pronto/erro, recarregar, atualizar em silêncio; R7; FR-023)
- [ ] T023 [P] Criar `comp/Filtros.tsx` (extraído de `src/paginas/Extrato.tsx`, que passa a usá-lo, sem mudar
  `Extrato.test.tsx`), `comp/ListaPaginada.tsx`, `comp/SeloSituacao.tsx`, `comp/Confirmacao.tsx` e os ícones de
  olho aberto, olho riscado e cartão em `comp/Icones.tsx`
- [ ] T024 Criar `src/sessao/SomentePerfil.tsx`, trocar identificadores em `padraoDaRota`
  (`src/telemetria/ponte.ts`) e acrescentar em `src/testes/handlers.ts` as respostas da API 1.1.0

**Checkpoint**: API com código curto e testes do backend verdes; apoio do frontend testado sem telas

---

## Phase 3: User Story 1 - Solicitar e ver o cartão (Priority: P1) 🎯 MVP

**Goal**: a PF solicita o cartão, vê limites e exibe ou oculta os dados com o olho

**Independent Test**: PF nova solicita Visa, confere limites, exibe e oculta os dados; nada no armazenamento

### Testes

- [ ] T025 [P] [US1] `src/cartao/useDadosCartao.test.tsx`: oculta por clique, desmontagem, `visibilitychange`,
  `pagehide` e 60 s; resposta que chega depois de ocultar é descartada; erro mantém oculto; storage vazio
- [ ] T026 [P] [US1] `src/paginas/cartao/Cartao.test.tsx`: solicitação (cenários 1 a 3 e 8), cartão e limites
  (4), olho com nome acessível e anúncio (5), copiar só o número (7), nome de 150 caracteres, carregando, erro com
  `traceId` e tentar de novo, PJ (9); campo "Link ou código da cobrança" válido e inválido (FR-009)
- [ ] T027 [P] [US1] `e2e/cartao.spec.ts`: solicitar, exibir, ocultar, trocar de aba e recarregar, contra a API

### Implementação

- [ ] T028 [P] [US1] Criar `src/cartao/useDadosCartao.ts` (R5) e `src/cartao/CartaoDesenhado.tsx` com
  `src/cartao/Cartao.module.css` (R12; leitura dígito a dígito)
- [ ] T029 [US1] Criar `src/paginas/cartao/SolicitarCartao.tsx` (FR-005), `src/paginas/cartao/Cartao.tsx`
  (FR-006, FR-007, FR-009) e a rota `/cartao` em `src/App.tsx`

**Checkpoint**: US1 funciona pelo endereço `/cartao`

---

## Phase 4: User Story 2 - Emitir e acompanhar cobranças (Priority: P1)

**Goal**: a PJ emite, lista, compartilha, acompanha e cancela cobranças

**Independent Test**: PJ emite, copia o link, filtra a lista, abre o detalhe e cancela

### Testes

- [ ] T030 [P] [US2] `src/paginas/cobrancas/Cobrancas.test.tsx`: vazio, filtros no endereço, páginas e "Carregar
  mais", erro, PF vê área indisponível; `NovaCobranca.test.tsx`: valor e descrição (contador de 200), erros por
  campo da API mantendo o digitado, duplo clique emite uma só
- [ ] T031 [P] [US2] `src/paginas/cobrancas/DetalheCobranca.test.tsx`: código, link, copiar e compartilhar (com e
  sem suporte), paga (sem pagador), última recusa, cancelar com confirmação, cancelamento de cobrança já paga
  (10), atualização a cada 5 s só se pendente e visível, anúncio, falha silenciosa e confirmação substituída pelo
  pagamento
- [ ] T032 [P] [US2] `e2e/cobrancas.spec.ts`: emitir, copiar o link, filtrar, abrir e cancelar; outra janela paga
  pela API e o detalhe muda para "Paga" em até 10 s

### Implementação

- [ ] T033 [P] [US2] Criar `src/paginas/cobrancas/useAtualizacaoAutomatica.ts` (R7)
- [ ] T034 [US2] Criar em `src/paginas/cobrancas/` `SecoesCobrancas.tsx` (navegação "Seções de cobranças"),
  `Cobrancas.tsx` (FR-011) e `NovaCobranca.tsx` (FR-010), com `Cobrancas.module.css` e as rotas `/cobrancas` e
  `/cobrancas/nova`
- [ ] T035 [US2] Criar `src/paginas/cobrancas/Cobranca.tsx` (rota `/c/:codigo`, escolhe a visão; FR-003) e
  `DetalheCobranca.tsx` (FR-012)

**Checkpoint**: PJ completa o ciclo da cobrança sem pagamento pela tela

---

## Phase 5: User Story 3 - Pagar uma cobrança com o cartão (Priority: P1)

**Goal**: a PF paga pelo link, com comprovante ou recusa explicada e sem pagamento em dobro

**Independent Test**: pagar com "Usar meu cartão" + CVV; repetir com CVV errado e valor acima do limite

### Testes

- [ ] T036 [P] [US3] `src/cartao/validacao.test.ts`: titular, 16 dígitos e Luhn, validade (formato, mês 13, vence
  no fim do mês de Brasília), CVV; máscaras
- [ ] T037 [P] [US3] `src/paginas/cobrancas/PagarCobranca.test.tsx`: cenários 1, 3 a 8 e 10 a 13; CVV limpo e
  focado após recusa; limite atual na recusa; falha de rede e reenvio com a mesma chave; duplo clique;
  `idempotencia-divergente` com cobrança já paga mostra o comprovante; CVV de "Usar meu cartão" nunca preenchido;
  formulário com `cc-name`, `cc-number`, `cc-exp` e `cc-csc`; sessão expirada descarta os dados
- [ ] T038 [P] [US3] `e2e/pagamento.spec.ts`: link sem sessão → login → cobrança → "Usar meu cartão" → comprovante
  reaberto; CVV errado; limite insuficiente; resposta perdida (interceptada) e novo envio = uma compra; código em
  minúsculas e sem hífen
- [ ] T039 [P] [US3] `e2e/privacidadeCartao.spec.ts`: percorre cartão e pagamento e procura número e CVV em
  `sessionStorage`, `localStorage`, endereço, histórico, título e corpos OTLP; a chave da operação fora do OTLP
  (SC-005, FR-008)

### Implementação

- [ ] T040 [P] [US3] Criar `src/cartao/validacao.ts` (R8; FR-015, data-model "Dados do cartão")
- [ ] T041 [US3] Criar em `src/paginas/cobrancas/` `PagarCobranca.tsx` (FR-013, FR-014, FR-017), `ComprovanteCartao.tsx`
  (FR-018) e `CobrancaSemAcao.tsx` (outra PJ, indisponível e não encontrada), ligados em `Cobranca.tsx`

**Checkpoint**: MVP completo (US1 + US2 + US3)

---

## Phase 6: User Story 4 - Consultar faturas (Priority: P2)

**Goal**: a PF vê as faturas por mês, com lançamentos, e o aviso no início

**Independent Test**: PF com faturas em várias situações confere totais, datas, situações e lançamentos

### Testes

- [ ] T042 [P] [US4] `src/paginas/faturas/Faturas.test.tsx`: lista e detalhe de cada situação (cenários 1 a 6),
  datas sem conversão de fuso, sem cartão, erro; ampliar `src/paginas/Inicio.test.tsx` com o aviso (disponível,
  vencida, mais antiga, sem cartão, erro e PJ) sem mudar os testes atuais
- [ ] T043 [US4] Acrescentar em `e2e/apoio.ts` a emissão, o cartão e o pagamento pela API e a preparação de
  faturas disponível e vencida por SQL (R13); `e2e/faturas.spec.ts`: lista, detalhe com juros e aviso do início

### Implementação

- [ ] T044 [US4] Criar `src/paginas/faturas/Faturas.tsx`, `Fatura.tsx` e `Faturas.module.css` (FR-019), com as
  rotas `/cartao/faturas` e `/cartao/faturas/:competencia`
- [ ] T045 [US4] Criar `src/paginas/faturas/AvisoFatura.tsx` (R11; FR-019a) e exibi-lo em `src/paginas/Inicio.tsx`
  só para PF

---

## Phase 7: User Story 5 - Pagar a fatura (Priority: P2)

**Goal**: a PF paga o total da fatura com o saldo

**Independent Test**: com fatura disponível e saldo, pagar e conferir situação, saldo, extrato e limite

### Testes

- [ ] T046 [P] [US5] `src/paginas/faturas/PagarFatura.test.tsx`: confirmação com valor, juros, saldo e saldo
  depois; saldo menor bloqueia; recusa da API por saldo; falha e reenvio com a mesma chave; duplo clique; paga em
  outra aba; valor pago devolvido diferente do mostrado
- [ ] T047 [US5] Ampliar `e2e/faturas.spec.ts`: pagar disponível e vencida; saldo, extrato e limite atualizados

### Implementação

- [ ] T048 [US5] Criar `src/paginas/faturas/PagarFatura.tsx` (FR-020) e ligá-lo em `Fatura.tsx`

---

## Phase 8: User Story 6 - Receber e antecipar (Priority: P2)

**Goal**: a PJ vê os recebíveis e antecipa um item

**Independent Test**: cobrança paga de R$ 299,90 → taxa R$ 6,00, líquido R$ 293,90 e novo saldo

### Testes

- [ ] T049 [P] [US6] `src/paginas/recebiveis/Recebiveis.test.tsx`: lista, filtros, páginas, vazio, erro, PF;
  confirmação com taxa, líquido, data e aviso; sucesso com os valores da API; sem ação se liquidado, antecipado ou
  previsto para hoje; `recebivel-nao-antecipavel` atualiza a situação; falha e duplo clique
- [ ] T050 [P] [US6] `e2e/recebiveis.spec.ts`: cobrança de R$ 299,90 paga pela API, antecipar e conferir saldo e
  extrato

### Implementação

- [ ] T051 [US6] Criar `src/paginas/recebiveis/Recebiveis.tsx` e `Recebiveis.module.css` (FR-021, FR-022), com a
  rota `/cobrancas/recebiveis`

---

## Phase 9: User Story 7 - Navegar e usar em qualquer tela (Priority: P2)

**Goal**: menu por perfil, perfil atualizado e telas acessíveis de 320 a 1920 px

**Independent Test**: percorrer as telas nas larguras de referência, por teclado e com axe

### Testes

- [ ] T052 [P] [US7] Ampliar `src/App.test.tsx` e `src/paginas/Perfil.test.tsx`: menu e barra por perfil (4 itens),
  item ativo nas rotas filhas, "Em breve" removido, permissões novas
- [ ] T053 [US7] Ampliar `e2e/responsividade.spec.ts` (6 larguras), `e2e/teclado.spec.ts` (solicitar, pagar,
  antecipar, cancelar e pagar fatura) e o axe com todas as telas novas, inclusive os dados exibidos e as
  confirmações abertas (FR-025, SC-007)

### Implementação

- [ ] T054 [US7] Atualizar `comp/Layout.tsx` (menu e barra; FR-001) e `src/sessao/permissoes.ts` (FR-004)
- [ ] T055 [US7] Corrigir o que T053 apontar nas telas novas

---

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T056 [P] Atualizar `docs/desenvolvimento.md` (telas, rotas, código curto, e2e de faturas) e as capturas das
  telas prontas em `docs/design/telas/`, ligadas em `docs/design/prototipo.md`
- [ ] T057 [P] Marcar `specs/014-cartao-credito-cobrancas/contracts/cartoes-cobrancas-api.md` como sucedido pela
  versão 1.1.0 da spec 015
- [ ] T058 Executar `dotnet test`, `npm run lint`, `npm run typecheck`, `npm test`, `npm run build` e
  `npm run e2e`, conferir o SC-008 e o FR-026 com a contagem da T002 e medir SC-001 a SC-003 no Compose
- [ ] T059 Verificação por mutação: quebrar, um de cada vez, o ocultar em 60 s, a chave reaproveitada, o limpar do
  CVV, a visibilidade por código e o `CHECK` do código, e confirmar que algum teste falha
- [ ] T060 Executar o [quickstart](quickstart.md) e atualizar os artefatos da spec que divergirem do código; a
  leitura com NVDA fica para o responsável se não puder ser feita aqui

---

## Dependencies & Execution Order

- **Setup** → **Foundational** (backend T003–T012 antes de T018; testes antes da implementação)
- **US1** e **US2** podem começar em paralelo depois da fundação
- **US3** depende de US1 (`useDadosCartao`) e US2 (`Cobranca.tsx`)
- **US4** depende da fundação e usa o cartão pela API; **US5** depende de US4
- **US6** depende da fundação e de `SecoesCobrancas.tsx` (US2)
- **US7** depois das demais telas; **Polish** por último

`src/App.tsx` é alterado por T029, T034, T035, T044 e T051: em sequência.

### Parallel Opportunities

- Fundação: T003–T007, T013–T017 e T019–T023
- US1: T025–T027; US2: T030–T033; US3: T036–T040; US6: T049–T050

```text
# Exemplo: fundação do frontend
T013 src/cobrancas/codigo.test.ts | T014 src/operacoes/operacoes.test.tsx | T015 src/formatacao/dia.test.ts
```

---

## Implementation Strategy

1. **MVP**: Setup + Foundational + US1 + US2 + US3 (solicitar, cobrar e pagar)
2. **Incrementos**: US4 + US5 (faturas) → US6 (recebíveis) → US7 (menu e acessibilidade) → Polish
3. Um commit por fase, no mínimo; o PR fecha a issue #54
