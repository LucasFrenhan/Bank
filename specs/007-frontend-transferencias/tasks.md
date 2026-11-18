---

description: "Lista de tarefas da feature 007-frontend-transferencias"
---

# Tasks: Frontend de transferências, comprovante e extrato

**Input**: Design documents from `specs/007-frontend-transferencias/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/frontend.md](contracts/frontend.md),
[quickstart.md](quickstart.md)

**Tests**: obrigatórios (FR-030 e constituição, Princípio VII): componentes com Vitest + Testing
Library + MSW e ponta a ponta com Playwright + axe contra a API real do Compose, em desktop e
mobile. Em cada história, os testes são escritos primeiro e devem falhar antes da implementação.

**Organization**: tarefas agrupadas por história de usuário (US1–US6), mais uma fase bloqueada
pela issue #36.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec atendida

## Path Conventions

Frontend em `src/frontend/` (abreviado abaixo como `front/`). Arquivos de raiz (`docs/`,
`.github/`) usam o caminho completo. Comandos `npm` rodam em `src/frontend/` com Node 26.13.0.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: tipos da API atualizados com o contrato da spec 006

- [X] T001 Subir a API da `main` (`docker compose up -d --build --wait`) e regenerar
  `front/src/api/schema.d.ts` com `npm run api:tipos`; conferir que aparecem
  `/api/v1/transferencias`, `/api/v1/transferencias/destinatarios/consultar` e
  `/api/v1/transferencias/extrato` (com `Idempotency-Key` e `direcao`) e que `npm run typecheck`
  continua passando

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: cliente, erros, formatação, armazenamento e componentes usados por todas as
histórias

**⚠️ CRITICAL**: nenhuma história começa antes desta fase

### Testes da fundação (escrever primeiro)

- [X] T002 [P] Criar `front/src/formatacao/valor.test.ts` cobrindo a tabela "Interpretação de
  valor" do data-model: `45,90`/`45.90`/`R$ 45,90` → 4590; `1.234,56`/`1234,56` → 123456;
  `1.234` → 123400; `10,5` → 1050; `0`/`0,00` → "Informe um valor a partir de R$ 0,01";
  `10,005` → "Use no máximo 2 casas decimais"; vazio → "Informe o valor"; e `saldoEmCentavos`
  para `842.5`, `'842.50'` e `0`; e `centavosParaNumero(4590) === 45.9`
- [X] T003 [P] Acrescentar a `front/src/formatacao/mascaras.test.ts`: `tipoDocumento` (com letra
  ou mais de 11 caracteres → CNPJ; senão CPF), `mascararDocumentoDigitado` (CPF progressivo e
  CNPJ em maiúsculas), `normalizarDocumento` (só `[0-9A-Z]`) e `cpfValido` (DV correto,
  incorreto e todos os dígitos iguais)
- [X] T004 [P] Acrescentar a `front/src/formatacao/data.test.ts` (fuso fixo
  `America/Sao_Paulo` e `agora` injetado): `formatarDataRelativa` → `Hoje, 14:32`,
  `Ontem, 19:47`, `15 nov, 11:05`, e ano quando diferente do atual; `formatarDataCurta` →
  `17 nov, 14:32`; `rotuloDia` → `Hoje`, `Ontem`, `15 de novembro` (ano quando diferente)
- [X] T005 [P] Acrescentar a `front/src/api/erros.test.ts` os códigos da tabela "Tradução de
  erros (acréscimos)" do data-model: `destinatario-nao-encontrado` (campo `documento`),
  `autotransferencia`, `saldo-insuficiente`, `idempotencia-divergente`,
  `transferencia-nao-encontrada`, com título, texto e variante (`erro-campo`, `atencao`,
  `informativo`, `neutro`); `acesso-negado` com contexto `'transferencia'` → "Indisponível para
  Pessoa Jurídica"; e que nenhum usa `detail`
- [X] T006 [P] Criar `front/src/transferencia/transferencia.test.ts`: `gerarChave` devolve UUID
  v4 (também sem `crypto.randomUUID`); `assinatura` = `documento|valorCentavos|descricao`;
  `prepararConfirmacao` mantém a chave com a mesma assinatura e gera outra se documento, valor ou
  descrição mudarem; leitura/gravação de `bank.transferencia` e `bank.comprovantes` (máximo 20,
  mais recentes) com `sessionStorage` indisponível sem lançar erro; `removerSessao()` apaga as
  duas chaves
- [X] T007 [P] Acrescentar a `front/src/testes/handlers.ts` handlers MSW para
  `POST /api/v1/transferencias/destinatarios/consultar`, `POST /api/v1/transferencias` e
  `GET /api/v1/transferencias/extrato`, com respostas padrão do contrato da spec 006 e
  utilitários para sobrescrever status (200, 201, 400, 403, 404, 409, 422 `autotransferencia`,
  422 `saldo-insuficiente`, 500, falha de rede)

### Implementação da fundação

- [X] T008 Criar `front/src/formatacao/valor.ts` com `interpretarValor(texto)` →
  `{ ok: true; centavos } | { ok: false; erro }` (regras de research R3: com vírgula, pontos são
  milhar; sem vírgula, um ponto seguido de 1–2 dígitos no fim é decimal; mais de 2 casas → erro,
  sem arredondar), `saldoEmCentavos(number | string)` (via texto com 2 casas, nunca
  multiplicação de float), `centavosParaNumero` e `formatarCentavos` (usa `formatarMoeda`)
- [X] T009 [P] Acrescentar a `front/src/formatacao/mascaras.ts`: `normalizarDocumento`,
  `tipoDocumento`, `mascararDocumentoDigitado` (reusa `mascararCpf`/`mascararCnpj`) e `cpfValido`;
  manter `ocultarDocumento` para os documentos fora do campo (FR-021)
- [X] T010 [P] Acrescentar a `front/src/formatacao/data.ts`: `formatarDataRelativa`,
  `formatarDataCurta`, `rotuloDia` e `chaveDia` (AAAA-MM-DD no fuso do navegador), todos com
  `agora` opcional para testes
- [X] T011 Estender `front/src/api/erros.ts`: `MensagemErro` ganha `titulo?` e
  `variante?: 'erro-campo' | 'atencao' | 'informativo' | 'neutro' | 'erro'`; novos textos em
  `MENSAGENS` exatamente como no data-model; `traduzirErro(status, corpo, contexto?)` com
  contexto `'transferencia'` para `acesso-negado`; mapear `destinatario-nao-encontrado` para
  `campos.documento`
- [X] T012 Estender `front/src/api/cliente.ts`: tipos `Destinatario`, `Comprovante`,
  `ExtratoPagina`, `ItemExtrato`; `api.consultarDestinatario(documento, sinal?)`,
  `api.transferir({ documentoDestino, valor, descricao? }, chave)` (cabeçalho `Idempotency-Key`;
  devolve `{ comprovante, repetida: response.status === 200 }`) e
  `api.obterExtrato({ pagina?, tamanhoPagina?, direcao? })`; erros traduzidos com contexto
  `'transferencia'` nas duas chamadas de transferência
- [X] T013 Criar `front/src/transferencia/chave.ts` (`gerarChave` com `crypto.randomUUID` ou
  `crypto.getRandomValues`; `assinatura`) e `front/src/transferencia/armazenamento.ts`
  (`lerTransferencia`, `salvarTransferencia`, `limparTransferencia`, `guardarComprovante`,
  `lerComprovante`, máximo 20 comprovantes), todos com `try/catch` no `sessionStorage`
- [X] T014 Alterar `front/src/sessao/armazenamento.ts` para `removerSessao()` também apagar
  `bank.transferencia` e `bank.comprovantes` (FR-010)
- [X] T015 [P] Acrescentar a `front/src/design/componentes/Feedback.tsx` o componente `Aviso` com
  as variantes `atencao` (ícone de alerta, cor `#B54708`), `informativo` (ícone "i", primária) e
  `neutro` (cadeado), título + texto + ação opcional (link), `role="alert"` para atenção e
  `role="status"` para informativo; e reaproveitar `EstadoVazio` com "Nenhuma movimentação ainda —
  Quando você enviar ou receber uma transferência, ela aparece aqui."
- [X] T016 [P] Criar `front/src/design/componentes/ListaLancamentos.tsx` (+ estilos em
  `componentes.module.css`): item com ícone de direção (seta para baixo = entrada, para cima =
  saída), nome, linha secundária, valor com "+"/"−" e cor de entrada; link para
  `/transferencias/{id}` quando houver `transferenciaId`; texto alternativo da direção para
  leitores de tela (entrada/saída não só pela cor)
- [X] T017 [P] Criar `front/src/paginas/useLarguraDesktop.ts` (`matchMedia('(min-width:
  1024px)')` com assinatura de mudanças) e `front/src/paginas/useExtrato.ts` (lê
  `api.obterExtrato` a cada exibição, estados carregando/pronto/erro, `recarregar`, trata 401
  como `useCarteira`)
- [X] T018 Alterar `front/src/telemetria/ponte.ts` para registrar a navegação pelo **padrão** da
  rota (`/transferencias/:id`), sem identificador nem query string; acrescentar caso em
  `front/src/telemetria/telemetria.test.ts`

**Checkpoint**: `npm test` e `npm run typecheck` verdes; fundação pronta

---

## Phase 3: User Story 1 - Transferir dinheiro em três etapas (Priority: P1) 🎯 MVP

**Goal**: PF faz Dados → Confirmação → Comprovante e vê o saldo atualizado

**Independent Test**: com PF com saldo e PJ com CNPJ alfanumérico, percorrer o fluxo em desktop e
mobile e conferir comprovante e saldos (quickstart, cenário 3)

### Tests for User Story 1

- [X] T019 [P] [US1] Criar `front/src/paginas/Transferir.test.tsx`: título "Transferir dinheiro"
  e etapas; máscara CPF/CNPJ (CNPJ minúsculo vira maiúsculo); consulta só com CNPJ de 14
  caracteres ou CPF com DV válido após 500 ms/ao sair do campo; ao sair do campo com CPF de DV
  inválido, consulta mesmo assim e mostra o 400 da API no campo; "Buscando titular…" durante a
  consulta; "Continuar" com documento incompleto → "Informe o CPF ou CNPJ completo"; cartão com
  iniciais, nome, tipo e documento mascarado; alterar o documento descarta o titular; resposta
  atrasada de consulta anterior é ignorada; texto "Disponível: R$ … · mínimo R$ 0,01"; contador "N/200"; "Continuar"
  indisponível sem titular confirmado; dados restaurados de `bank.transferencia`
- [X] T020 [P] [US1] Criar `front/src/paginas/ConfirmarTransferencia.test.tsx`: "Confira antes
  de enviar", valor em destaque, linhas Para/Documento/Tipo de conta/Descrição (omitida se vazia)/
  Saldo depois (em centavos: 842,50 − 45,90 = R$ 796,60); descrição só com espaços não aparece e
  não é enviada; aviso de que não pode ser desfeita;
  "Voltar"/"Editar dados" voltam com tudo preenchido; botão "Transferir R$ 45,90" envia com
  `Idempotency-Key` e leva a `/transferencias/{id}`; sem transferência em andamento →
  `/transferir`
- [X] T021 [P] [US1] Criar `front/src/paginas/Comprovante.test.tsx`: a partir do cache da aba,
  "Transferência concluída", "R$ 45,90 enviados para {nome}", linhas Valor, De (nome · CPF
  mascarado de `/usuarios/me`, só quando a sessão é o pagador), Para (nome · documento mascarado), Descrição, Data, Status
  "Concluída", ID monoespaçado; "Voltar ao início"; "Compartilhar" usa `navigator.share` e, sem
  ele, copia o texto (sem chave de idempotência) e anuncia "Comprovante copiado"
- [X] T022 [P] [US1] Criar `front/e2e/transferencia.spec.ts` (caminho feliz) e acrescentar a
  `front/e2e/apoio.ts` `transferirPelaApi(page, token, documento, valor)` e
  `acumularSaldo(page, conta, contasDeApoio)`: PF→PJ (CNPJ alfanumérico digitado em minúsculas) e
  PF→PF, comprovante, saldo reduzido no início e transferência em "Últimas movimentações", nos
  projetos desktop e mobile; o cartão do titular aparece em até 2 s após o documento completo
  (SC-004)

### Implementation for User Story 1

- [X] T023 [P] [US1] Criar `front/src/design/componentes/Etapas.tsx` (Dados → Confirmar →
  Comprovante; etapa atual com `aria-current="step"`, concluídas com ✓),
  `front/src/design/componentes/LayoutEtapas.tsx` (sem menu; variante `etapas` com link de volta
  — "Voltar ao início"/"Editar dados" — e `Etapas`; variante `marca` só com a marca, para o
  comprovante; `main#conteudo` e "Pular para o conteúdo" como no `Layout`) e
  `front/src/design/componentes/CampoValor.tsx` (prefixo "R$", `inputMode="decimal"`, texto de
  ajuda e erro associados por `aria-describedby`)
- [X] T024 [US1] Criar `front/src/transferencia/useTransferencia.ts`: estado da transferência em
  andamento (data-model), consulta do destinatário com sequência + `AbortController` (R6),
  validação local de valor (`valorCentavos` ≥ 1) e descrição ("≤ 200"), `prepararConfirmacao`
  (chave por assinatura, R2), `enviar()` com trava contra envio simultâneo e gravação do
  comprovante no cache
- [X] T025 [US1] Criar `front/src/paginas/Transferir.tsx` (etapa Dados, layout do protótipo
  `desktop-06`/`mobile-06`; "Voltar ao início"; restaura a transferência em andamento; após uma
  concluída, começa vazia)
- [X] T026 [US1] Criar `front/src/paginas/ConfirmarTransferencia.tsx` (layout `desktop-07`/
  `mobile-07`; "Saldo depois" a partir de `useCarteira` em centavos; envio e navegação para o
  comprovante)
- [X] T027 [US1] Criar `front/src/paginas/Comprovante.tsx` (layout `desktop-08`/`mobile-08`; lê o
  cache da aba por `:transferenciaId`; sem comprovante → "Comprovante não encontrado" com link
  "Ir para o extrato"; "Compartilhar" conforme research R10)
- [X] T028 [US1] Registrar em `front/src/App.tsx` as rotas protegidas `/transferir`,
  `/transferir/confirmar` e `/transferencias/:transferenciaId`, envolvidas por `LayoutEtapas`
  (variante `etapas` nas duas primeiras e `marca` no comprovante), conforme o protótipo
- [X] T029 [US1] Em `front/src/paginas/Transferir.tsx` e `ConfirmarTransferencia.tsx`, mostrar
  para PJ (claim `tipo_conta`) o aviso neutro "Indisponível para Pessoa Jurídica — Contas PJ
  recebem transferências e geram cobranças, mas não enviam." com link para o início, sem
  formulário (FR-001)

**Checkpoint**: US1 funcional e testada; transferência completa em desktop e mobile

---

## Phase 4: User Story 2 - Entender e corrigir uma transferência recusada (Priority: P1)

**Goal**: cada recusa mostra a mensagem do protótipo no lugar certo, sem perder dados

**Independent Test**: provocar cada recusa (quickstart, cenário 4)

### Tests for User Story 2

- [X] T030 [P] [US2] Acrescentar a `Transferir.test.tsx` e `ConfirmarTransferencia.test.tsx`:
  404 → erro no campo "Nenhuma conta Bank com esse documento."; 422 `autotransferencia` → aviso
  "Esse documento é o seu" (na consulta e no envio); 400 por campo (`documento`,
  `documentoDestino`, `valor`, `descricao`) no campo certo; valor acima do saldo → aviso "Saldo
  insuficiente — Você tem R$ {saldo} disponíveis. Ajuste o valor para continuar." antes do
  envio; 422 `saldo-insuficiente` no envio recarrega a carteira e mostra o saldo atual; 409 →
  "Essa transferência mudou" e o próximo envio usa outra chave; 403 → "Indisponível para Pessoa
  Jurídica"; rede/500 → mensagem de conexão ou "Algo deu errado" com `traceId`, dados preservados
- [X] T031 [P] [US2] Acrescentar a `front/e2e/transferencia.spec.ts`: CPF sem conta, próprio
  documento, valor acima do saldo, `10,005`, PJ abrindo `/transferir` (desktop e mobile)

### Implementation for User Story 2

- [X] T032 [US2] Em `front/src/transferencia/useTransferencia.ts`, tratar cada `codigo` de erro
  (consulta e envio) conforme a tabela do data-model, mantendo dados e chave; em 409, zerar a
  chave; em `saldo-insuficiente`, chamar `recarregar` da carteira; erros de campo da API mapeados
  `documentoDestino` → campo do documento
- [X] T033 [US2] Exibir em `front/src/paginas/Transferir.tsx` e
  `ConfirmarTransferencia.tsx` os avisos (`Aviso`) e erros de campo, com foco movido para o
  primeiro erro ou aviso após cada recusa

**Checkpoint**: US1 + US2 com todas as recusas do contrato

---

## Phase 5: User Story 3 - Reenviar sem pagar duas vezes (Priority: P1)

**Goal**: reenvios usam a mesma chave e mostram "Essa transferência já foi feita"

**Independent Test**: quickstart, cenário 5 — um único débito no extrato

### Tests for User Story 3

- [X] T034 [P] [US3] Acrescentar a `ConfirmarTransferencia.test.tsx`: duplo clique → um único
  POST; falha de rede e nova tentativa → mesma `Idempotency-Key`; resposta 200 → aviso
  informativo "Essa transferência já foi feita — Nada foi cobrado de novo." com "Ver
  comprovante" para `/transferencias/{id}`; recarregar a confirmação mantém a chave; editar
  valor/descrição/documento → nova chave; nova transferência após concluída → formulário vazio e
  nova chave
- [X] T035 [P] [US3] Acrescentar a `front/e2e/transferencia.spec.ts`: concluir, voltar pelo
  navegador até a confirmação e enviar de novo → aviso de reenvio e saldo inalterado; recarregar
  a confirmação e enviar; duplo clique numa nova → um lançamento no extrato (conferido pela API)

### Implementation for User Story 3

- [X] T036 [US3] Em `front/src/transferencia/useTransferencia.ts` e
  `front/src/paginas/ConfirmarTransferencia.tsx`, tratar `repetida: true` (marcar `concluida`,
  guardar comprovante, exibir o aviso com link em vez de navegar), manter a chave ao recarregar e
  após falha de rede, e limpar a transferência em andamento ao abrir `/transferir` depois de uma
  concluída

**Checkpoint**: todas as histórias P1 completas — **MVP**

---

## Phase 6: User Story 4 - Consultar o extrato (Priority: P2)

**Goal**: PF e PJ veem lançamentos com filtros e paginação, em tabela (desktop) e lista
agrupada (mobile)

**Independent Test**: quickstart, cenário 6 (itens 1–2)

### Tests for User Story 4

- [X] T037 [P] [US4] Criar `front/src/paginas/Extrato.test.tsx`: título, saldo atual e itens na
  ordem da API; "Saldo inicial" com "Saldo inicial de demonstração" e sem link; "Sem descrição";
  status "Concluída"; filtros alteram `?direcao=entradas|saidas`, voltam à página 1 e chamam a
  API com `Entrada`/`Saida`; desktop: "Página X de Y · 20 por página", "Anterior"/"Próxima"
  desabilitados nas pontas, página além da última → substitui pela última válida; mobile: grupos
  `Hoje`/`Ontem`/`15 de novembro`, "Carregar mais" acrescenta sem duplicar (`lancamentoId`) e
  some no fim, `pagina=N` inicial pede `tamanhoPagina = 20·N` (até 100); vazio, carregando e erro
  com "Tentar de novo"; parâmetros inválidos na URL → padrão
- [X] T038 [P] [US4] Criar `front/e2e/extrato.spec.ts` e acrescentar a `apoio.ts`
  `criarLancamentos(page, conta, n)` (transferências de R$ 0,01 pela API): PF recém-criada com
  "Saldo inicial"; filtros; 21+ lançamentos com paginação no desktop e "Carregar mais" no
  mobile; recarregar mantém filtro e página; primeira página visível em até 2 s (SC-004)

### Implementation for User Story 4

- [X] T039 [US4] Criar `front/src/paginas/Extrato.tsx`: lê/escreve `direcao` e `pagina` na URL
  (data-model "Filtro do extrato"); saldo de `useCarteira`; filtros como grupo de botões
  alternáveis (`aria-pressed`); desktop em tabela com cabeçalhos Data, Contraparte, Descrição,
  Status, Valor (layout `desktop-05`); mobile em `ListaLancamentos` agrupada por `rotuloDia` com
  "Carregar mais" (layout `mobile-05`); itens de transferência levam a
  `/transferencias/{transferenciaId}` **somente quando o comprovante está no cache da aba**
  (`lerComprovante`) — a Fase 10 libera o link para todos; estilos em
  `front/src/paginas/Extrato.module.css`
- [X] T040 [US4] Registrar a rota protegida `/extrato` em `front/src/App.tsx`

**Checkpoint**: extrato completo nos dois layouts

---

## Phase 7: User Story 5 - Ver movimentações reais na tela inicial (Priority: P2)

**Goal**: início PF com ações ativas e últimas movimentações; início PJ com últimos
recebimentos; navegação ativa

**Independent Test**: quickstart, cenário 6 (item 3) e cenário 3 (item 1)

### Tests for User Story 5

- [X] T041 [P] [US5] Atualizar `front/src/paginas/Inicio.test.tsx`: PF com "Transferir" e "Ver
  extrato" como links (sem "Em breve"); "Últimas movimentações" com 5 itens
  (`tamanhoPagina=5`), "Transferência recebida/enviada · Hoje, 14:32" e "Ver tudo" → `/extrato`;
  PJ com "Últimos recebimentos" (`direcao=Entrada`), "{descrição ou Sem descrição} · data" e "Ver
  tudo" → `/extrato?direcao=entradas`; vazio "Nenhuma movimentação ainda"; sem "Entradas no
  mês"/"Saídas no mês"
- [X] T042 [P] [US5] Atualizar `front/e2e/inicio.spec.ts` (remover expectativas de "Em breve"
  para Transferir/Extrato) e acrescentar: navegação PF (desktop: Início, Transferir, Extrato,
  Perfil; mobile: Início, Extrato, Perfil) e PJ (Recebimentos → extrato filtrado; Cobranças "Em
  breve"); listas reais após transferências

### Implementation for User Story 5

- [X] T043 [US5] Alterar `front/src/design/componentes/Layout.tsx`: `ITENS_NAVEGACAO` com
  `para` em Transferir (`/transferir`), Extrato (`/extrato`) e Recebimentos
  (`/extrato?direcao=entradas`); item opcional `somenteDesktop` para esconder "Transferir" da
  barra inferior da PF; ativo correto para `/transferir/confirmar`
- [X] T044 [US5] Alterar `front/src/paginas/Inicio.tsx`: substituir `AcaoEmBreve` por links
  "Transferir" e "Ver extrato" no cartão de saldo; "Últimas movimentações" (PF) e "Últimos
  recebimentos" (PJ) com `useExtrato` + `ListaLancamentos` + "Ver tudo"; remover `AcaoEmBreve`
  de `front/src/design/componentes/Botao.tsx` se ficar sem uso

**Checkpoint**: todas as histórias funcionais

---

## Phase 8: User Story 6 - Mesma experiência no computador e no celular (Priority: P2)

**Goal**: layouts do protótipo, sem rolagem horizontal, WCAG 2.2 AA

**Independent Test**: quickstart, cenário 8

- [X] T045 [P] [US6] Acrescentar `/transferir`, `/transferir/confirmar`, o comprovante e
  `/extrato` a `front/e2e/responsividade.spec.ts` (320, 390, 768, 1440, 1920 px, sem rolagem
  horizontal) e a verificação axe `semViolacoesA11y` em cada tela e estado de aviso
- [X] T046 [P] [US6] Acrescentar a `front/e2e/teclado.spec.ts`: transferência completa e uso
  dos filtros/paginação do extrato só com teclado, com foco visível; etapa atual, titular
  encontrado, avisos e conclusão anunciados (regiões `aria-live`/`role` verificadas)
- [X] T047 [US6] Ajustar `front/src/paginas/Transferencia.module.css`,
  `front/src/paginas/Extrato.module.css`, `front/src/paginas/Autenticado.module.css` e
  `front/src/design/componentes/componentes.module.css` às medidas do protótipo: conteúdo das
  etapas com até 560 px centralizado; botões principais 48 px (desktop) e 52 px (mobile); alvos
  ≥ 44 px; raios 10/16 px; valores com `tabular-nums`; nomes longos quebrando linha; teclado
  `numeric` para CPF e `text` quando o CNPJ tem letra; até T045–T046 passarem

**Checkpoint**: US6 verificada nos dois projetos do Playwright

---

## Phase 9: Polish & Cross-Cutting Concerns

- [ ] T048 [P] Acrescentar a `front/e2e/telemetria.spec.ts`: após uma transferência, os sinais
  enviados a `/otlp` não contêm documento, nome, valor, descrição nem chave de idempotência, e a
  navegação aparece como `/transferencias/:id` (SC-007)
- [ ] T049 [P] Atualizar `docs/desenvolvimento.md` (seção Frontend): telas novas, rotas,
  preparo de contas e saldo para testes locais (FR-031)
- [ ] T050 Executar `npm run lint && npm run typecheck && npm test && npm run build && npm run
  e2e` e o roteiro de [quickstart.md](quickstart.md) cenários 1–6 e 8; registrar desvios na spec,
  no plano ou no contrato no mesmo PR (constituição, Princípio I)

---

## Phase 10: Bloqueada pela issue #36 — reabrir o comprovante

**Purpose**: FR-014 e US4 cenário 9 após o merge de `GET /api/v1/transferencias/{id}`

- [ ] T051 Regenerar `front/src/api/schema.d.ts` (`npm run api:tipos`) e acrescentar
  `api.obterTransferencia(id)` a `front/src/api/cliente.ts` (404 →
  `transferencia-nao-encontrada`) e o handler MSW em `front/src/testes/handlers.ts`
- [ ] T052 [P] [US1] Acrescentar a `front/src/paginas/Comprovante.test.tsx`: sem cache, carrega pela
  API; sessão como recebedor → "Transferência recebida"; 404 → "Comprovante não encontrado" com
  "Ir para o extrato"; carregando e erro com `traceId`
- [ ] T053 [US1] Alterar `front/src/paginas/Comprovante.tsx` para buscar pela API quando o cache
  da aba não tiver o comprovante, distinguir pagador/recebedor pelo `usuarioId` da sessão e obter
  o CPF do pagador só quando a sessão for o pagador; em `front/src/paginas/Extrato.tsx` e
  `Inicio.tsx`, liberar o link para o comprovante em todo lançamento de transferência
- [ ] T054 [P] [US4] Criar `front/e2e/comprovante.spec.ts`: abrir pelo extrato; abrir o endereço após
  sair e entrar de novo; recebedor PJ; terceiro → "Comprovante não encontrado" (quickstart,
  cenário 7)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (1)** → **Foundational (2)** → histórias
- **US1 (3)** → **US2 (4)** e **US3 (5)** (estendem as mesmas telas e o mesmo hook)
- **US4 (6)** depende só da fundação; pode correr em paralelo com US1–US3
- **US5 (7)** depende da fundação (`useExtrato`, `ListaLancamentos`); os links "Ver tudo" e
  "Transferir" apontam para rotas das fases 3 e 6
- **US6 (8)** e **Polish (9)** depois das histórias
- **Fase 10** só após o merge da issue #36

### Within Each User Story

- Testes primeiro, falhando; depois hook/estado, telas e rotas

### Parallel Opportunities

- T002–T007 (testes da fundação) em paralelo; T009, T010, T015, T016, T017 em paralelo
- Testes de cada história ([P]) em paralelo entre si
- Fase 6 (Extrato) em paralelo com as fases 3–5 por outra pessoa

---

## Parallel Example: User Story 1

```text
T019 Transferir.test.tsx    T020 ConfirmarTransferencia.test.tsx
T021 Comprovante.test.tsx   T022 e2e/transferencia.spec.ts
T023 Etapas.tsx + CampoValor.tsx
```

## Parallel Example: User Story 4 + User Story 5

```text
T037 Extrato.test.tsx       T041 Inicio.test.tsx
T038 e2e/extrato.spec.ts    T042 e2e/inicio.spec.ts
```

---

## Implementation Strategy

### MVP First

1. Fases 1 e 2
2. US1 + US2 + US3 → transferência segura de ponta a ponta
3. **PARAR e VALIDAR** em desktop e mobile (quickstart, cenários 3–5)

### Incremental Delivery

1. US4 → extrato
2. US5 → início e navegação ativos
3. US6 → responsividade e acessibilidade verificadas
4. Polish → telemetria, documentação, quickstart
5. Fase 10 → após a issue #36 (pode ir em PR separado)

### Commits

Conventional Commits por fase, escopo `frontend-transferencias` (por exemplo
`feat(frontend-transferencias): ...`, `test(frontend-transferencias): ...`); PR com `Closes #32`
apenas quando a Fase 10 estiver concluída — antes disso, `Refs #32`.
