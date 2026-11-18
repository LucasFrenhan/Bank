# Quickstart: Frontend de transferências, comprovante e extrato

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/frontend.md](contracts/frontend.md) | **Modelo**: [data-model.md](data-model.md)

## Pré-requisitos

- Ambiente da spec 004 funcionando (Docker; Node.js 26.13.0 para testes fora do Compose).
- API com a spec 006 (`main`); para o cenário 7, também com a issue #36.

## Cenário 1 — Qualidade

```bash
cd src/frontend
npm ci
npm run api:tipos   # com a API no ar; regenera src/api/schema.d.ts com os endpoints da spec 006
npm run lint && npm run typecheck && npm test && npm run build
```

**Esperado**: sem erros; testes de componentes de Transferir, Confirmação, Comprovante, Extrato
e Início aprovados (cada resposta do contrato simulada).

## Cenário 2 — Preparar contas

```bash
docker compose up -d --build --wait
```

Em <http://localhost:3000>, crie: uma PF **A** (pagadora, R$ 10,00), uma PF **B** e uma PJ **C**
com CNPJ alfanumérico. Para ter mais saldo em A, entre com B e transfira R$ 10,00 para A.

## Cenário 3 — Transferência completa (US1) em desktop e em 390 × 844

1. Entre com A → início: "Transferir" e "Ver extrato" ativos, sem "Em breve".
2. "Transferir" → digite o CNPJ de C em minúsculas: máscara em maiúsculas e cartão com a razão
   social, "Pessoa Jurídica" e documento mascarado.
3. Valor `4,50`, descrição "Café" → "Continuar" → confirmação com "Saldo depois" correto.
4. "Transferir R$ 4,50" → comprovante "Transferência concluída", ID em fonte monoespaçada.
5. "Voltar ao início": saldo reduzido e a transferência em "Últimas movimentações".

**Esperado**: igual nos dois tamanhos, sem rolagem horizontal.

## Cenário 4 — Recusas (US2)

| Ação | Esperado |
| --- | --- |
| CPF válido sem conta | Erro no campo: "Nenhuma conta Bank com esse documento." |
| CPF da própria A | Aviso "Esse documento é o seu" |
| Valor acima do saldo | Aviso "Saldo insuficiente" com o saldo disponível |
| `10,005` | Erro de campo "Use no máximo 2 casas decimais" |
| Entrar com C e abrir `/transferir` | "Indisponível para Pessoa Jurídica" |
| API parada (`docker compose stop api`) e enviar | Mensagem de conexão; dados preservados; ao religar, "Transferir" reenviado com a mesma chave |

## Cenário 5 — Reenvio sem cobrar duas vezes (US3)

1. Conclua uma transferência; use "voltar" do navegador até a confirmação e clique em
   "Transferir" de novo → "Essa transferência já foi feita — Nada foi cobrado de novo." com
   "Ver comprovante".
2. Na confirmação, recarregue a página e envie → mesmo resultado se já concluída.
3. Duplo clique rápido em "Transferir" numa transferência nova → um único lançamento no extrato.

## Cenário 6 — Extrato e início (US4, US5)

1. Com A, abra o extrato: lançamentos recentes primeiro, "Saldo inicial" com "Saldo inicial de
   demonstração", filtros Todas/Entradas/Saídas (a URL muda; recarregar mantém o filtro).
2. Com mais de 20 lançamentos: desktop com "Página 1 de 2 · 20 por página" e "Próxima"; mobile
   com grupos por dia e "Carregar mais".
3. Com C: início com "Últimos recebimentos" reais; "Recebimentos" e "Ver tudo" abrem o extrato
   filtrado em Entradas.

## Cenário 7 — Reabrir o comprovante (após a issue #36)

1. No extrato de A, escolha a transferência para C → comprovante completo.
2. Copie o endereço, saia, entre de novo e abra-o → mesmo comprovante.
3. Com C, abra a mesma transferência pelo extrato → "Transferência recebida".
4. Com B, abra o endereço → "Comprovante não encontrado".

## Cenário 8 — Ponta a ponta, acessibilidade e telemetria

```bash
cd src/frontend
npm run e2e
```

**Esperado**: projetos `desktop` e `mobile` aprovados, incluindo axe sem violações A/AA em cada
tela nova, fluxo só por teclado e larguras 320/768/1920. No Aspire Dashboard
(<http://localhost:18888>), a transferência aparece como um trace `bank-frontend` → API, sem
documento, nome, valor, descrição ou chave de idempotência nos atributos.
