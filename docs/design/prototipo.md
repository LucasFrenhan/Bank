# Protótipo de telas

Referência visual do frontend (React + TypeScript, desktop e mobile) para as specs de
usuários e carteiras ([`003-usuarios-carteiras`](../../specs/003-usuarios-carteiras/spec.md))
e de transferências (issue #8). O protótipo não é uma spec: telas, textos e regras finais
continuam definidos pelas specs aprovadas em `specs/`.

- **Protótipo navegável:** <https://claude.ai/artifact/3E3AmKHUZHPJRomcG4WMGN> (privado;
  o acesso é concedido pelo dono do link).
- **Capturas:** [`telas/`](telas/) — uma imagem por tela.
- **Fontes:** [`fontes/`](fontes/) — um arquivo `.dc.html` por tela, no formato do editor do
  protótipo. Não abrem diretamente no navegador; servem como referência de estrutura,
  textos e estilos.

## Telas

Todas as telas existem em versão desktop (layout fluido, referência em 1440 px) e mobile
(390 × 844).

| Tela | Perfil | Origem | Desktop | Mobile |
| --- | --- | --- | --- | --- |
| Login | Público | Spec 003 — `POST /autenticacao/login` | [captura](telas/desktop-01-login.png) | [captura](telas/mobile-01-login.png) |
| Cadastro (PF / PJ) | Público | Spec 003 — `POST /autenticacao/registrar` | [captura](telas/desktop-02-cadastro.png) | [captura](telas/mobile-02-cadastro.png) |
| Início — Pessoa Física | PF | Spec 003 — `GET /carteiras/minha` | [captura](telas/desktop-03-inicio-pf.png) | [captura](telas/mobile-03-inicio-pf.png) |
| Início — Pessoa Jurídica | PJ | Spec 003 — RN-06 | [captura](telas/desktop-04-inicio-pj.png) | [captura](telas/mobile-04-inicio-pj.png) |
| Extrato | PF e PJ | Issue #13 — `GET /transferencias/extrato` | [captura](telas/desktop-05-extrato.png) | [captura](telas/mobile-05-extrato.png) |
| Transferir (destinatário e valor) | PF | Issue #8 — `POST /transferencias` | [captura](telas/desktop-06-transferir.png) | [captura](telas/mobile-06-transferir.png) |
| Confirmação | PF | Issue #8 | [captura](telas/desktop-07-confirmar.png) | [captura](telas/mobile-07-confirmar.png) |
| Comprovante | PF | Issue #8 | [captura](telas/desktop-08-comprovante.png) | [captura](telas/mobile-08-comprovante.png) |
| Perfil | PF e PJ | Spec 003 | [captura](telas/desktop-09-perfil.png) | [captura](telas/mobile-09-perfil.png) |

Fluxos navegáveis no protótipo: login → início → transferir → confirmação → comprovante →
início; cadastro com alternância PF/PJ (rótulos, máscara e saldo inicial mudam com o tipo);
filtros Todas / Entradas / Saídas no extrato.

## Cartão, cobranças, faturas e recebíveis (spec 015)

Protótipo próprio, aprovado para a [spec 015](../../specs/015-frontend-cartao-credito/spec.md) (issue #54):

- **Protótipo navegável:** <https://claude.ai/artifact/9mhoKhZ8E9guBLsVE53oEY> (privado; o acesso é concedido pelo
  dono do link).
- **Fontes:** [`fontes/cartao/`](fontes/cartao/), no mesmo formato, com o arranjo das telas em `canvas.json`. As
  versões de celular (`M-*`) reaproveitam a tela de desktop.
- **Capturas:** entram com as telas prontas, na implementação da spec 015.

| Tela | Perfil | Desktop | Celular |
| --- | --- | --- | --- |
| Cartão | PF | [Main](fontes/cartao/Main.dc.html) | [M-Cartao](fontes/cartao/M-Cartao.dc.html) |
| Solicitar cartão | PF | [Solicitar](fontes/cartao/Solicitar.dc.html) | [M-Solicitar](fontes/cartao/M-Solicitar.dc.html) |
| Pagar cobrança | PF | [Pagar](fontes/cartao/Pagar.dc.html) | [M-Pagar](fontes/cartao/M-Pagar.dc.html) |
| Comprovante | PF | [Comprovante](fontes/cartao/Comprovante.dc.html) | [M-Comprovante](fontes/cartao/M-Comprovante.dc.html) |
| Faturas | PF | [Faturas](fontes/cartao/Faturas.dc.html) | [M-Faturas](fontes/cartao/M-Faturas.dc.html) |
| Fatura | PF | [Fatura](fontes/cartao/Fatura.dc.html) | [M-Fatura](fontes/cartao/M-Fatura.dc.html) |
| Cobranças | PJ | [Cobrancas](fontes/cartao/Cobrancas.dc.html) | [M-Cobrancas](fontes/cartao/M-Cobrancas.dc.html) |
| Nova cobrança | PJ | [NovaCobranca](fontes/cartao/NovaCobranca.dc.html) | [M-NovaCobranca](fontes/cartao/M-NovaCobranca.dc.html) |
| Cobrança | PJ | [Cobranca](fontes/cartao/Cobranca.dc.html) | [M-Cobranca](fontes/cartao/M-Cobranca.dc.html) |
| Recebíveis | PJ | [Recebiveis](fontes/cartao/Recebiveis.dc.html) | [M-Recebiveis](fontes/cartao/M-Recebiveis.dc.html) |
| Estados e recusas | PF e PJ | [Estados](fontes/cartao/Estados.dc.html) | — |
| Menu | PF e PJ | [Menu](fontes/cartao/Menu.dc.html) | o mesmo |

Onde diferem, vale a spec: nenhuma bandeira vem marcada e o limite não aparece antes da emissão. O aviso de
fatura no início da PF foi decidido depois e não está no protótipo. Número, CVV, nomes e valores são
fictícios.

## Design system

Capturas: [tipografia e tokens](telas/design-system-01-tipografia.png) e
[estados de erro e feedback](telas/design-system-02-estados.png).

### Tipografia

Inter em toda a interface, com algarismos tabulares (`font-variant-numeric: tabular-nums`)
em valores monetários. JetBrains Mono para identificadores técnicos (IDs de transferência,
chave de idempotência, `traceId`).

| Estilo | Desktop | Mobile | Peso | Uso |
| --- | --- | --- | --- | --- |
| Display / Saldo | 40/48 | 32/40 | 600 | Saldo da carteira, valor da transferência |
| H1 | 32/40 | 24/32 | 600 | Título da página |
| H2 | 24/32 | 20/28 | 600 | Seções |
| H3 | 20/28 | 18/24 | 500 | Títulos de cards |
| Body L | 16/24 | 16/24 | 400 | Texto principal, campos de formulário |
| Body M | 14/20 | 14/20 | 400 | Listas e extrato |
| Label | 14/20 | 14/20 | 500 | Rótulos e botões |
| Caption | 12/16 | 12/16 | 400 | Datas, textos de ajuda |
| Mono | 13/20 | 12/16 | 400 | IDs e `traceId` |

Valores em px no formato `tamanho/altura de linha`.

### Cores

| Token | Valor | Uso |
| --- | --- | --- |
| Primária | `#1D4ED8` | Ações principais, links, item ativo |
| Texto | `#0F172A` | Texto e valores de saída |
| Texto suave | `#475569` | Textos secundários |
| Fundo | `#F5F7FA` | Fundo das páginas |
| Borda | `#E2E8F0` | Bordas e divisórias |
| Entrada | `#047857` | Valores de entrada, status concluído |
| Erro | `#B42318` | Validação e erros |
| Atenção | `#B54708` | Avisos de regra de negócio (ex.: saldo insuficiente) |

Entrada e saída também se diferenciam por sinal (`+` / `−`) e ícone, não só pela cor.

### Componentes base

- Raio de 10 px em controles e 16 px em cards; espaçamento em múltiplos de 4 px.
- Alvos de toque com no mínimo 44 px; botões principais com 48 px (desktop) e 52 px
  (mobile).
- Contraste mínimo de 4,5:1 para texto.

## Mensagens de erro

Cada resposta `ProblemDetails` vira uma mensagem para a pessoa; o código HTTP é só
referência para o time.

| Situação | HTTP | Mensagem |
| --- | --- | --- |
| Campo inválido no cadastro | 400 | Erro abaixo do campo (ex.: "CPF inválido. Confira os dígitos.") |
| E-mail ou documento já cadastrado | 409 | "Este e-mail já tem conta", com link para o login |
| Credenciais inválidas | 401 | "E-mail ou senha incorretos" (sem dizer qual) |
| Conta PJ tenta transferir | 403 | "Indisponível para Pessoa Jurídica" |
| Destinatário não encontrado | 404 | "Nenhuma conta One Bank com esse documento." |
| Saldo insuficiente | 422 | "Saldo insuficiente", com o saldo disponível |
| Transferência para a própria carteira | 422 | "Esse documento é o seu" |
| Chave de idempotência com dados diferentes | 409 | "Essa transferência mudou" |
| Reenvio idempotente | 200 | "Essa transferência já foi feita", com link para o comprovante |
| Erro inesperado | 500 | "Algo deu errado", exibindo o `traceId` para o suporte |

## Decisões

- **Saldo inicial:** PF R$ 10,00 e PJ R$ 0,00 (RN-05 da spec 003).
- **Conta PJ sem "Transferir":** a ação nem aparece para PJ (RN-06); o 403 fica como
  proteção caso a tela seja acessada diretamente.
- **Cobranças:** aparecem no menu da PJ como "Em breve", pois ainda não há spec. A spec 015 cria a área de
  Cobranças da PJ.
- **Transferência em três etapas** (dados → confirmação → comprovante), com o destinatário
  confirmado pelo nome antes do envio e o saldo após a transferência na confirmação.
- **Documentos mascarados** fora do formulário (ex.: `•••.456.789-••`).
- **Dados de exemplo:** nomes, documentos e valores das telas são fictícios.
- **Nome e ícone (spec 013):** o banco se chama **One Bank** e o ícone da marca é **1B** (branco
  sobre a cor primária). As capturas e fontes, anteriores a essa decisão, ainda mostram "Bank" e
  "B"; vale a [spec 013](../../specs/013-frontend-dados-bancarios/spec.md).
- **Dados da conta (spec 013):** o início PF e PJ tem o cartão "Dados da conta" (banco, agência,
  conta com dígito, tipo e "Copiar"), que não existe nas telas originais. No desktop fica ao lado
  do saldo (na PJ, abaixo de "Receber transferências"); no celular, abaixo dele.
