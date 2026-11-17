# Data Model: Usuários (PF/PJ), Carteiras e Autenticação

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-11-17

## Domínio

### Usuario (raiz de agregado)

| Atributo | Tipo | Regra |
| --- | --- | --- |
| `Id` | `Guid` (UUIDv7) | Gerado no Domain na criação |
| `NomeCompleto` | `string` | 1–150 caracteres após `Trim`; não pode ser só espaços |
| `Email` | `Email` (VO) | `Trim` + minúsculas; formato `local@dominio` válido; ≤ 150 caracteres |
| `Documento` | `Documento` (VO) | `Cpf` se PF, `Cnpj` se PJ (R2); valor normalizado sem máscara, maiúsculas |
| `TipoConta` | `TipoConta` | `PessoaFisica` \| `PessoaJuridica`; imutável (sem setter público, sem método de alteração) |
| `SenhaHash` | `string` | Produzido por `IHasherSenha`; a senha em texto nunca entra na entidade |
| `CriadoEm` | `DateTimeOffset` (UTC) | Fornecido por `TimeProvider` |
| `Carteira` | `Carteira` | Exatamente uma, criada junto com o usuário |

**Criação** — `Usuario.Criar(nome, email, documento, tipoConta, senhaHash, saldoInicial, agora)`:
cria o usuário e a carteira (com `saldoInicial` e `AtualizadoEm = agora`) em uma única
operação; não existe usuário sem carteira no modelo.

### Carteira

| Atributo | Tipo | Regra |
| --- | --- | --- |
| `Id` | `Guid` (UUIDv7) | Gerado no Domain |
| `UsuarioId` | `Guid` | Titular; 1:1 com `Usuario` |
| `Saldo` | `decimal` | ≥ 0; escala 2 |
| `Moeda` | `string` | `BRL` (ISO 4217) |
| `AtualizadoEm` | `DateTimeOffset` (UTC) | Igual a `CriadoEm` do usuário na criação |

Nenhuma operação de movimentação nesta spec; débitos e créditos chegam com a issue #8.

### Value objects e enums

| Tipo | Regra |
| --- | --- |
| `Cpf` | 11 dígitos, DV módulo 11, rejeita dígitos repetidos; aceita máscara `000.000.000-00` (R2) |
| `Cnpj` | 14 caracteres `[0-9A-Z]{12}[0-9]{2}`, DV com valor ASCII − 48, rejeita caracteres repetidos; aceita máscara `XX.XXX.XXX/XXXX-00` e minúsculas (R2) |
| `Documento` | Valida o documento conforme `TipoConta`; CPF com tipo PJ ou CNPJ com tipo PF → erro no campo `documento` |
| `Email` | Normalizado e validado; ≤ 150 caracteres |
| `PoliticaSenha` | 8–128 caracteres; maiúscula, minúscula, dígito e caractere especial (R3) |
| `TipoConta` | Parse sensível a maiúsculas: só `PessoaFisica` e `PessoaJuridica` (`pessoafisica`, `PF`, números → erro) |
| `Permissao` / `PermissoesPorPerfil` | PF: `RealizarTransferencia`, `ReceberTransferencia`, `PagarCobrancaCartao`; PJ: `ReceberTransferencia`, `GerarCobrancaCartao` (R5) |
| `Resultado`, `Resultado<T>`, `Erro` | Resultados tipados; `Erro` = código estável + mensagem + campo opcional (R6) |

## Persistência (PostgreSQL)

Migration inicial `CriarUsuariosECarteiras`.

### Tabela `usuarios`

| Coluna | Tipo | Restrições |
| --- | --- | --- |
| `id` | `uuid` | `PRIMARY KEY` (`pk_usuarios`) |
| `nome_completo` | `varchar(150)` | `NOT NULL` |
| `email` | `varchar(150)` | `NOT NULL`, `UNIQUE` (`ux_usuarios_email`) — armazenado em minúsculas |
| `documento` | `varchar(14)` | `NOT NULL`, `UNIQUE` (`ux_usuarios_documento`) — CPF: 11 dígitos; CNPJ: 14 caracteres alfanuméricos em maiúsculas |
| `tipo_conta` | `varchar(20)` | `NOT NULL`, `CHECK (tipo_conta IN ('PessoaFisica','PessoaJuridica'))` (`ck_usuarios_tipo_conta`) |
| `senha_hash` | `text` | `NOT NULL` |
| `criado_em` | `timestamptz` | `NOT NULL` |

### Tabela `carteiras`

| Coluna | Tipo | Restrições |
| --- | --- | --- |
| `id` | `uuid` | `PRIMARY KEY` (`pk_carteiras`) |
| `usuario_id` | `uuid` | `NOT NULL`, `UNIQUE` (`ux_carteiras_usuario_id`), `FK → usuarios(id) ON DELETE RESTRICT` (`fk_carteiras_usuarios`) |
| `saldo` | `numeric(18,2)` | `NOT NULL`, `CHECK (saldo >= 0)` (`ck_carteiras_saldo_nao_negativo`) |
| `moeda` | `char(3)` | `NOT NULL DEFAULT 'BRL'`, `CHECK (moeda = 'BRL')` (`ck_carteiras_moeda`) |
| `atualizado_em` | `timestamptz` | `NOT NULL` |
| `xmin` (sistema) | `xid` | Token de concorrência otimista (R7); não é coluna criada |

**Diferenças em relação à issue #5** (decorrentes da spec): `documento` aceita letras
maiúsculas (CNPJ alfanumérico); `carteiras.moeda` adicionada (constituição, Princípio IV);
constraints nomeadas e `CHECK` de `tipo_conta`.

## Configuração

| Chave | Padrão | Regra |
| --- | --- | --- |
| `ConnectionStrings:Bank` | SDK: `Host=localhost;Port=5432;Database=bank;Username=bank;Password=bank_dev_password` (`appsettings.Development.json`); Compose: host `postgres` | Obrigatória |
| `Persistencia:AplicarMigracoesNaInicializacao` | `false`; `true` em `Development` e `Testing` | R7 |
| `Carteiras:SaldoInicial:PessoaFisica` | `0.00` (base/`Production`); `10.00` em `Development` e `Testing` | ≥ 0, até 2 casas; MUST ser `0.00` em `Production` (R8) |
| `Carteiras:SaldoInicial:PessoaJuridica` | `0.00` | ≥ 0, até 2 casas; MUST ser `0.00` em `Production` (R8) |
| `Jwt:Emissor` | `bank-api` | Obrigatório |
| `Jwt:Audiencia` | `bank-api` | Obrigatório |
| `Jwt:ExpiracaoSegundos` | `3600` | > 0 |
| `Jwt:ChaveAssinatura` | — (segredo) | ≥ 32 bytes; user-secrets (SDK), `JWT_CHAVE_ASSINATURA` (Compose), aleatória (testes) — R4 |

Todas validadas na inicialização (`ValidateOnStart`): configuração inválida impede a API de
subir, com mensagem que identifica a chave.
