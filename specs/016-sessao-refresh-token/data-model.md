# Data Model: Sessão renovável com refresh token rotativo

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md)

Novo conceito no Domain em `Bank.Domain/Sessoes/` (fronteira do futuro módulo de Identidade, #62). O
`Usuario` não muda.

## Identificadores tipados

| Tipo | Base | Geração |
| --- | --- | --- |
| `SessaoId` | `Guid` (UUIDv7) | `SessaoId.Novo(agora)` |
| `CredencialRenovacaoId` | `Guid` (UUIDv7) | `CredencialRenovacaoId.Novo(agora)` |

## Value objects e enums

- **`HashCredencial`**: 32 bytes (SHA-256). Criado só a partir do valor recebido ou gerado; nunca guarda o
  valor original. Igualdade por bytes.
- **`MotivoRevogacao`**: `Logout`, `Reuso`, `NovoLogin`.
- **`LimitesSessao`** (configuração em segundos, não persistido): `Inatividade` (900 s), `Absoluto` (8 h),
  `ToleranciaReuso` (30 s), `Retencao` (7 dias). Validação: todos > 0; `Inatividade` ≤ `Absoluto`;
  `ToleranciaReuso` < `Inatividade`.
- **`ResultadoRenovacao`** (Domain): `Renovada(nova credencial)`, `Recusada(motivo interno)`,
  `ReusoDetectado`. O motivo interno (`Revogada`, `Inativa`, `LimiteAbsoluto`, `Desconhecida`) vai só para
  log e métrica, nunca para a resposta HTTP.

## Entidade `Sessao` (raiz de agregado)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `Id` | `SessaoId` | PK |
| `UsuarioId` | `UsuarioId` | FK → `usuarios`, `ON DELETE CASCADE` |
| `IniciadaEm` | `DateTimeOffset` | login |
| `UltimaRenovacaoEm` | `DateTimeOffset` | login ou última renovação (base da inatividade) |
| `ExpiraEm` | `DateTimeOffset` | `IniciadaEm + Absoluto` (imutável) |
| `RevogadaEm` | `DateTimeOffset?` | preenchida uma única vez |
| `MotivoRevogacao` | `MotivoRevogacao?` | junto com `RevogadaEm` |
| `Credenciais` | coleção de `CredencialRenovacao` | carregada parcialmente (apresentada + ativa) |

**Comportamento**

- `Iniciar(usuarioId, hashInicial, agora, limites)` → sessão + primeira credencial.
- `Renovar(hashApresentado, novoHash, agora, limites)`:
  1. revogada → `Recusada(Revogada)`;
  2. `agora ≥ ExpiraEm` → `Recusada(LimiteAbsoluto)`;
  3. `agora ≥ UltimaRenovacaoEm + Inatividade` → `Recusada(Inativa)`;
  4. credencial apresentada é a ativa, ou foi rotacionada há ≤ `ToleranciaReuso` → rotaciona a **ativa**:
     marca `RotacionadaEm = agora`, `SubstitutaId = nova`; cria a nova com
     `ExpiraEm = min(agora + Inatividade, Sessao.ExpiraEm)`; `UltimaRenovacaoEm = agora`; → `Renovada`;
  5. senão (rotacionada há mais que a tolerância) → `Revogar(Reuso)` → `ReusoDetectado`.
- `Revogar(motivo, agora)`: idempotente; a primeira revogação vale.
- `PrazoInatividade(agora)` e `PrazoAbsoluto(agora)`: segundos restantes, usados na resposta.

**Estados**

```text
            Iniciar
              │
              ▼
           [Ativa] ──Renovar (ok)──► [Ativa]
              │
              ├─ Revogar(Logout | NovoLogin) ─► [Revogada]
              ├─ Renovar com credencial antiga fora da tolerância ─► [Revogada (Reuso)]
              └─ prazo de inatividade ou absoluto vencido ─► [Vencida] (derivado, sem gravação)
[Revogada] e [Vencida] ── após Retencao ──► removidas pela limpeza
```

## Entidade `CredencialRenovacao` (filha de `Sessao`)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `Id` | `CredencialRenovacaoId` | PK |
| `SessaoId` | `SessaoId` | FK → `sessoes`, `ON DELETE CASCADE` |
| `Hash` | `HashCredencial` | `bytea`, único |
| `EmitidaEm` | `DateTimeOffset` | |
| `ExpiraEm` | `DateTimeOffset` | `min(EmitidaEm + Inatividade, Sessao.ExpiraEm)`; define o `Max-Age` do cookie |
| `RotacionadaEm` | `DateTimeOffset?` | preenchida uma vez |
| `SubstitutaId` | `CredencialRenovacaoId?` | a credencial emitida na rotação |

Invariante: no máximo uma credencial com `RotacionadaEm` nulo por sessão (índice único parcial
`(sessao_id) WHERE rotacionada_em IS NULL`).

## Persistência (PostgreSQL)

Migration `AdicionarSessoes`:

```text
sessoes
  id uuid PK, usuario_id uuid NOT NULL FK usuarios(id) ON DELETE CASCADE,
  iniciada_em timestamptz NOT NULL, ultima_renovacao_em timestamptz NOT NULL, expira_em timestamptz NOT NULL,
  revogada_em timestamptz NULL, motivo_revogacao varchar(20) NULL,
  CHECK ((revogada_em IS NULL) = (motivo_revogacao IS NULL))
  INDEX (usuario_id); INDEX (expira_em); INDEX (revogada_em) WHERE revogada_em IS NOT NULL

credenciais_renovacao
  id uuid PK, sessao_id uuid NOT NULL FK sessoes(id) ON DELETE CASCADE,
  hash bytea NOT NULL UNIQUE CHECK (octet_length(hash) = 32),
  emitida_em timestamptz NOT NULL, expira_em timestamptz NOT NULL,
  rotacionada_em timestamptz NULL, substituta_id uuid NULL
  UNIQUE (sessao_id) WHERE rotacionada_em IS NULL
```

- `Sql/Consultas/BloquearSessao.sql`: `SELECT … FROM sessoes WHERE id = @id FOR UPDATE` (R3).
- Limpeza (LINQ, `ExecuteDeleteAsync`): sessões com `revogada_em < agora − Retencao` ou
  `expira_em < agora − Retencao` ou `ultima_renovacao_em + Inatividade < agora − Retencao`.

## Abstrações (Application)

| Abstração | Implementação | Uso |
| --- | --- | --- |
| `ISessaoRepositorio` | `SessaoRepositorio` | obter por hash (com bloqueio), adicionar, gravar |
| `IGeradorCredencial` | `GeradorCredencialAleatoria` | gera valor + `HashCredencial`; calcula hash de um valor recebido |
| `IEmissorToken` | `EmissorTokenJwt` | `Emitir(usuario, sessaoId)` — acrescenta `sid` |
| `ILimpezaSessoes` | `SessaoRepositorio` | remove sessões além da retenção |

## Casos de uso (Application, `Autenticacao/`)

| Caso de uso | Entrada | Saída |
| --- | --- | --- |
| `Autenticar` (alterado) | e-mail, senha, credencial atual (opcional) | token de acesso + credencial nova + prazos |
| `RenovarSessao` | credencial | token de acesso + credencial nova + prazos, ou falha `nao-autenticado` |
| `EncerrarSessao` | credencial (opcional; qualquer credencial da sessão, ativa ou já trocada) | sempre sucesso |
| `LimparSessoes` | — | quantidade removida |

## Frontend (estado, sem persistência de token)

| Item | Onde | Conteúdo |
| --- | --- | --- |
| Token de acesso | memória do módulo `tokenMemoria.ts` | JWT; some ao recarregar |
| `Sessao` (React) | `ContextoSessao` | claims, `estado` (`verificando` \| `ativa` \| `ausente`), prazos de inatividade e absoluto (instantes locais calculados a partir dos segundos recebidos) |
| Mensagens entre abas | `BroadcastChannel('bank.sessao')` | `entrou {sub}`, `renovou {inatividadeSeg, absolutoSeg}`, `saiu`, `expirou` — nunca o token |
| Dados da operação | `sessionStorage` (inalterado) | `bank.operacoes`, transferência em andamento; apagados só no fim da sessão |
| Chave antiga `bank.sessao` | `sessionStorage` | removida na primeira carga |
