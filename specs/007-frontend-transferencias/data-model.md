# Data Model: Frontend de transferências, comprovante e extrato

**Feature**: [spec.md](spec.md) | **Pesquisa**: [research.md](research.md)

Modelos do navegador. Nada é persistido além de `sessionStorage` (escopo da sessão da spec
004); os dados de negócio vêm do contrato da [spec 006](../006-transferencias/contracts/transferencias-api.md).

## Transferência em andamento

Guardada em `sessionStorage['bank.transferencia']` (R4). Apagada ao sair, ao expirar a sessão, ao
abrir "Transferir" depois de uma transferência concluída e por `removerSessao()`.

| Campo | Tipo | Regra |
| --- | --- | --- |
| `documento` | string | Normalizado: só `[0-9A-Z]`, 11 (CPF) ou 14 (CNPJ) caracteres |
| `destinatario` | `{ nome, tipoConta }` \| null | Resultado da consulta para **este** `documento`; null = não confirmado |
| `valorCentavos` | inteiro | ≥ 1 |
| `descricao` | string | Aparada; vazia = ausente; ≤ 200 |
| `chave` | UUID \| null | Criada ao entrar na confirmação (R2) |
| `assinatura` | string | `documento|valorCentavos|descricao` no momento em que `chave` foi criada |
| `concluida` | `transferenciaId` \| null | Preenchido após 200/201 |

**Transições**

```text
vazia ──digitar──▶ Dados (destinatario null)
Dados ──consulta 200──▶ Dados (destinatario confirmado)
Dados ──documento alterado──▶ Dados (destinatario null)
Dados ──Continuar (válido)──▶ Confirmação (chave = nova se assinatura mudou; senão mantém)
Confirmação ──Voltar/Editar──▶ Dados (mantém tudo, inclusive chave)
Confirmação ──201──▶ Comprovante (concluida = id)
Confirmação ──200 (reenvio)──▶ aviso "Essa transferência já foi feita" + link (concluida = id)
Confirmação ──409──▶ aviso "Essa transferência mudou"; chave = null (próximo envio gera outra)
Confirmação ──422/404/403/400/rede/5xx──▶ aviso correspondente; mantém chave
```

## Comprovante

Corpo de `POST /transferencias` (200/201) e, após a issue #36, de
`GET /transferencias/{id}`. Cache da aba em `sessionStorage['bank.comprovantes']` (até 20,
mapa `transferenciaId → comprovante`).

| Exibição | Origem |
| --- | --- |
| Título | "Transferência concluída" (sessão = pagador) ou "Transferência recebida" (sessão = recebedor) |
| Valor | `valor` |
| De | `pagador.nome`; + ` · CPF {mascarado}` só quando a sessão é o pagador (`GET /usuarios/me`) — a API não devolve o documento do pagador |
| Para | `recebedor.nome` · `ocultarDocumento(recebedor.documento)` |
| Descrição | `descricao` (linha omitida quando vazia) |
| Data | `criadoEm` no fuso do navegador (`17 nov 2026, 08:10`) |
| Status | `status` `Concluida` → selo "Concluída" |
| ID | `transferenciaId`, fonte monoespaçada |

## Item de extrato (visão)

Do `GET /transferencias/extrato`.

| Campo de tela | Regra |
| --- | --- |
| Contraparte | `contraparte.nome`; `tipo = Abertura` → "Saldo inicial" |
| Direção | `Entrada` → "Entrada", seta para baixo, "+", cor de entrada; `Saida` → "Saída", seta para cima, "−", cor de texto |
| Descrição | `descricao`; vazia → "Sem descrição" |
| Status | "Concluída" (todo lançamento existente é concluído) |
| Data (desktop) | `17 nov, 14:32` (ano quando diferente do atual) |
| Data (início) | `Hoje, 14:32` · `Ontem, 19:47` · `15 nov, 11:05` |
| Grupo (mobile) | `HOJE` · `ONTEM` · `15 DE NOVEMBRO` (ano quando diferente do atual) |
| Link | `tipo = Transferencia` → `/transferencias/{transferenciaId}`; abertura sem link. Até a issue #36, só quando o comprovante está no cache da aba |

Datas e agrupamentos usam o fuso do navegador.

## Filtro do extrato (URL)

| Parâmetro | Valores | API |
| --- | --- | --- |
| `direcao` | ausente · `entradas` · `saidas` | ausente · `Entrada` · `Saida` |
| `pagina` | inteiro ≥ 1 (inválido → 1) | `pagina` (desktop) ou `tamanhoPagina = 20·N` na 1ª carga mobile (R7) |

## Interpretação de valor (`interpretarValor`)

| Entrada | Resultado |
| --- | --- |
| `45,90` · `45.90` · `R$ 45,90` | 4590 centavos |
| `1.234,56` · `1234,56` | 123456 |
| `1.234` | 123400 (ponto como milhar, sem vírgula e sem 1–2 dígitos finais) |
| `10,5` | 1050 |
| `0` · `0,00` | erro "Informe um valor a partir de R$ 0,01" |
| `10,005` | erro "Use no máximo 2 casas decimais" |
| vazio / sem dígitos | erro "Informe o valor" |

## Tradução de erros (acréscimos à spec 004)

| `type` (sufixo) | Onde aparece | Variante | Texto |
| --- | --- | --- | --- |
| (local) documento incompleto | Campo do documento | Erro de campo | "Informe o CPF ou CNPJ completo" |
| (local) consulta em andamento | Abaixo do campo do documento | Status | "Buscando titular…" |
| `destinatario-nao-encontrado` | Campo do documento | Erro de campo | "Nenhuma conta Bank com esse documento." |
| `autotransferencia` | Abaixo do cartão do documento | Atenção | "Esse documento é o seu" — "Não é possível transferir para a própria carteira." |
| `saldo-insuficiente` | Etapa Dados (antecipado) ou Confirmação | Atenção | "Saldo insuficiente" — "Você tem R$ {saldo} disponíveis. Ajuste o valor para continuar." |
| `idempotencia-divergente` | Confirmação | Atenção | "Essa transferência mudou" — "Os dados não batem com o envio anterior. Revise e envie de novo." |
| `acesso-negado` (contexto transferência) | Tela inteira | Neutro (cadeado) | "Indisponível para Pessoa Jurídica" — "Contas PJ recebem transferências e geram cobranças, mas não enviam." |
| `transferencia-nao-encontrada` (#36) | Comprovante | Neutro | "Comprovante não encontrado", link "Ir para o extrato" |
| `validacao` | Campos `documento`/`documentoDestino`, `valor`, `descricao`; `Idempotency-Key` → mensagem geral "Algo deu errado" | Erro de campo | Texto devolvido por campo |
| 200 no envio (reenvio) | Confirmação | Informativo | "Essa transferência já foi feita" — "Nada foi cobrado de novo." + "Ver comprovante" |
| demais | Local da ação | Erro | "Algo deu errado" + `traceId` (spec 004) |
