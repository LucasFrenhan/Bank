# Contrato do Frontend — v1.3.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/009 — frontend.md v1.2.0](../../009-limite-transferencia/contracts/frontend.md) | **API consumida**: [specs/012 — carteira-minha.md v1.1.0](../../012-dados-bancarios-carteira/contracts/carteira-minha.md)

Lista só o que muda em relação à v1.2.0. Rotas, navegação, armazenamento e endpoints consumidos não
mudam.

## Início — cartão "Dados da conta"

| Aspecto | Contrato |
| --- | --- |
| Região | nome acessível "Dados da conta", com título de nível 2 igual |
| Posição PF | ao lado do saldo (≥ 1024 px); abaixo do saldo (< 1024 px) |
| Posição PJ | abaixo de "Receber transferências": mesma coluna (≥ 1024 px); em seguida (< 1024 px) |
| Campos | Banco, Agência, Conta, Tipo, nessa ordem, com rótulo associado |
| Valores e leitura | [data-model — Dados da conta](../data-model.md#dados-da-conta-visão-derivada) |
| Texto de apoio | "Para receber de clientes One Bank, informe o seu CPF." (PF) / "…CNPJ." (PJ) |
| Ação | botão "Copiar", nome acessível "Copiar dados da conta" |
| Sucesso | "Dados da conta copiados" (`role="status"`) |
| Falha | "Não foi possível copiar. Selecione os dados e copie manualmente." (`role="status"`) |
| Carregando ou erro da carteira | o cartão não é exibido |

O cartão do CNPJ (PJ) mantém textos, ação e avisos da v1.2.0, exceto o nome do banco.

## Nome e ícone

| Onde | Antes | Agora |
| --- | --- | --- |
| Marca (nome acessível e visível) | Bank | One Bank |
| Ícone da marca (decorativo), favicon e atalho iOS | B | 1B |
| Título da página | `{tela} · Bank` | `{tela} · One Bank` |
| Título inicial e descrição (`index.html`) | Bank | One Bank |
| `destinatario-nao-encontrado` | "Nenhuma conta Bank com esse documento." | "Nenhuma conta One Bank com esse documento." |
| Início PJ | "Clientes com conta Bank…" | "Clientes com conta One Bank…" |
| Não encontrada | "…não existe no Bank." | "…não existe no One Bank." |
| Resumo do comprovante | "Comprovante de transferência — Bank" | "Comprovante de transferência — One Bank" |

Nomes técnicos não mudam: `bank.sessao`, `bank.transferencia`, `bank.comprovantes`,
`bank-frontend`, `bank.frontend.*`, `BANK_API_URL`.

## Telemetria (acréscimos)

- Proibidos em qualquer sinal, além das listas anteriores: agência, número e dígito da conta.
- A ação de copiar não gera sinal.

## Histórico

- **1.3.0** (2026-11-24): cartão "Dados da conta" no início; nome "One Bank" e ícone "1B" (issue #51).
- **1.2.0** (2026-11-18): ver spec 009.
