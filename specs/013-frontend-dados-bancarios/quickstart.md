# Quickstart: validar dados bancários e nome One Bank

**Feature**: [spec.md](spec.md) | **Contrato**: [contracts/frontend.md](contracts/frontend.md) | **Dados**: [data-model.md](data-model.md)

## Pré-requisitos

- Docker Desktop em execução e Node.js 26.13.0 (`src/frontend/.nvmrc`).
- Na raiz do repositório:

```powershell
docker compose up -d --build --wait
```

## Tipos da API

```powershell
cd src/frontend
npm ci
npm run api:tipos
git diff --stat src/api/schema.d.ts
```

Esperado: a única mudança é aditiva — `contaBancaria` em `MinhaCarteiraResposta` e os esquemas
`ContaBancariaResposta` e `BancoResposta`. `BANK_API_URL` aponta para outra API, se preciso.

## Verificações automáticas

```powershell
npm run lint
npm run typecheck
npm test
npm run build
docker compose -f ../../docker-compose.yml up -d --build --wait frontend
npm run e2e
```

Esperado: tudo verde, em `desktop` e `mobile`.

## Roteiro manual (http://localhost:3000)

| # | Passo | Esperado | Spec |
| --- | --- | --- | --- |
| 1 | Cadastrar uma PF e abrir o início | "Dados da conta" ao lado do saldo, com os 4 campos iguais aos de `GET /carteiras/minha` e "…informe o seu CPF." | US1.1, US1.4, US1.6 |
| 2 | Cadastrar uma PJ e abrir o início | Cartão abaixo de "Receber transferências"; CNPJ e seu "Copiar" como antes | US1.2 |
| 3 | Conta poupança (comando abaixo) | Tipo "Conta poupança" | US1.3 |
| 4 | Leitor de tela (NVDA) sobre o cartão | Rótulo e valor juntos; agência e conta dígito a dígito; "dígito N" | US1.5 |
| 5 | Parar a API (`docker compose stop api`) e recarregar | Erro atual da tela, sem o cartão; `docker compose start api` ao final | US1.7 |
| 6 | "Copiar" e colar num editor | As 3 linhas, sem nome nem documento; aviso de sucesso; copiar de novo repete o aviso | US3.1, US3.3 |
| 7 | Bloquear a área de transferência do site e copiar | Mensagem de falha; a seleção manual copia só o texto visível | US3.2 |
| 8 | Percorrer entrar, criar conta, 404, início, transferir, comprovante, extrato e perfil | Ícone "1B", "One Bank" na marca (onde existe), no título e nos textos | US2 |
| 9 | Compartilhar um comprovante | Começa com "Comprovante de transferência — One Bank" | US2.4 |
| 10 | Olhar a aba do navegador e, no celular, o atalho na tela inicial | "1B" legível | FR-009 |
| 11 | Início em 320, 390, 768, 1024, 1440 e 1920 px | Sem rolagem horizontal nem corte | FR-012 |

Conta poupança, na raiz do repositório (no PowerShell, rode pelo Git Bash):

```bash
docker compose exec -T postgres psql -U bank -d bank -v ON_ERROR_STOP=1 < scripts/sql/dados-bancarios/criar-conta-poupanca.sql
```

Entre com `poupanca.demo@exemplo.com.br` / `Poupanca@123`.

## Telemetria

Depois do passo 6, no Aspire Dashboard (<http://localhost:18888>), nenhum sinal de `bank-frontend`
contém agência, número ou dígito da conta (FR-013).
