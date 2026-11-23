# Research: Notificações de transferência persistidas (auditoria)

**Feature**: [spec.md](spec.md) | **Plano**: [plan.md](plan.md)

Ponto de partida: o desenho da spec 010 (ver
[research da 010](../010-mensageria-rabbitmq/research.md), R7–R10). O consumidor
`ConsumidorEventos` abre um escopo de DI, inicia a transação no `BankDbContext` desse escopo,
grava `eventos_processados` (`ON CONFLICT DO NOTHING`) e, se a linha foi inserida, chama
`ProcessarAsync(escopo.ServiceProvider, ...)`, que resolve `INotificadorTransferencia`
(implementado por `NotificarTransferenciaConcluida`, Application). O commit e o ACK vêm depois.
Esta spec só muda o que acontece dentro de `ProcessarAsync`.

## R1 — Onde fica a notificação de auditoria

**Decisão**: entidade de domínio `Bank.Domain/Notificacoes/NotificacaoAuditoria`, com
identificador tipado `NotificacaoAuditoriaId` (`IIdentificador<T>`, UUIDv7 por `Novo(agora)`),
enums `TipoNotificacao` (`ComprovanteEnvio`, `AvisoCredito`) e `CanalNotificacao` (`Simulado`).
A criação é feita por duas fábricas, `NotificacaoAuditoria.ComprovanteEnvio(...)` e
`NotificacaoAuditoria.AvisoCredito(...)`, que montam o texto (R5). A entidade não tem métodos de
alteração: só leitura depois de criada (FR-007).

**Motivo**: o texto e a escolha do destinatário são regra de negócio (Princípio II: regras no
Domain/Application); a constituição exige identificador tipado para entidades do domínio e o teste
`IdentificadoresTipadosTests` cobre o Domain automaticamente.

**Alternativas**: entidade na Infrastructure, como `EventoProcessado` (rejeitada:
`EventoProcessado` é detalhe técnico de idempotência, enquanto a notificação é um conceito de
negócio que o envio real vai reutilizar); texto montado na Application (rejeitada: espalharia a
regra do texto fora da entidade que a guarda).

## R2 — Persistência dentro da transação do consumidor

**Decisão**: nova abstração `Bank.Application/Abstracoes/INotificacoesAuditoria`:

```text
Task<bool> ExistemParaEventoAsync(EventoId eventoId, CancellationToken ct)
Task AdicionarAsync(IReadOnlyCollection<NotificacaoAuditoria> notificacoes, CancellationToken ct)
```

Implementada em `Bank.Infrastructure/Persistencia/NotificacoesAuditoriaRepositorio` (scoped) com o
`BankDbContext` do escopo: `AddRange` + `SaveChangesAsync`. Como o consumidor resolve o caso de uso
no **mesmo escopo** em que abriu a transação, o `SaveChangesAsync` participa da transação corrente
do `DbContext` (o EF Core usa a transação ativa da conexão) e o commit continua sendo do
`ConsumidorEventos`. `ConsumidorEventos` e `ConsumidorNotificacoesTransferencia` não mudam.

O registro em `AddPersistencia` (Infrastructure) torna o repositório disponível ao Worker; a API
também o recebe, mas não o usa.

**Motivo**: FR-005 (mesma transação de `eventos_processados`, ACK após o commit) sem tocar no
consumidor base, que já foi testado contra todas as falhas da spec 010.

**Alternativas**: o consumidor gravar as notificações direto (rejeitada: regra de negócio na
Infrastructure); SQL puro em `.sql` (desnecessário: `INSERT` simples via EF; a constituição pede
LINQ/EF por padrão); transação própria no repositório (quebraria a atomicidade com
`eventos_processados`).

## R3 — Idempotência em camadas

**Decisão**:

1. **Principal (existente)**: `eventos_processados` na mesma transação; duplicata → o efeito nem
   é chamado (spec 010, FR-017).
2. **Caso de uso**: `NotificarTransferenciaConcluida` consulta `ExistemParaEventoAsync`; se já há
   notificações do evento, não grava nada (log `Debug`, sem métrica). Isso torna o caso de uso
   idempotente por si só, o que é testável em teste unitário (FR-012).
3. **Banco**: índice único `ux_notificacoes_auditoria_evento_tipo (evento_id, tipo)` (FR-006).
   Se as duas primeiras camadas falharem (bug), a violação vira exceção, a transação é desfeita e a
   mensagem segue nova tentativa/DLQ (sem duplicar).

**Motivo**: FR-006 pede a restrição no banco; a checagem no caso de uso custa uma consulta por
índice e cobre o requisito do teste unitário de idempotência sem depender do broker.

**Alternativas**: só `eventos_processados` (rejeitada pela Clarification Q1); `ON CONFLICT DO
NOTHING` na inserção das notificações (esconderia um bug na camada 1 em vez de expô-lo).

## R4 — Nomes chegam ao caso de uso

**Decisão**: `NotificacaoTransferencia` ganha `string? PagadorNome` e `string? RecebedorNome`
(últimos parâmetros, opcionais). `LeitorTransferenciaConcluida` passa a ler `pagadorNome` e
`recebedorNome` do corpo (campos que o contrato v1 já tem desde a spec 010); ausente ou em branco
→ `null`. A ausência **não** torna a mensagem inválida (eventos antigos). A descrição continua
fora do comando (FR-004).

**Motivo**: Clarification Q3 (texto com o nome quando o evento trouxer). O contrato
`transferencia.concluida` v1 não muda (FR-009).

**Alternativas**: buscar os nomes atuais em `usuarios` no consumidor (rejeitada: o evento traz o
nome do momento da transferência, que é o que vale para o comprovante; consulta extra).

## R5 — Texto e formato do valor

**Decisão**: textos fixos em português, montados no Domain:

| Tipo | Com o nome da outra parte | Sem o nome |
| --- | --- | --- |
| `comprovante_envio` (pagador) | `Você enviou R$ {valor} para {recebedorNome}.` | `Você enviou R$ {valor}.` |
| `aviso_credito` (recebedor) | `Você recebeu R$ {valor} de {pagadorNome}.` | `Você recebeu R$ {valor}.` |

`{valor}` no formato brasileiro (`1.234,56`): a solução roda com `InvariantGlobalization=true`
(`Directory.Build.props`), então a cultura `pt-BR` não está disponível. O formato usa um
`NumberFormatInfo` explícito (separador decimal `,`, de milhar `.`, 2 casas) com o padrão
`#,##0.00`, independente da cultura do processo. Nome: `Trim()`, já limitado a 150 caracteres pela
validação de usuário; `mensagem` cabe em `varchar(500)`.

**Motivo**: FR-004 e Edge Cases (valor em formato brasileiro); resultado determinístico em
Windows, no runner Linux do CI e no contêiner Alpine.

**Alternativas**: `CultureInfo("pt-BR")` (falha ou cai na cultura invariante com
`InvariantGlobalization`); desligar `InvariantGlobalization` (aumenta a imagem com ICU e muda o
comportamento de toda a solução por causa de um texto).

## R6 — Log e métrica existentes

**Decisão**: o log estruturado por destinatário (`EventId` 5100) e a métrica
`bank.notificacoes.enviadas` (tag `papel`) são mantidos, sem nomes nem texto (FR-009, FR-010). A
métrica passa a ser incrementada **depois** do `AdicionarAsync` (gravação feita, faltando só o
commit do consumidor). Se o commit falhar depois disso, a métrica conta a mais naquela execução;
aceito e documentado, porque a métrica de resultado do consumo (`banco.eventos.processados`) é a
fonte de verdade de sucesso e a contagem a mais só ocorre com falha no próprio commit.

**Motivo**: ponto deixado em aberto no clarify; contar depois do commit exigiria um gancho
pós-commit no consumidor base só para esta métrica.

**Alternativas**: gancho pós-commit no `ConsumidorEventos` (complexidade sem ganho real);
remover a métrica (quebra o painel e a spec 010).

## R7 — Migration e modelo relacional

**Decisão**: migration `AdicionarNotificacoesAuditoria` (aplicada pela API na partida, como as
demais; spec 010, R9). Tabela, chave estrangeira `RESTRICT` para `usuarios`, índice único e
`CHECK` de `tipo` e `canal` descritos em [data-model.md](data-model.md). Enums gravados como texto
`snake_case` por conversor na configuração (`NotificacaoAuditoriaConfiguracao`).
`NotificacaoAuditoriaId` registrado em `ConfigureConventions` com `IdentificadorConverter`.

**Motivo**: FR-008 e Clarification Q4. O nome da issue (`CriacaoTabelasMensageriaEAuditoria`)
não é usado porque `eventos_processados` já foi criada por `AdicionarMensageria`.

**Alternativas**: enums como inteiro (ilegível na auditoria feita direto no banco); sem `CHECK`
(valores livres na coluna).

## R8 — Testes

**Decisão**:

- **Unitários** (`Bank.UnitTests`):
  - `Domain/NotificacaoAuditoriaTests`: fábricas (tipo, destinatário, canal, texto com e sem nome,
    formato `1.234,56`, `0,01`, nome com espaços nas extremidades).
  - `Application/NotificarTransferenciaConcluidaTests` (ajustado): grava duas notificações; mesmo
    `eventoId` duas vezes → um único par (repositório em memória que respeita
    `ExistemParaEventoAsync`); log e métrica mantidos, sem nomes.
  - `Infrastructure/LeitorTransferenciaConcluidaTests` (ajustado): lê os nomes; ausentes/brancos →
    `null`; mensagem continua válida.
- **Integração** (`Bank.IntegrationTests`, Testcontainers):
  - `PublicacaoOutboxTests` (ajustado): ponta a ponta `POST /api/v1/transferencias` → duas linhas
    em `notificacoes_auditoria` com tipos, destinatários e textos esperados.
  - `ConsumoIdempotenteTests` (ajustado): mesma mensagem duas vezes e cópias simultâneas em dois
    Workers → exatamente duas linhas.
  - `NotificacoesAuditoriaTests` (novo): falha na gravação da 2ª notificação → nada gravado e nova
    tentativa; falha permanente → DLQ sem linhas; reprocessamento da DLQ → duas linhas;
    destinatário inexistente → DLQ (`tentativas-esgotadas`); índice único rejeita duplicata
    inserida à mão; banco recusa `DELETE` de usuário com notificações (usuário de teste criado sem
    carteira, para que só a nova chave estrangeira bloqueie).
  - `DadosSensiveisMensageriaTests` (ajustado): nomes e texto da notificação não aparecem em logs,
    spans nem métricas.
- **Dados de teste da 010**: o `Payload` dos testes de integração da spec 010 usa ids de usuário
  aleatórios; com a FK `RESTRICT` (R7), o consumo real falharia e a mensagem iria para a DLQ. Os
  payloads passam a usar um par de usuários criados no banco (`UsuariosDeTeste`).
- **Arquitetura**: cobertura automática pelos testes existentes (Domain sem infraestrutura,
  identificadores tipados).

Para a falha na 2ª notificação, um dublê de `INotificacoesAuditoria` que grava a 1ª e lança na 2ª
(via ambiente do Worker com serviço substituído, como o `AmbienteNotificadorControlavel` da 010).

**Motivo**: FR-012, FR-013 e Princípio VII.
