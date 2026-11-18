# Contrato do Frontend — v1.1.1

**Feature**: [../spec.md](../spec.md) | **Base**: [specs/004 — frontend.md v1.0.0](../../004-frontend-base/contracts/frontend.md) | **API consumida**: [specs/008 — transferencias-api.md v1.1.0](../../008-consulta-transferencia/contracts/transferencias-api.md) (base: spec 006 v1.0.0)

Este documento lista só o que muda em relação à v1.0.0 (spec 004); ambiente, scripts e regras de
telemetria continuam valendo.

## Rotas (acréscimos)

| Rota | Acesso | Tela | Observações |
| --- | --- | --- | --- |
| `/transferir` | Protegida; PJ vê "Indisponível para Pessoa Jurídica" | Transferir — Dados | Restaura a transferência em andamento da aba; após uma concluída, começa vazia |
| `/transferir/confirmar` | Protegida; PJ idem | Transferir — Confirmação | Sem transferência em andamento válida → `/transferir` |
| `/transferencias/:transferenciaId` | Protegida (PF e PJ) | Comprovante | Cache da aba ou `GET /transferencias/{id}`; inexistente ou de outra pessoa → "Comprovante não encontrado" |
| `/extrato` | Protegida (PF e PJ) | Extrato | Query `direcao` (`entradas`·`saidas`), `pagina` (≥ 1) |

## Navegação por perfil

| Perfil | Desktop (menu lateral) | Mobile (barra inferior) |
| --- | --- | --- |
| PF | Início · Transferir → `/transferir` · Extrato → `/extrato` · Perfil | Início · Extrato · Perfil (Transferir pelo cartão de saldo) |
| PJ | Início · Recebimentos → `/extrato?direcao=entradas` · Cobranças (Em breve) · Perfil | Início · Recebimentos · Cobranças (Em breve) · Perfil |

## Endpoints consumidos (acréscimos)

| Chamada | Tela | Autenticação | Respostas tratadas |
| --- | --- | --- | --- |
| `POST /api/v1/transferencias/destinatarios/consultar` | Transferir — Dados | Bearer | 200, 400, 401, 403, 404, 422, rede, 5xx |
| `POST /api/v1/transferencias` (`Idempotency-Key`) | Transferir — Confirmação | Bearer | 201, 200 (reenvio), 400, 401, 403, 404, 409, 422 ×2, rede, 5xx |
| `GET /api/v1/transferencias/extrato` | Extrato; Início PF (`tamanhoPagina=5`); Início PJ (`tamanhoPagina=5&direcao=Entrada`) | Bearer | 200, 400, 401, 403, rede, 5xx |
| `GET /api/v1/transferencias/{id}` (spec 008) | Comprovante (quando não está no cache da aba) | Bearer | 200, 400, 401, 403, 404, rede, 5xx |
| `GET /api/v1/carteiras/minha` | + Transferir (saldo disponível), Confirmação ("Saldo depois"), Extrato (saldo atual) | Bearer | como na v1.0.0 |

## Armazenamento do navegador (acréscimos)

| Chave (`sessionStorage`) | Conteúdo | Apagada quando |
| --- | --- | --- |
| `bank.transferencia` | Transferência em andamento ([data-model](../data-model.md#transferência-em-andamento)) | Sair, sessão expirada/recusada, nova transferência após concluída |
| `bank.comprovantes` | Até 20 comprovantes recebidos nesta aba | Sair, sessão expirada/recusada |

## Telemetria (acréscimos)

- Navegações das rotas novas registradas pelo **padrão** da rota (`/transferencias/:id`), sem o
  identificador nem a query string.
- Falhas das chamadas novas registradas só com `type`, status e `traceId`.
- Proibidos em qualquer sinal, além da lista da v1.0.0: valor, descrição, nome de destinatário ou
  contraparte, chave de idempotência. O identificador da transferência não é dado pessoal e pode
  aparecer no caminho da chamada `GET /api/v1/transferencias/{id}` (spec 008, FR-007).

## Histórico

- **1.1.1** (2026-11-18): comprovante reaberto pela consulta da spec 008 (contrato de
  transferências v1.1.0); CPF do pagador vindo do comprovante, sem `GET /usuarios/me`; nota do
  identificador da transferência na telemetria (issue #32, fase 10).
- **1.1.0** (2026-11-18): rotas de transferência, comprovante e extrato; navegação ativa por
  perfil; endpoints da spec 006 e da issue #36 (issue #32).
- **1.0.0** (2026-11-17): versão inicial (issue #17).
