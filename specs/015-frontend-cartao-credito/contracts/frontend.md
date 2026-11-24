# Contrato do Frontend — v1.4.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/013 — frontend.md v1.3.0](../../013-frontend-dados-bancarios/contracts/frontend.md) | **API consumida**: [cartoes-cobrancas-api.md v1.1.0](cartoes-cobrancas-api.md)

Lista só o que muda em relação à v1.3.0.

## Rotas

| Rota | Perfil | Tela | Título |
| --- | --- | --- | --- |
| `/cartao` | PF | Cartão, ou solicitação se não houver | Cartão |
| `/cartao/faturas` | PF | Lista de faturas | Faturas |
| `/cartao/faturas/:competencia` | PF | Fatura (`AAAA-MM`) e pagamento | Fatura |
| `/cobrancas?situacao=&pagina=` | PJ | Lista de cobranças | Cobranças |
| `/cobrancas/nova` | PJ | Nova cobrança | Nova cobrança |
| `/cobrancas/recebiveis?situacao=&pagina=` | PJ | Recebíveis e antecipação | Recebíveis |
| `/c/:codigo` | todos | Detalhe (emissora), pagamento ou comprovante (PF), só dados (outra PJ) | Cobrança / Pagar cobrança / Pagamento aprovado |

Perfil errado (e 403 `acesso-negado` nas rotas novas): "Área indisponível para o seu tipo de conta", com "O cartão é
para contas Pessoa Física." ou "Cobranças e recebíveis são para contas Pessoa Jurídica." e "Ir para o início". Outra
PJ em `/c/:codigo`: "Só contas Pessoa Física pagam cobranças", "Entre com a sua conta Pessoa Física para pagar." e
"Ir para o início". `situacao` no endereço:
`pendentes`, `pagas`, `canceladas` (cobranças); `pendentes`, `antecipados`, `liquidados` (recebíveis).

## Navegação por perfil

| Perfil | Menu lateral | Barra inferior |
| --- | --- | --- |
| PF | Início, Transferir, Extrato, Cartão, Perfil | Início, Extrato, Cartão, Perfil |
| PJ | Início, Recebimentos, Cobranças, Perfil | os mesmos |

"Cartão" e "Cobranças" ficam ativos nas rotas filhas. Em `/cobrancas` e `/cobrancas/recebiveis` há a navegação
"Seções de cobranças": Cobranças e Recebíveis.

## Início e perfil

- Início PF: região "Aviso de fatura" só com fatura disponível ou vencida
  ([data-model](../data-model.md#aviso-de-fatura)).
- Perfil: PF ganha "Usar cartão de crédito e pagar faturas"; PJ ganha "Receber e antecipar recebíveis".

## Cartão

| Elemento | Contrato |
| --- | --- |
| Olho | botão no cartão; nome "Exibir dados do cartão" (olho riscado) / "Ocultar dados do cartão" (olho aberto) |
| Anúncio | "Dados do cartão exibidos" / "Dados do cartão ocultos" (`role="status"`) |
| Fatura atual | região "Fatura atual": mês, situação, total, "Fecha em … · Vence em …", link "Abrir fatura"; erro só nessa região |
| Faturas | link "Ver faturas" → `/cartao/faturas` |
| Copiar | "Copiar número", só com os dados exibidos; "Número copiado" |
| Campo | "Link ou código da cobrança" + "Abrir"; erro "Confira o link ou o código" |

## Pagamento

Campos "Nome impresso no cartão" (`cc-name`), "Número do cartão" (`cc-number`), "Validade" (`cc-exp`) e "CVV"
(`cc-csc`); "Usar meu cartão"; botão "Pagar R$ {valor}". Validação local:

| Campo | Mensagem |
| --- | --- |
| Nome | Informe o nome impresso no cartão |
| Número | Confira o número do cartão |
| Validade | Informe a validade no formato MM/AA · O cartão está vencido |
| CVV | O CVV tem 3 dígitos |

## Tradução de erros (acréscimos)

| Código | Título | Orientação | Próxima ação |
| --- | --- | --- | --- |
| `cartao-nao-autorizado` | Cartão não autorizado | Confira os dados do cartão e digite o CVV de novo. | Ver os dados do meu cartão → `/cartao` |
| `cartao-bloqueado-fatura-vencida` | Cartão bloqueado por fatura vencida | Pague a fatura para voltar a usar o cartão. | Ver faturas → `/cartao/faturas` |
| `limite-cartao-insuficiente` | Limite insuficiente | Você tem R$ {disponível} de limite disponível. | Ver limite → `/cartao` |
| `cobranca-nao-encontrada` | Cobrança não encontrada | Confira o link com quem enviou. | Ir para o início → `/` |
| `cobranca-nao-pendente` | Esta cobrança não está mais disponível | Ela já foi paga ou cancelada. | Ir para o cartão → `/cartao` |
| `cobranca-nao-pendente` (cancelar, paga) | Esta cobrança acabou de ser paga | Não é mais possível cancelar. | Ver pagamento → detalhe atualizado |
| `cobranca-nao-pendente` (cancelar, cancelada) | Esta cobrança já foi cancelada | Veja a situação atualizada abaixo. | Ver cobranças → `/cobrancas` |
| `rede` (emitir cobrança) | Não foi possível confirmar a emissão | Confira na lista se a cobrança foi criada antes de enviar de novo. | Ver cobranças → `/cobrancas` |
| `cartao-nao-encontrado` | Você ainda não tem um cartão | Peça o seu para pagar esta cobrança. | Solicitar cartão → `/cartao` |
| `cartao-ja-existente` | Você já tem um cartão | Mostramos o seu cartão abaixo. | Ver cartão → o cartão na mesma tela |
| `fatura-nao-encontrada` | Fatura não encontrada | Confira o mês na lista de faturas. | Ver faturas → `/cartao/faturas` |
| `fatura-nao-disponivel` | Esta fatura não está disponível para pagamento | Veja a situação atualizada abaixo. | Ver faturas → `/cartao/faturas` |
| `saldo-insuficiente` (fatura) | Saldo insuficiente | Você tem R$ {saldo} e a fatura é de R$ {valor}. | Voltar à fatura → fecha a confirmação |
| `recebivel-nao-encontrado` | Recebível não encontrado | Veja a lista atualizada. | Ver recebíveis → lista recarregada |
| `recebivel-nao-antecipavel` | Este recebível não pode mais ser antecipado | Veja a situação atualizada abaixo. Previsto para hoje: O valor integral já está previsto para hoje. | Ver recebíveis → lista recarregada |
| `idempotencia-divergente` (operação) | Confira antes de tentar de novo | Os dados não batem com o envio anterior. | Conferir → recarrega a tela da operação |

## Armazenamento

`bank.operacoes` (`sessionStorage`): chave de idempotência por operação, sem dados do cartão, no escopo da
sessão. Proibidos em qualquer armazenamento: número, CVV, validade e nome do cartão.

## Telemetria (acréscimos)

- Rotas registradas como `/c/:codigo` e `/cartao/faturas/:competencia`.
- Proibidos em qualquer sinal, além das listas anteriores: número, CVV e validade do cartão, valor, descrição e
  código de autorização.
- 404 `cartao-nao-encontrado` no início e em `/cartao`, e falhas da atualização automática, não geram log.

## Histórico

- **1.4.0** (2026-11-24): cartão, cobranças, faturas, recebíveis e aviso de fatura (issue #54).
- **1.3.0** (2026-11-24): ver spec 013.
