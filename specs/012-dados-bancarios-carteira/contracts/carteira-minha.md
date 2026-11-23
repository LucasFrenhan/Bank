# Contrato HTTP: `GET /api/v1/carteiras/minha` — v1.1.0

**Feature**: [../spec.md](../spec.md) | **Versão anterior**: [specs/003 — v1.0.0](../../003-usuarios-carteiras/contracts/usuarios-carteiras-api.md#get-apiv1carteirasminha)

Mudança **aditiva** (minor): entra o objeto `contaBancaria`; todos os campos da v1.0.0 continuam
com o mesmo nome, tipo e significado. Acesso, política, erros (`401`, `403`, `404`) e cabeçalhos
não mudam. Nenhuma outra rota muda nesta entrega (FR-009).

## 200 OK

```json
{
  "carteiraId": "019a8f12-9999-7890-abcd-ef1234567890",
  "usuarioId": "019a8f12-3456-7890-abcd-ef1234567890",
  "nomeTitular": "Lucas Silva",
  "tipoConta": "PessoaFisica",
  "saldo": 10.00,
  "moeda": "BRL",
  "atualizadoEm": "2026-11-17T18:00:00Z",
  "contaBancaria": {
    "banco": { "codigo": "001", "nome": "One Bank" },
    "agencia": "0001",
    "numero": "00000042",
    "digito": "0",
    "tipo": "ContaCorrente"
  }
}
```

| Campo | Tipo | Regra |
| --- | --- | --- |
| `contaBancaria` | objeto | sempre presente |
| `contaBancaria.banco.codigo` | string | 3 dígitos; sempre `001` para carteiras |
| `contaBancaria.banco.nome` | string | nome do cadastro de bancos (`One Bank`) |
| `contaBancaria.agencia` | string | 4 dígitos, com zeros à esquerda (`0001`) |
| `contaBancaria.numero` | string | 8 dígitos, com zeros à esquerda |
| `contaBancaria.digito` | string | 1 dígito: Luhn sobre `agencia + numero` ([research R1](../research.md)) |
| `contaBancaria.tipo` | string | `ContaCorrente` \| `ContaPoupanca` |

- `tipoConta` (`PessoaFisica`/`PessoaJuridica`) continua sendo o tipo do **titular**;
  `contaBancaria.tipo` é o tipo da **conta bancária**.
- Exibição sugerida (frontend, task futura): `Banco 001 – One Bank · Ag. 0001 · C/C 00000042-0`.
- Os valores são texto para preservar zeros à esquerda; clientes não devem convertê-los em número.

## Vetor de conferência do dígito

| Agência | Número | Dígito |
| --- | --- | --- |
| `0001` | `00000001` | `6` |
| `0001` | `00000002` | `4` |
| `0001` | `00000042` | `0` |
| `0001` | `12345678` | `0` |
| `0001` | `99999999` | `6` |

Os mesmos vetores são usados em `ContaBancariaTests` (unitário) e no teste que compara a
function `calcular_digito_conta` com a implementação em C#.
