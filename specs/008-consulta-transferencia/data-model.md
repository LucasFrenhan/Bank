# Data Model: Consulta de transferência por identificador

**Feature**: [spec.md](spec.md) | **Pesquisa**: [research.md](research.md)

Nenhuma tabela, coluna, índice ou migration nova: a consulta lê `transferencias`, `carteiras` e
`usuarios` da spec [006](../006-transferencias/data-model.md). A busca é pela chave primária de
`transferencias` (`id`), já indexada.

## Comprovante (Application) — alterado

| Campo | Tipo | Regra |
| --- | --- | --- |
| `TransferenciaId` | `Guid` | — |
| `ChaveIdempotencia` | `Guid?` | **Alterado**: sempre preenchido no envio; na consulta, preenchido só para o pagador (FR-003) |
| `Pagador` | `PagadorComprovante(UsuarioId, Nome, Documento)` | **`Documento` novo**: CPF mascarado `•••.982.247-••` (`Documento.Ocultar`), em envio e consulta (FR-002a) |
| `Recebedor` | `RecebedorComprovante(UsuarioId, Nome, Documento, TipoConta)` | Documento completo (inalterado) |
| `Valor` | `decimal` | — |
| `Descricao` | `string?` | — |
| `Status` | `StatusTransferencia` | `Concluida` |
| `CriadoEm` | `DateTimeOffset` | UTC |

## Consulta (entrada do caso de uso)

| Campo | Origem | Regra |
| --- | --- | --- |
| `UsuarioId` | claim `sub` | Ausente → 403 `acesso-negado` (no endpoint) |
| `TransferenciaId` | rota (texto) | UUID válido; senão 400 `validacao` no parâmetro `transferenciaId` |

## Regra de acesso (`ConsultarTransferencia`)

```text
id inválido                                   → 400 validacao            (métrica: invalida)
comprovante inexistente                       → 404 transferencia-nao-encontrada (métrica: nao-encontrada)
usuário ≠ pagador e usuário ≠ recebedor       → 404 transferencia-nao-encontrada (métrica: nao-encontrada)
usuário = pagador                             → 200, chave preenchida    (métrica: encontrada)
usuário = recebedor                           → 200, chave nula          (métrica: encontrada)
```

Em todos os casos de 200, o documento do pagador sai mascarado e o do recebedor completo.

## Erro novo (Domain)

| Código | Tipo | HTTP | Mensagem (`detail`) |
| --- | --- | --- | --- |
| `transferencia-nao-encontrada` | `TipoErro.NaoEncontrado` | 404 | "Transferência não encontrada." |

## Métrica nova

| Nome | Tipo | Unidade | Rótulos |
| --- | --- | --- | --- |
| `bank.transferencias.consultas` | contador | `{consulta}` | `resultado` = `encontrada` · `nao-encontrada` · `invalida` |
