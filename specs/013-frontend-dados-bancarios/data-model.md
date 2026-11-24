# Data Model: Frontend — dados bancários da conta e nome One Bank

**Feature**: [spec.md](spec.md) | **Contrato da API**: [spec 012 — carteira-minha.md 1.1.0](../012-dados-bancarios-carteira/contracts/carteira-minha.md)

Nada é gravado no navegador nem na API: os dados existem só em memória, enquanto a tela está aberta.

## Conta bancária (lida da API)

Tipo gerado `ContaBancariaResposta`, campo `contaBancaria` de `MinhaCarteiraResposta`.

| Campo | Tipo | Regra de apresentação |
| --- | --- | --- |
| `banco.codigo` | string | como vem |
| `banco.nome` | string | como vem; quebra de linha se longo |
| `agencia` | string | como vem, com zeros |
| `numero` | string | como vem, com zeros |
| `digito` | string | como vem; `0` é válido |
| `tipo` | string | ver tabela de tipos |

### Tipos

| Valor da API | Exibido e copiado |
| --- | --- |
| `ContaCorrente` | Conta corrente |
| `ContaPoupanca` | Conta poupança |
| qualquer outro | Conta |

## Dados da conta (visão derivada)

| Item | Regra | Exemplo |
| --- | --- | --- |
| Banco | `{codigo} – {nome}` (travessão curto) | `001 – One Bank` |
| Agência | `{agencia}` | `0001` |
| Conta | `{numero}-{digito}` | `00000042-0` |
| Tipo | tabela de tipos | `Conta corrente` |
| Agência (leitores de tela) | dígitos separados por espaço | `0 0 0 1` |
| Conta (leitores de tela) | dígitos separados por espaço + `, dígito {digito}` | `0 0 0 0 0 0 4 2, dígito 0` |
| Texto copiado | 3 linhas separadas por `\n`, sem linha final | ver abaixo |
| Texto de apoio | `Para receber de clientes One Bank, informe o seu {CPF\|CNPJ}.` | PF → CPF; PJ → CNPJ |

```text
Banco 001 – One Bank
Agência 0001
Conta corrente 00000042-0
```

## Aviso de cópia (estado da tela)

`vazio` → (copiar) → `vazio` → `copiado` | `falhou`. Uma nova cópia volta a `vazio` antes do
resultado. `falhou` usa a cor de erro. Cada cartão (CNPJ e dados da conta) tem o próprio estado.
