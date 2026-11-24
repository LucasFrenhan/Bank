# Research: Frontend de cartão de crédito, cobranças, faturas e recebíveis

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

## R1 — Código curto da cobrança (backend)

- **Decision**: value object `CodigoCobranca` (Domain): 8 caracteres do alfabeto
  `23456789ABCDEFGHJKMNPQRSTVWXYZ` (30 símbolos, sem 0, 1, I, L, O e U), gerados por
  `RandomNumberGenerator.GetString`. `Criar(texto)` remove hífens e espaços e passa a maiúsculas;
  `ToString()` dá `XXXX-XXXX`. Gravado sem hífen em `cobrancas.codigo char(8)`, com
  `ux_cobrancas_codigo` e `ck_cobrancas_codigo`. `EmitirCobranca` tenta até 5 códigos se o índice acusar
  colisão (`ResultadoGravacao.CodigoDeCobrancaDuplicado`); esgotadas, lança exceção como `SolicitarCartao` (500
  sem detalhes), nada fica gravado, e métrica e log só são registrados no sucesso.
- **Rationale**: 30⁸ ≈ 6,6 × 10¹¹ combinações; colisão e adivinhação são improváveis, e a consulta exige
  autenticação. Alfabeto fácil de ditar; sem vogal U, evita parte das palavras.
- **Alternatives considered**: sequencial em base 30 (adivinhável e revela volume); derivar do UUID (continua
  longo ou colide); encurtador externo (dependência e exposição dos links a terceiros).

## R2 — Código das cobranças existentes

- **Decision**: a migration `AdicionarCodigoCobranca` cria a coluna anulável, executa
  `Sql/Migracoes/PreencherCodigosDasCobrancas.sql` e só então a torna obrigatória, com índice e `CHECK`. O
  script sorteia o código de cada cobrança a partir dos bytes aleatórios de `gen_random_uuid()` e repete se já
  existir.
- **Rationale**: mesmo procedimento da spec 012 (R7); `gen_random_uuid()` usa o gerador criptográfico do
  PostgreSQL, sem extensão nova.

## R3 — Consulta pelo código

- **Decision**: `GET /api/v1/cobrancas/codigo/{codigo}`, com a mesma resposta e visibilidade de
  `GET /cobrancas/{cobrancaId}`. Código malformado ou inexistente: 404 `cobranca-nao-encontrada`.
  `CobrancaResposta` ganha `codigo` (`XXXX-XXXX`). As demais rotas não mudam: a tela consulta pelo código e
  usa o `cobrancaId` para pagar e cancelar. Contrato 1.1.0, aditivo.
- **404 para malformado**: justificado no plano, "Complexity Tracking".
- **Alternatives considered**: aceitar código em `/{cobrancaId}` (mistura dois formatos e muda o 400 atual);
  pagar pelo código (duplicaria rotas de escrita).

## R4 — Rotas e navegação

- **Decision**: `/cartao`, `/cartao/faturas`, `/cartao/faturas/:competencia` (PF); `/cobrancas`,
  `/cobrancas/nova`, `/cobrancas/recebiveis` (PJ); `/c/:codigo` (todos). `/cartao` mostra a solicitação
  quando não há cartão. `SomentePerfil` mostra "Área indisponível para o seu tipo de conta". Os itens "Cartão"
  e "Cobranças" ficam ativos também nas rotas filhas. Em `/c/:codigo`, o tipo de conta e o emissor decidem a
  tela: detalhe (emissora), pagamento ou comprovante (PF) ou dados sem ação (outra PJ). A emissora é quem tem
  `lojista.usuarioId` igual ao da sessão. Até a US3, a PF vê só os dados.
- **Rationale**: endereço único da cobrança (FR-003); `RotaProtegida` já devolve à rota pedida após o login.

## R5 — Dados do cartão só em memória

- **Decision**: `useDadosCartao` busca `GET /cartoes/meu/dados` ao clicar no olho e guarda o resultado só no
  estado do componente. Oculta por clique, desmontagem, `visibilitychange` para oculto, `pagehide` (cache de
  volta do navegador) e temporizador de 60 s; resposta que chega depois de ocultar é descartada. "Usar meu
  cartão" lê nome, número e validade e descarta o CVV na hora. Nada vai para `sessionStorage`, endereço,
  título, log ou telemetria; o formulário vive no estado do componente e é limpo em `pagehide`, para não
  reaparecer pelo cache de volta do navegador.
- **Campos**: `cc-name`, `cc-number`, `cc-exp` e `cc-csc` em `autocomplete`, como exige o WCAG 1.3.5. Guardar
  o cartão no navegador é escolha da pessoa e nunca é feito pelo One Bank; navegadores não guardam o
  `cc-csc`.
- **Alternatives considered**: `autocomplete="off"` (viola 1.3.5 AA e é ignorado por vários navegadores).

## R6 — Chave de idempotência por operação

- **Decision**: `src/operacoes/chaves.ts` guarda em `sessionStorage` (`bank.operacoes`) uma chave por operação
  (`cobranca:{codigo}`, `fatura:{competencia}`, `recebivel:{id}`), sem dados do cartão nem assinatura. A
  chave nasce no primeiro envio, é reaproveitada até o sucesso e descartada no sucesso, em
  `idempotencia-divergente` e quando a operação não é mais possível; some com a sessão. `useOperacao` bloqueia
  o segundo clique com uma referência síncrona.
- **Rationale**: a API não consome a chave em recusas (spec 014, FR-019) e, com a mesma chave e outros dados,
  só responde `idempotencia-divergente` se o primeiro pagamento foi aprovado; a tela recarrega
  a cobrança e, se já paga pela pessoa, mostra o comprovante; senão, pede para conferir e usa nova chave.
  Guardar um resumo do cartão para comparar seria guardar
  dado do cartão (FR-008).

## R7 — Leitura e listas

- **Decision**: `useConsulta(chave, buscar)` (carregando/pronto/erro, recarregar, atualizar em silêncio) para as
  telas novas, e `ListaPaginada` (desktop por página, mobile com "Carregar mais", filtro e página no endereço)
  para cobranças e recebíveis. `Filtros` é extraído de `Extrato.tsx`, que passa a usá-lo sem mudança visual. Os
  hooks atuais não mudam.
- **Detalhe da cobrança**: `useAtualizacaoAutomatica` chama a atualização em silêncio a cada 5 s só com status
  `Pendente` e aba visível; falhas são ignoradas e não viram log (R10).

## R8 — Dinheiro e datas

- **Decision**: centavos inteiros (`formatacao/valor.ts`). Taxa mostrada antes de antecipar:
  `floor((centavos × 2 + 50) / 100)`. `formatacao/dia.ts` formata `AAAA-MM-DD` e `AAAA-MM` pelas partes do
  texto, sem `Date` (FR-024). "Hoje" para a validade do cartão usa o fuso `America/Sao_Paulo` por `Intl`.
- **Vetores**: 29990 → 600 / 29390; 1 → 0 / 1; 10000 → 200 / 9800; 25 → 1 / 24.

## R9 — Erros

- **Decision**: `traduzirErro` ganha os 11 códigos da spec 014, com título, orientação, variante e
  `proximaAcao` (`rotulo`, `para`), exibida por `AvisoErro`. `saldo-insuficiente` e `idempotencia-divergente`
  ganham textos por contexto (`fatura`, `operacao`). `acesso-negado` nas rotas novas usa
  "Área indisponível" (contexto `area`). Todo erro de negócio novo tem próxima ação: link (`para`) ou ação na própria
  tela. Emitir cobrança não tem chave na API: em falha de rede, a orientação é conferir a lista antes de
  reenviar. Limite insuficiente consulta o limite atual antes de exibir.

## R10 — Telemetria

- **Decision**: `padraoDaRota` troca `/c/{codigo}` por `/c/:codigo` e `/cartao/faturas/{competencia}` por
  `/cartao/faturas/:competencia`. Nenhum sinal novo. `chamar` aceita `esperados` (códigos que não geram log de
  falha): 404 `cartao-nao-encontrado` no início e na tela do cartão, e falhas da atualização automática. O
  código da cobrança e o `cobrancaId` podem aparecer no caminho das chamadas (não são dados pessoais nem de
  cartão); número, CVV, valor, descrição, nomes e chave continuam proibidos.

## R11 — Aviso de fatura no início

- **Decision**: `AvisoFatura` consulta `GET /faturas?tamanhoPagina=100` no início da PF e mostra a mais antiga
  `Vencida` ou `Disponivel`. Carregando, erro, 404 ou nada a pagar: não mostra nada. A "Fatura atual" da tela do
  cartão é a primeira da mesma
  lista (a do ciclo atual é sempre a mais nova).

## R12 — Componentes visuais

- **Decision**: `CartaoDesenhado` (proporção 1,586; fundo `--cor-destaque`; bandeira em texto), olho no próprio
  cartão (`IconeOlhoAberto` visível, `IconeOlhoRiscado` oculto), número e CVV lidos dígito a dígito
  (`lerDigitos`, da spec 013), mudança anunciada em `role="status"`. Confirmações (pagar fatura, antecipar,
  cancelar) abrem na própria tela, com foco no título e retorno do foco ao botão. Situações em `Selo` com cor
  e texto.

## R13 — Testes

- **Decision**: backend — unitários e integração (código, colisão, migration, consulta, 401/403, contrato).
  Frontend — Vitest + MSW para cada estado e recusa; Playwright + axe contra o Compose nas larguras de
  referência. Faturas `Disponivel` e `Vencida` no e2e são preparadas por SQL (`docker compose exec postgres
  psql`, disponível localmente e no CI): a compra é feita pela API, e a fatura é antedatada e liberada por
  `UPDATE` (a tabela `faturas` é mutável) e um `INSERT` do lançamento, numa única transação e já com
  `liberada_em`: o worker, que roda a cada 60 s, nunca vê a fatura antedatada sem liberação, o que mudaria o
  vencimento, nem lança a compra de novo. Um e2e percorre os fluxos e procura número
  e CVV em `sessionStorage`, `localStorage`, histórico, endereço e envios OTLP (SC-005).
- **Alternatives considered**: relógio ajustável na API do Compose (mudança no backend só para teste).
