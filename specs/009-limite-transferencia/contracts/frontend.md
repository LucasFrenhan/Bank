# Contrato do Frontend — v1.2.0

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/007 — frontend.md v1.1.1](../../007-frontend-transferencias/contracts/frontend.md) | **API consumida**: [transferencias-api.md v1.2.0](transferencias-api.md)

Lista só o que muda em relação à v1.1.1. Rotas, navegação e armazenamento não mudam; as telas
Início, Perfil, Comprovante e Extrato não mudam (Clarification Q4).

## Endpoints consumidos (acréscimos)

| Chamada | Tela | Autenticação | Respostas tratadas |
| --- | --- | --- | --- |
| `GET /api/v1/transferencias/limites` | Transferir — Dados | Bearer | 200; 401 (sessão, como na spec 004); 403, rede e 5xx → segue sem limites na dica |
| `POST /api/v1/transferencias` | Transferir — Confirmação | Bearer | + 422 `limite-excedido` |

## Transferir — Dados

**Máximo agora** = menor valor entre `porOperacao` e `disponivelHoje` (ignorando `null`). Se os
dois forem `null`, ou a consulta falhar, não há máximo.

Dica do campo valor (desktop e mobile):

| Situação | Texto |
| --- | --- |
| Com máximo | "Disponível: R$ {saldo} · Limite agora: R$ {máximo} · mínimo R$ 0,01" |
| Sem máximo | "Disponível: R$ {saldo} · mínimo R$ 0,01" (como na v1.1.1) |

Ao continuar, valor acima do máximo não avança e mostra o aviso de limite abaixo do campo valor,
com o tipo calculado na mesma ordem da API: primeiro `PorOperacao`, depois `Diario`. Valor igual
ao máximo é permitido.

## Tradução de erros (acréscimos)

| `type` (sufixo) | Onde aparece | Variante | Texto |
| --- | --- | --- | --- |
| `limite-excedido`, `tipoLimite=PorOperacao` | Etapa Dados (antecipado) ou Confirmação | Atenção | "Limite excedido" — "O limite por transferência é R$ {limite}. Ajuste o valor para continuar." |
| `limite-excedido`, `tipoLimite=Diario`, `valorDisponivel > 0` | idem | Atenção | "Limite excedido" — "Você ainda pode transferir R$ {valorDisponivel} hoje. Ajuste o valor para continuar." |
| `limite-excedido`, `tipoLimite=Diario`, `valorDisponivel = 0` | idem | Atenção | "Limite excedido" — "Você já usou todo o limite diário de R$ {limite}. Ele renova à meia-noite (horário de Brasília)." |
| `limite-excedido`, outro `tipoLimite` ou extensões ausentes | idem | Atenção | "Limite excedido" — "Ajuste o valor para continuar." |

Na Confirmação, o aviso oferece voltar para a etapa Dados (mesmo comportamento de
`saldo-insuficiente`). O máximo da dica é recarregado ao voltar.

## Telemetria (acréscimos)

- A falha de `GET /limites` é registrada só com status, `type` e `traceId`.
- Limites e valores não entram em atributos de telemetria (regra de valores da v1.1.0).

## Histórico

- **1.2.0** (2026-11-18): limites na dica do valor e aviso de `limite-excedido` (issue #33).
- **1.1.1** (2026-11-18): ver spec 007.
