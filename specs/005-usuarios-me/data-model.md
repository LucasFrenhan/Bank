# Data Model: Dados do titular autenticado

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-11-17

Nenhuma tabela, coluna ou migration nova: leitura da tabela `usuarios` da
[spec 003](../003-usuarios-carteiras/data-model.md#tabela-usuarios).

## Visão `DadosTitular` (Application)

| Campo | Origem (`usuarios`) | Regra |
| --- | --- | --- |
| `UsuarioId` | `id` | UUIDv7 |
| `NomeCompleto` | `nome_completo` | Como armazenado (1–150) |
| `Email` | `email` | Normalizado (minúsculas) |
| `Documento` | `documento` | Sem máscara: CPF 11 dígitos; CNPJ 14 caracteres `[0-9A-Z]` |
| `TipoConta` | `tipo_conta` | `PessoaFisica` \| `PessoaJuridica` |
| `CriadoEm` | `criado_em` | `DateTimeOffset` em UTC |

Não inclui `senha_hash` nem dados da carteira.

## Erro novo (Domain)

| Código | Tipo | HTTP | Mensagem |
| --- | --- | --- | --- |
| `usuario-nao-encontrado` | `NaoEncontrado` | 404 | "Usuário não encontrado para o token informado." |
