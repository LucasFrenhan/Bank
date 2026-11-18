# Research: Frontend de transferências, comprovante e extrato

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Base: o frontend da spec [004](../004-frontend-base/plan.md) (React 19, TypeScript 5.9, Vite 8,
`react-router` 8, `openapi-fetch`, Vitest + MSW, Playwright + axe). Nenhuma dependência npm nova
é necessária; as decisões abaixo usam o que já existe e APIs nativas do navegador.

## R1 — Tipos da API e chamadas novas

- **Decision**: regenerar `src/api/schema.d.ts` com `npm run api:tipos` contra a API da `main`
  (spec 006) e acrescentar ao objeto `api` de `src/api/cliente.ts`: `consultarDestinatario`,
  `transferir`, `obterExtrato` e, após a issue #36, `obterTransferencia`. `transferir` devolve
  também se a resposta foi 201 (nova) ou 200 (reenvio), lido de `response.status`.
- **Rationale**: mantém o único ponto de acesso à rede (Constitution Check II da spec 004) e os
  tipos derivados do contrato.
- **Alternatives considered**: escrever os tipos à mão (diverge do OpenAPI); usar `fetch` direto
  nas telas (proibido pela regra de lint da spec 004).

## R2 — Chave de idempotência

- **Decision**: a chave é um UUID v4 gerado com `crypto.randomUUID()` (com recurso a
  `crypto.getRandomValues` fora de contexto seguro) **ao entrar na confirmação** com um conjunto
  de dados. Ela é guardada junto com a transferência em andamento (R4) e reaproveitada enquanto
  `documento normalizado + valor em centavos + descrição aparada` não mudarem; qualquer mudança,
  um 409 `idempotencia-divergente` ou uma nova transferência geram outra chave. O botão de envio
  fica bloqueado enquanto há requisição em voo (FR-009).
- **Rationale**: atende FR-008 e o Princípio IV; recarregar a confirmação ou voltar pelo
  navegador reenvia com a mesma chave e recebe 200 (US3). A descrição entra na comparação do
  cliente mesmo não entrando na do servidor, para que mudar só a descrição seja, para a pessoa,
  uma nova transferência e não um reenvio.
- **Alternatives considered**: gerar a chave a cada clique (perde a proteção contra reenvio);
  gerar ao abrir a etapa Dados (a chave sobreviveria a edições e provocaria 409 desnecessários);
  biblioteca `uuid` (dependência desnecessária).

## R3 — Valores monetários no navegador

- **Decision**: o valor digitado é convertido para **centavos inteiros** por uma função pura
  (`interpretarValor`): aceita `45,90`, `45.90`, `1.234,56` e `R$ 1.234,56`; se houver vírgula,
  pontos são separadores de milhar; sem vírgula, um único ponto seguido de 1–2 dígitos no fim é
  decimal; mais de 2 casas → erro de campo (sem arredondar). "Saldo depois" e a comparação com o
  saldo são feitos em centavos; o saldo da API (`number | string`) também é convertido para
  centavos a partir do texto com 2 casas. O corpo enviado é `centavos / 100` (número JSON com no
  máximo 2 casas, que o `decimal` da API lê exatamente).
- **Rationale**: Princípio IV (sem `float` para dinheiro na aritmética); evita erros como
  `842.5 - 45.9 = 796.6000000000001`.
- **Alternatives considered**: biblioteca de decimal (`big.js`/`dinero.js`) — dependência a
  mais para uma única subtração; enviar o valor como string (o contrato define número).

## R4 — Transferência em andamento e comprovante na aba

- **Decision**: a transferência em andamento (`bank.transferencia`) e os comprovantes recebidos
  nesta aba (`bank.comprovantes`, até 20, mais recentes) ficam em `sessionStorage`, o mesmo escopo
  da sessão (R5 da spec 004). São apagados por `removerSessao()` (sair, expirar, conta removida).
  As etapas Dados e Confirmação têm rotas próprias (`/transferir` e `/transferir/confirmar`) para
  que recarregar ou voltar no navegador funcione; sem transferência em andamento,
  `/transferir/confirmar` redireciona para `/transferir`.
- **Rationale**: FR-010 e US3 (recarregar na confirmação reaproveita a chave); o comprovante
  continua visível após recarregar mesmo antes da issue #36.
- **Alternatives considered**: estado só em memória (perde a chave ao recarregar, quebrando a
  proteção de reenvio); `localStorage` (vazaria entre abas e sobreviveria à sessão).

## R5 — Comprovante com endereço próprio (`/transferencias/:id`)

- **Decision**: o comprovante é a rota `/transferencias/:transferenciaId`. Ao concluir ou ao
  receber um reenvio (200), o comprovante devolvido é guardado (R4) e a tela navega para essa
  rota (no reenvio, mostra antes o aviso "Essa transferência já foi feita" com o link). A tela
  procura primeiro no cache da aba e, **após a issue #36**, consulta
  `GET /api/v1/transferencias/{id}`; 404 → "Comprovante não encontrado" com link para o extrato.
  O identificador é um UUID da transferência, não um dado pessoal, e pode aparecer na URL e na
  telemetria de navegação.
- **Rationale**: FR-011, FR-012 e FR-014; a mesma tela serve ao pagador e ao recebedor
  ("Transferência recebida" quando o usuário da sessão é o recebedor).
- **Alternatives considered**: comprovante como estado da navegação (some ao recarregar);
  montar o comprovante a partir do item do extrato (opção B rejeitada no clarify).

## R6 — Busca do destinatário

- **Decision**: o campo de documento detecta o tipo pelo conteúdo: com letra ou mais de 11
  caracteres → CNPJ (máscara da spec 004); senão, CPF. A consulta
  (`POST /transferencias/destinatarios/consultar`) é disparada: (a) imediatamente com 14
  caracteres de CNPJ; (b) com 11 dígitos de CPF cujo dígito verificador confere localmente,
  após 500 ms sem digitação ou ao sair do campo. Cada consulta tem um número de sequência e um
  `AbortController`; respostas que não correspondem ao documento atual são descartadas. Alterar
  o documento descarta o titular confirmado.
- **Rationale**: FR-003 e SC-004 (nome em até 2 s), sem consultar CPFs incompletos de quem ainda
  está digitando um CNPJ numérico; a verificação local de DV só decide **quando** consultar — o
  erro de documento continua vindo da API (FR-006).
- **Alternatives considered**: botão "Buscar" (passo extra fora do protótipo); consultar a cada
  tecla (varredura de documentos e ruído na métrica FR-009b da spec 006).

## R7 — Extrato: filtro, paginação e layouts

- **Decision**: rota `/extrato?direcao=entradas|saidas&pagina=N` (`direcao` ausente = todas;
  `pagina` ausente = 1); valores inválidos são normalizados para o padrão. O layout é decidido
  por um hook `useLarguraDesktop()` (`matchMedia('(min-width: 1024px)')`, o mesmo ponto de quebra
  do `Layout`):
  - **desktop**: tabela, uma página de 20 por requisição, "Anterior"/"Próxima", "Página X de Y ·
    20 por página" (`Y = ceil(totalItens / 20)`, mínimo 1); página além da última → substitui a
    URL pela última página válida;
  - **mobile**: lista agrupada por dia ("Hoje", "Ontem", "15 de novembro"; ano quando diferente
    do atual) com "Carregar mais"; a primeira carga de `pagina=N` pede `tamanhoPagina = 20·N`
    (até 100) e as seguintes, a página 20 seguinte; itens são deduplicados por `lancamentoId`.
- **Rationale**: FR-017 a FR-020; a URL preserva filtro e página ao recarregar ou voltar; o
  mesmo componente de dados serve aos dois layouts.
- **Alternatives considered**: rolagem infinita (o protótipo usa "Carregar mais", mais
  acessível); renderizar os dois layouts e esconder um com CSS (duplicaria requisições e o
  conteúdo para leitores de tela).

## R8 — Início PF/PJ e saldo atualizado

- **Decision**: o início PF pede `GET /transferencias/extrato?tamanhoPagina=5`; o PJ,
  `?tamanhoPagina=5&direcao=Entrada`. Carteira e extrato continuam sendo lidos a cada exibição
  (`useCarteira` da spec 004 e um novo `useExtrato`), o que atende FR-025 sem cache global.
  "Entradas/Saídas no mês" não são exibidos (FR-024). "Transferir"/"Ver extrato" viram links;
  `ITENS_NAVEGACAO` ganha `para` em Transferir (`/transferir`), Extrato (`/extrato`) e
  Recebimentos (`/extrato?direcao=entradas`); na barra inferior mobile da PF, "Transferir" sai
  (protótipo: Início, Extrato, Perfil) e fica no cartão de saldo.
- **Rationale**: dados sempre frescos com o padrão já existente; sem biblioteca de cache.
- **Alternatives considered**: TanStack Query (dependência nova para três consultas);
  calcular totais do mês percorrendo o extrato (várias requisições e soma no cliente).

## R9 — Tradução dos erros novos

- **Decision**: `traduzirErro` ganha os códigos `destinatario-nao-encontrado`,
  `autotransferencia`, `saldo-insuficiente`, `idempotencia-divergente` e
  `transferencia-nao-encontrada`, com o texto e a variante visual do protótipo (erro de campo,
  atenção, informativo). `acesso-negado` recebe um contexto opcional (`'transferencia'`) que troca
  a mensagem por "Indisponível para Pessoa Jurídica". O aviso de saldo insuficiente recarrega a
  carteira para exibir o saldo atual. O reenvio (200) não é erro: é tratado pela tela de
  confirmação.
- **Rationale**: FR-026/FR-027 e Princípio III (decisão pelo `type`, nunca pelo `detail`).
- **Alternatives considered**: mapear por status HTTP (dois 422 diferentes e o 409 colidiriam).

## R10 — Compartilhar o comprovante

- **Decision**: `navigator.share({ title, text })` quando existir; senão
  `navigator.clipboard.writeText(text)` com confirmação em região `aria-live`. O texto usa
  valor, data, nomes, documentos mascarados e o identificador; nunca a chave de idempotência.
- **Rationale**: FR-013 sem dependência; funciona em celular e computador.
- **Alternatives considered**: gerar PDF/imagem (fora do escopo da issue).

## R11 — Testes

- **Decision**:
  - **Vitest + MSW**: funções puras (`interpretarValor`, centavos, detecção de tipo de documento,
    agrupamento por dia, chave de idempotência, leitura da URL do extrato) e telas (Transferir,
    Confirmar, Comprovante, Extrato, Início) com handlers MSW para cada resposta do contrato
    (200/201, 400, 403, 404, 409, 422 ×2, rede, 500).
  - **Playwright + axe** contra o Compose, projetos desktop e mobile: fluxo completo PF→PJ,
    PF→PF, cada recusa, reenvio (recarregar a confirmação após concluir e duplo clique),
    extrato com filtros e paginação, início PF/PJ, acesso PJ a `/transferir`, teclado,
    responsividade (320/768/1920) e acessibilidade de cada tela.
  - **Saldo para os e2e**: sem acesso à base; contas de apoio criadas pela API transferem seus
    R$ 10,00 de demonstração para a conta do teste quando é preciso mais saldo, e 21+
    lançamentos para paginação são criados com transferências de R$ 0,01 pela API.
- **Rationale**: FR-030 e SC-008; os e2e continuam independentes da base (R3 da spec 004).
- **Alternatives considered**: endpoint de teste para ajustar saldo (backend fora do escopo);
  SQL direto no Postgres do Compose (acopla o e2e ao schema).
