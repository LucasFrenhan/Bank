# Research: Frontend — dados bancários da conta e nome One Bank

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Não restou nenhum NEEDS CLARIFICATION: a stack é a das specs 004 e 007, e as decisões de produto
estão em Clarifications. As decisões abaixo são de desenho.

## R1 — Fonte e tipos dos dados

- **Decision**: usar `contaBancaria` de `GET /api/v1/carteiras/minha`, já buscada por `useCarteira`;
  regenerar `src/api/schema.d.ts` com `npm run api:tipos` e expor
  `ContaBancaria = Esquemas['ContaBancariaResposta']` em `src/api/cliente.ts`. Nenhuma chamada nova.
- **Rationale**: a tela já lê a carteira a cada exibição; carregamento, erro e nova tentativa ficam
  os atuais (US1, cenário 7). O tipo gerado mantém todos os campos como `string` (FR-003).
- **Alternatives considered**: hook ou consulta própria (duplicaria a requisição); escrever o tipo à
  mão (quebraria a regra de tipos gerados da spec 004).

## R2 — Formatação num módulo puro

- **Decision**: `src/formatacao/contaBancaria.ts` com funções puras: nome do tipo
  (`ContaCorrente` → "Conta corrente", `ContaPoupanca` → "Conta poupança", outro → "Conta"),
  `numero-digito`, leitura dígito a dígito e texto de cópia. Só concatenação de texto, sem
  `Number`/`parseInt`.
- **Rationale**: regra de apresentação testável sem tela (FR-014); `tipo` é `string` no OpenAPI, então
  o valor desconhecido (FR-005) é um caso normal do mapeamento.
- **Alternatives considered**: formatar no componente (mistura regra e marcação).

## R3 — Leitura por leitores de tela

- **Decision**: `<dl>` com `<dt>/<dd>`; em agência e conta, o valor visível fica em
  `<span aria-hidden="true">` e um `<span className="apenas-leitor-de-tela">` traz os dígitos
  separados por espaço ("0 0 0 1"; "0 0 0 0 0 0 4 2, dígito 0"). O texto só para leitores recebe
  `user-select: none` neste cartão.
- **Rationale**: é o padrão já usado no CPF oculto do Perfil. `user-select: none` evita que a seleção
  manual (US3, cenário 2) copie também o texto invisível.
- **Alternatives considered**: `aria-label` em `<span>` (nome proibido em papel genérico, acusado
  pelo axe); `role="text"` (fora do padrão ARIA); CSS `speak-as` (suporte insuficiente).

## R4 — Copiar

- **Decision**: hook `useCopia` em `src/paginas/useCopia.ts`, com `copiar(texto, { sucesso, falha })` e
  `aviso` (`{ texto, falhou }`); o cartão do CNPJ passa a usá-lo, com os mesmos textos. O aviso é
  limpo antes de cada cópia, e a falha usa a cor de erro (hoje a do CNPJ sai na cor de sucesso;
  FR-007). Cada cartão tem a própria região `role="status"`, no componente comum
  `TextoAvisoCopia`.
- **Rationale**: a segunda cópia da mesma lógica justifica o hook; limpar o aviso faz a cópia repetida
  ser anunciada de novo; avisos separados não se confundem (edge case da PJ). Sem
  `navigator.clipboard` (contexto inseguro) a chamada lança e cai na mensagem de falha.
- **Alternatives considered**: duplicar o código; incluir o comprovante no hook (ele usa
  `navigator.share` primeiro e tem outro fluxo); `document.execCommand` (obsoleto).
- **Consequência**: o teste atual da PJ usa `getByRole('status')` e passa a ter duas regiões; deve
  buscar o status dentro do cartão.

## R5 — Posição e visual do cartão

- **Decision**: `src/paginas/DadosDaConta.tsx`, um `Cartao` com `rotulo="Dados da conta"` e `h2`,
  dentro de `.resumo`. Na PF é a segunda coluna; na PJ, uma coluna empilha "Receber transferências" e
  "Dados da conta", e o saldo fica alinhado ao topo. Os quatro campos (Banco, Agência, Conta, Tipo)
  usam grade de duas colunas `minmax(0, 1fr)` em todas as larguras, com quebra de palavra.
- **Rationale**: `.resumo` já tem duas colunas a partir de 1024 px e uma abaixo. `.linhaDado` do
  Perfil reserva 200 px ao rótulo e ficaria apertada em meia largura; a grade 2×2 mantém o cartão
  baixo no celular. Alinhar o saldo ao topo na PJ evita um bloco escuro esticado.
- **Alternatives considered**: reutilizar `.linhaDado`; uma linha única (pior leitura e quebra).

## R6 — Nome do banco

- **Decision**: constante `NOME_BANCO = 'One Bank'` em `src/marca.ts`, usada por marca, título,
  mensagens e resumo do comprovante; `index.html` (estático) recebe o texto direto. No cartão, o nome
  ao lado do código vem da API (FR-002); o texto de apoio usa `NOME_BANCO`. Um teste percorre os
  arquivos de `src/` (exceto testes e o `api/schema.d.ts` gerado) e o `index.html` e falha se houver `Bank` como palavra sem
  "One " antes.
- **Rationale**: o nome está hoje repetido em 8 pontos; a constante e o teste garantem o SC-003
  também no futuro. Nomes técnicos (`bank.sessao`, `bank-frontend`, `BANK_API_URL`) são minúsculos
  ou maiúsculos e não são afetados pela busca.
- **Alternatives considered**: usar o nome da API em toda a interface (as telas públicas não têm
  sessão); literais soltos (repetem o problema).

## R7 — Ícone "1B"

- **Decision**: `Marca` mantém o `<span aria-hidden>` com o texto "1B", em fonte menor para caber em
  28 px. `favicon.svg` ganha contornos de "1" e "B" (sem `<text>`), mesmo fundo `#1d4ed8` e raio;
  `apple-touch-icon.png` (180×180, fundo sem cantos, como hoje) é gerado a partir do mesmo desenho
  com o Chromium do Playwright, já disponível, por um comando avulso sem script versionado. O
  traço do "1" e o do "B" ficam alinhados à grade de 16 px.
  Legibilidade conferida por captura de 16 × 16 px, com "1" e "B" distinguíveis (FR-009).
- **Rationale**: o favicon é renderizado isolado, sem as fontes da página (motivo já registrado no
  arquivo); nenhuma dependência nova.
- **Alternatives considered**: `<text>` no SVG (depende de fonte do sistema); biblioteca de conversão
  de imagem (dependência só para um arquivo).

## R8 — Telemetria

- **Decision**: nenhum sinal novo. O e2e de telemetria ganha um caso que abre o início, copia os dados
  e verifica que o número da conta (delimitado por não dígitos, `(?<!\d)numero(?!\d)`) e
  `numero-digito` não aparecem nos corpos OTLP.
- **Rationale**: a telemetria não lê corpo de resposta nem conteúdo da tela (spec 004), então o caso
  só protege contra regressão. A delimitação evita falso positivo com timestamps; a agência `0001`,
  curta demais, é coberta por `agencia` não aparecer como nome de atributo.
- **Alternatives considered**: métrica de "copiar" (sem requisito).

## R9 — Estratégia de testes

| Camada | Cobertura |
| --- | --- |
| Vitest (unitário) | `contaBancaria.test.ts`: tipos, zeros, dígito 0, leitura, texto de cópia, tipo desconhecido; `marca.test.ts`: nome antigo ausente |
| Vitest + MSW (componentes) | Início PF, PJ, poupança e tipo desconhecido; leitura acessível; texto de apoio; cópia com sucesso, repetida e com falha; erro/carregamento sem cartão; títulos e mensagens "One Bank"; comprovante compartilhado |
| Playwright (API real, desktop e mobile) | Valores iguais aos da API em PF e PJ, ordem dos cartões, cópia lida da área de transferência, axe, 320–1920 px (suíte atual já percorre `/`), nome de banco longo a 320 px (resposta alterada por `page.route`), seleção manual sem o texto invisível, teclado, telemetria |
| Manual (quickstart) | Conta poupança criada por script, favicon a 16 px, leitor de tela |

A conta poupança não entra no e2e porque só nasce por script SQL, e o e2e não acessa a base
(spec 007); o comportamento é coberto por componente e pelo roteiro manual.
