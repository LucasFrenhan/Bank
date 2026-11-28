# Guia de desenvolvimento

Como executar, testar e evoluir o projeto Bank. As regras obrigatórias do projeto estão na
[constituição](../.specify/memory/constitution.md).

## Ambiente local

Infraestrutura de apoio ao desenvolvimento, definida em
[`specs/001-infra-local`](../specs/001-infra-local/spec.md).

**Pré-requisito:** Docker Desktop (Windows/macOS) ou Docker Engine (Linux) em execução,
com Docker Compose v2.

```bash
# (opcional) personalizar portas e credenciais — sem .env, valem os mesmos padrões
cp .env.example .env

# subir e aguardar até todos os serviços estarem saudáveis
docker compose up -d --wait

# ver o estado (todos devem estar "(healthy)")
docker compose ps

# parar mantendo os dados
docker compose down

# reset: parar e apagar todos os dados
docker compose down -v
```

| Serviço | Endereço | Credenciais padrão |
| --- | --- | --- |
| PostgreSQL | `localhost:5432`, banco `bank` | `bank` / `bank_dev_password` |
| RabbitMQ (AMQP) | `localhost:5672`, vhost `bank` | `bank` / `bank_dev_password` |
| RabbitMQ Management | <http://localhost:15672> | `bank` / `bank_dev_password` |
| Aspire Dashboard (traces, métricas e logs) | <http://localhost:18888> | sem login |
| OTLP gRPC / HTTP | `http://localhost:4317` / `http://localhost:4318` | sem autenticação |
| API (`/health`, `/scalar/v1`) | <http://localhost:8080> | sem autenticação |
| Worker de mensageria (`/health/pronto`) | sem porta publicada (`docker compose ps worker`) | usa as credenciais do RabbitMQ |
| Frontend | <http://localhost:3000> | conta criada na própria tela |

- O `docker compose up -d --wait` também constrói e sobe a API e o frontend (use `--build` após alterar
  o código). Para subir só a infraestrutura e rodar a API pelo SDK:
  `docker compose up -d --wait postgres rabbitmq aspire-dashboard`.
- As portas escutam apenas em `127.0.0.1` e podem ser alteradas no `.env`, por exemplo
  `POSTGRES_PORT=55432` se a 5432 já estiver ocupada.
- As credenciais são exclusivamente para desenvolvimento local.
- A telemetria do Aspire Dashboard fica só em memória e é perdida ao reiniciar o contêiner.
- Strings de conexão, endpoints e garantias do ambiente:
  [contrato do ambiente local](../specs/001-infra-local/contracts/ambiente-local.md).
- Roteiro completo de validação: [quickstart](../specs/001-infra-local/quickstart.md).

## Backend (API)

Solução .NET 10 em `src/backend/Bank.slnx`, definida em
[`specs/002-setup-api`](../specs/002-setup-api/spec.md): camadas `Bank.Api`,
`Bank.Application`, `Bank.Domain` e `Bank.Infrastructure` (núcleo antigo), o Shared Kernel
(`src/backend/src/SharedKernel/`, spec 017) e os projetos de teste unitário, de integração e de
arquitetura.

**Pré-requisito:** SDK do .NET 10 (`dotnet --version` na raiz deve mostrar `10.0.4xx`; o
`global.json` fixa a versão).

```bash
# ferramentas locais (dotnet-ef, fixado em .config/dotnet-tools.json)
dotnet tool restore

# compilar e executar todos os testes (os de integração exigem Docker: PostgreSQL via Testcontainers)
dotnet build src/backend/Bank.slnx
dotnet test --solution src/backend/Bank.slnx

# rodar a API pelo SDK (com o ambiente local no ar: banco e telemetria)
docker compose up -d --wait postgres rabbitmq aspire-dashboard
dotnet user-secrets set "Jwt:ChaveAssinatura" "$(openssl rand -base64 48)" --project src/backend/src/Bank.Api  # uma vez
dotnet user-secrets set "Cartoes:ChaveDerivacao" "$(openssl rand -base64 48)" --project src/backend/src/Bank.Api  # uma vez
dotnet run --project src/backend/src/Bank.Api
```

| Recurso | SDK (`dotnet run`) | Compose |
| --- | --- | --- |
| Saúde | <http://localhost:5080/health> | <http://localhost:8080/health> |
| Documentação interativa (Scalar) | <http://localhost:5080/scalar/v1> | <http://localhost:8080/scalar/v1> |
| Documento OpenAPI | <http://localhost:5080/openapi/v1.json> | <http://localhost:8080/openapi/v1.json> |

- A documentação (Scalar e OpenAPI) só existe fora do ambiente `Production`.
- Toda resposta de erro usa `ProblemDetails` com `traceId`, que pode ser buscado no
  Aspire Dashboard (<http://localhost:18888>), onde a API aparece como `bank-api`.
- Outra porta pelo SDK: `dotnet run --project src/backend/src/Bank.Api --urls http://localhost:5090`.
- Garantias de erro, endpoints e telemetria:
  [contrato da API base](../specs/002-setup-api/contracts/api-base.md).

### Módulos e fronteiras

O backend está migrando do monólito em camadas para o **monólito modular** (constituição v1.3.0,
Princípio VIII; issue #58). A fundação está em
[`specs/017-modulos-fundacao`](../specs/017-modulos-fundacao/spec.md). Enquanto a migração não termina,
os projetos `Bank.Domain`, `Bank.Application` e `Bank.Infrastructure` formam o **núcleo antigo** e
convivem com os módulos.

- **Shared Kernel** (`src/backend/src/SharedKernel/`):
  - `Bank.SharedKernel` não tem nenhuma dependência e contém:
    - `Dinheiro`, `Erro`, `Resultado`, `IIdentificador` e `EventoId`;
    - `CalendarioBancario` (namespace `.Calendario`);
    - `Paginacao`, `PaginaDe`, `Entradas` e `ErrosIdempotencia` (namespace `.Aplicacao`).
  - `Bank.SharedKernel.Infrastructure` contém só o `IdentificadorConverter` e só pode ser usado por
    Infrastructure.
  - Não entram no kernel:
    - os identificadores de cada contexto (`UsuarioId`, `CarteiraId`…), que ficam no contexto dono;
    - `IUnidadeDeTrabalho`, cujo resultado tem casos de cartão e cobrança.
  - Para incluir algo no kernel, a justificativa vai no `plan.md` da spec que o inclui.
- **Formato de módulo**: em `src/backend/src/Modulos/<Nome>/`, cada módulo tem dois projetos:
  - `Bank.Modulos.<Nome>`, com Domain, Application, Infrastructure e Endpoints. Tudo é `internal`, exceto
    a classe de registro `Modulo<Nome>`.
  - `Bank.Modulos.<Nome>.Contratos`, público.

  Para incluir um módulo, adicione uma linha no `Modulos/RegistroModulos.cs` da API e outra no do Worker,
  e siga o [contrato do formato de módulo](../specs/017-modulos-fundacao/contracts/formato-modulo.md).
  O módulo de exemplo `Alfa`, em `tests/Bank.ArchitectureTests.Exemplos`, mostra o formato completo.
- **Regras de fronteira (MOD-01 a MOD-07)** ficam em `tests/Bank.ArchitectureTests/Modulos/` e valem
  para todo namespace `Bank.Modulos.<Nome>`, sem configuração por módulo. As regras proíbem:
  - módulo usar algo de outro fora do contrato público, ou a persistência de outro;
  - módulo depender do núcleo antigo;
  - módulo ferir as camadas do Princípio II;
  - Shared Kernel depender de módulos ou frameworks;
  - host ou núcleo antigo usar o interno de um módulo;
  - domínio usar a infraestrutura comum.

  A falha lista uma linha por violação, no formato `MOD-0X <descrição>: <Origem> → <Destino>`. A tabela
  completa está no [contrato das regras](../specs/017-modulos-fundacao/contracts/regras-fronteira.md).
- **Exceções conhecidas** (`tests/Bank.ArchitectureTests/Modulos/ExcecoesConhecidas.cs`):
  - Cada entrada tem regra, origem, destino, motivo e issue no formato `#N`.
  - A lista começa vazia e só diminui. Nenhum PR acrescenta entradas (constituição, Princípio VII):
    quem extrai um módulo corrige no próprio PR as violações que aparecerem.
  - Uma entrada sem violação correspondente quebra o build ("exceção obsoleta"). Remova a entrada no
    mesmo PR que corrige a violação.
  - Uma regra com falso positivo é corrigida na própria regra, com um exemplo em `Violacoes/`. A regra
    nunca é desligada.

### Usuários, carteiras e autenticação

Cadastro (`POST /api/v1/autenticacao/registrar`), login com JWT
(`POST /api/v1/autenticacao/login`) e consulta da carteira (`GET /api/v1/carteiras/minha`),
definidos em [`specs/003-usuarios-carteiras`](../specs/003-usuarios-carteiras/spec.md) —
[contrato](../specs/003-usuarios-carteiras/contracts/usuarios-carteiras-api.md) e
[roteiro de validação](../specs/003-usuarios-carteiras/quickstart.md).

- **Chave JWT** (obrigatória; a API não sobe sem ela, com ≥ 32 bytes): pelo SDK, via
  `dotnet user-secrets` (comando acima); no Compose, `JWT_CHAVE_ASSINATURA` do `.env`, com
  um padrão só de desenvolvimento em `.env.example`.
- **Banco**: fora de `Production`, as migrations são aplicadas automaticamente ao subir a
  API (`Persistencia:AplicarMigracoesNaInicializacao`). Para criar uma migration:

  ```bash
  dotnet ef migrations add NomeDaMigration --project src/backend/src/Bank.Infrastructure \
    --startup-project src/backend/src/Bank.Api --output-dir Persistencia/Migrations
  ```

- **Saldo de demonstração**: fora de `Production`, contas PF nascem com R$ 10,00 e PJ com
  R$ 0,00 (`Carteiras:SaldoInicial`); em `Production` ambos são R$ 0,00 e a API recusa subir
  com outro valor. Os dados de carteiras locais existem só para testes.
- No Scalar, use **Authorize** (esquema `Bearer`) com o `tokenAcesso` do login para chamar
  os endpoints protegidos.
- **Dados do titular**: `GET /api/v1/usuarios/me` devolve nome, e-mail, documento (sem
  máscara), tipo de conta e data de cadastro do usuário do token, com `Cache-Control: no-store`
  — [contrato](../specs/005-usuarios-me/contracts/usuarios-me-api.md) (spec 005).
- **Dados bancários** ([spec 012](../specs/012-dados-bancarios-carteira/spec.md)): toda carteira
  tem banco, agência, número de 8 dígitos com dígito verificador e tipo, expostos em
  `contaBancaria` de `GET /api/v1/carteiras/minha`
  ([contrato 1.1.0](../specs/012-dados-bancarios-carteira/contracts/carteira-minha.md)).
  - Bancos fixos (tabela `bancos`): 001 One Bank (o nosso), 002 Banco Aurora, 003 Banco
    Horizonte, 004 Banco Atlântico e 005 Banco Cerrado (os quatro últimos são simbólicos).
  - O cadastro cria sempre **conta corrente** no 001, agência 0001, com o próximo número da
    sequence `seq_numero_conta_one_bank` (lacunas são normais quando um cadastro falha).
  - Dígito verificador: Luhn (módulo 10) sobre agência + número, em C#
    (`ContaBancaria.CalcularDigito`) e na function SQL `calcular_digito_conta`, conferida por
    `CHECK` nas tabelas.
  - Contas de outros bancos e conta poupança só por script (reexecutáveis), na raiz do
    repositório:

    ```bash
    docker compose exec -T postgres psql -U bank -d bank -v ON_ERROR_STOP=1 < scripts/sql/dados-bancarios/registrar-contas-outros-bancos.sql
    docker compose exec -T postgres psql -U bank -d bank -v ON_ERROR_STOP=1 < scripts/sql/dados-bancarios/criar-conta-poupanca.sql
    ```

    O segundo cria o titular `poupanca.demo@exemplo.com.br` (senha `Poupanca@123`) com conta
    poupança no 001. As contas dos bancos 002–005 ficam em `contas_externas`, sem login nem saldo.

### Transferências e extrato

Definidos em [`specs/006-transferencias`](../specs/006-transferencias/spec.md) —
[contrato](../specs/006-transferencias/contracts/transferencias-api.md),
[evento](../specs/006-transferencias/contracts/evento-transferencia-concluida.md) e
[roteiro de validação](../specs/006-transferencias/quickstart.md).

- **Transferir** (`POST /api/v1/transferencias`, só PF): corpo `documentoDestino` (CPF ou CNPJ,
  com ou sem máscara), `valor` (≥ 0,01, até 2 casas) e `descricao` opcional (até 200). O
  cabeçalho **`Idempotency-Key`** (UUID gerado pelo cliente) é obrigatório: reenviar com a mesma
  chave e os mesmos destino e valor devolve `200` com o comprovante original, sem novo débito;
  com outro destino ou valor, `409`. Saldo insuficiente e autotransferência → `422`.
- **Consultar destinatário** (`POST /api/v1/transferencias/destinatarios/consultar`, só PF):
  confirma nome e tipo de conta antes de enviar. O documento vai no corpo, nunca na URL.
- **Extrato** (`GET /api/v1/transferencias/extrato`, PF e PJ): `pagina` (padrão 1),
  `tamanhoPagina` (padrão 20, máximo 100) e `direcao` (`Entrada`/`Saida`), mais recente
  primeiro; CPF de terceiros mascarado, CNPJ completo.
- **Consultar transferência** (`GET /api/v1/transferencias/{transferenciaId}`, PF e PJ —
  [spec 008](../specs/008-consulta-transferencia/spec.md)): reabre o comprovante. Só o pagador e
  o recebedor enxergam; para qualquer outra conta, a resposta é o mesmo `404
  transferencia-nao-encontrada` de um identificador inexistente. Para o recebedor,
  `chaveIdempotencia` vem `null`. Identificador que não é UUID → `400`.
- **Comprovante** (envio e consulta, contrato v1.1.0): traz `pagador.documento` com o CPF
  mascarado (`•••.982.247-••`) e `criadoEm` com precisão de microssegundos, igual ao gravado.
- **Limites** ([spec 009](../specs/009-limite-transferencia/spec.md), contrato v1.2.0): PF tem
  limite por transferência (padrão R$ 1.000,00) e diário (padrão R$ 5.000,00, soma das
  transferências enviadas no dia civil de **Brasília**, que vira à meia-noite local). Acima de um
  deles → `422 limite-excedido`, com `tipoLimite` (`PorOperacao`/`Diario`), `limite` e
  `valorDisponivel`; o limite é verificado antes do saldo, e um reenvio idempotente não passa por
  ele. `GET /api/v1/transferencias/limites` (só PF) devolve limites, transferido e disponível
  hoje.
  - Configuração `Transferencias:Limites:PessoaFisica:PorOperacao` e `...:Diario`; valor vazio
    desliga o limite. No Compose, `LIMITE_PF_POR_OPERACAO` e `LIMITE_PF_DIARIO` do `.env` (vazia
    desliga; não definida usa o padrão). Para ver a recusa com o saldo de demonstração:
    `LIMITE_PF_POR_OPERACAO=4.00 LIMITE_PF_DIARIO=6.00 docker compose up -d --build --wait`.
  - O dia de Brasília usa o fuso `America/Sao_Paulo` (imagem com `tzdata`) ou, no Windows,
    `E. South America Standard Time`; sem nenhum dos dois, a API não sobe.
- **Razão**: transferências e lançamentos são imutáveis (trigger no banco) e toda carteira tem
  saldo = soma dos lançamentos. O evento `transferencia.concluida` é gravado em `outbox_eventos`
  na mesma transação e publicado pelo Worker (ver "Mensageria", abaixo).
- **Abertura**: o saldo de demonstração vira um lançamento `Abertura` no cadastro; fora de
  `Production`, ao subir a API, carteiras antigas sem abertura a recebem
  (`Persistencia:CriarLancamentosDeAberturaNaInicializacao`).
- **SQL puro** (bloqueio `FOR UPDATE`, trigger, abertura) fica em arquivos `.sql` em
  `src/backend/src/Bank.Infrastructure/Persistencia/Sql/`, embutidos no assembly (constituição
  v1.1.0).

### Cartão de crédito, cobranças, faturas e recebíveis

Definidos em [`specs/014-cartao-credito-cobrancas`](../specs/014-cartao-credito-cobrancas/spec.md) —
[contrato](../specs/014-cartao-credito-cobrancas/contracts/cartoes-cobrancas-api.md),
[evento](../specs/014-cartao-credito-cobrancas/contracts/evento-cobranca-paga.md) e
[roteiro de validação](../specs/014-cartao-credito-cobrancas/quickstart.md). Sem telas por enquanto.

- **Cartão** (`/api/v1/cartoes`, só PF): um por CPF, com a bandeira escolhida (001 Visa, 002
  Mastercard) e limite de `Cartoes:LimitePadrao`. `GET /meu/dados` devolve nome, número, validade e
  CVV ao titular, com `no-store`.
- **Chave de derivação** (obrigatória; a API não sobe sem ela, com ≥ 32 bytes): número e CVV nunca são
  gravados; são derivados de `Cartoes:ChaveDerivacao` (user-secrets no SDK;
  `CARTOES_CHAVE_DERIVACAO` no Compose). **Trocar a chave muda os dados de todos os cartões**, e os já
  emitidos passam a falhar com erro 500 ao exibir ou pagar.
- **Cobranças** (`/api/v1/cobrancas`): a PJ emite, lista e cancela; a PF paga com o próprio cartão
  (`Idempotency-Key` obrigatório). A bandeira simulada recusa com `422`: `cartao-nao-autorizado`,
  `cartao-bloqueado-fatura-vencida` ou `limite-cartao-insuficiente`. Pagar não muda saldos.
- **Faturas** (`/api/v1/faturas/{AAAA-MM}`, só PF): fecham no dia 07, vencem no dia 10 ou no próximo
  dia útil e são pagas pelo total com o saldo. Atraso: juros simples por dia
  (`Cartoes:JurosAtrasoMensalPercentual` ÷ 30).
- **Recebíveis** (`/api/v1/recebiveis`, só PJ): valor integral no 30º dia útil ou antecipado na hora
  com taxa de 2%.
- **Processamento diário** (Worker): na partida, à 00:00 de Brasília e a cada
  `ProcessamentoDiario:IntervaloSegundos` (60), só em dias úteis: lança as compras de dias anteriores,
  libera as faturas fechadas e credita os recebíveis devidos. Repetir, atrasar ou rodar em várias
  instâncias não duplica. Desligar: `ProcessamentoDiario:Habilitado=false`.
- **Dias úteis**: `CalendarioBancario` (Domain), calculado pela Páscoa; sem cadastro de feriados.
- **Métricas**: `bank.cartoes.*`, `bank.cobrancas.*`, `bank.faturas.*`, `bank.recebiveis.*` (API) e
  `bank.processamento.*` (Worker).

### Mensageria (Worker)

Definida em [`specs/010-mensageria-rabbitmq`](../specs/010-mensageria-rabbitmq/spec.md) —
[topologia e mensagens](../specs/010-mensageria-rabbitmq/contracts/topologia-e-mensagens.md),
[evento](../specs/010-mensageria-rabbitmq/contracts/evento-transferencia-concluida.md) e
[roteiro de validação](../specs/010-mensageria-rabbitmq/quickstart.md).

- **Processo**: `src/backend/src/Bank.Worker` (serviço `worker` do Compose) hospeda o
  despachante do outbox, os consumidores de notificações e o processamento diário; a API não fala com o broker. Pelo SDK:
  `dotnet run --project src/backend/src/Bank.Worker` (porta 5081), com a API já tendo aplicado
  as migrations.
- **Publicação**: a cada 1 s o despachante reserva até 50 eventos pendentes de `outbox_eventos`
  (`FOR UPDATE SKIP LOCKED`), publica em `banco.eventos.topic` com `mandatory` e confirmação do
  broker e só então preenche `publicado_em`. Com o broker fora do ar, as transferências seguem
  concluindo e os eventos esperam no outbox.
- **Consumo**: `notificacoes.transferencias.fila` e `notificacoes.cobrancas.fila` (spec 014, com as
  próprias filas de espera e DLQ); cada evento é registrado em
  `eventos_processados` na mesma transação da notificação, então entregas duplicadas são
  descartadas.
- **Notificações** ([spec 011](../specs/011-notificacoes-auditoria/spec.md),
  [contrato](../specs/011-notificacoes-auditoria/contracts/notificacoes-auditoria.md)): cada
  transferência gera duas linhas em `notificacoes_auditoria`, o comprovante de envio (pagador) e
  o aviso de crédito (recebedor), no canal `simulado` (não há envio real), além de um log
  estruturado sem nomes. Para consultar:

  ```sql
  SELECT tipo, usuario_destino_id, mensagem, criado_em
  FROM notificacoes_auditoria ORDER BY criado_em DESC, tipo LIMIT 10;
  ```
- **Falhas**: 3 novas tentativas pelas filas `notificacoes.transferencias.espera.{1000,2000,4000}`;
  depois, ou se a mensagem for inválida, ela vai para `notificacoes.transferencias.dlq` com os
  headers `x-tentativas`, `x-motivo`, `x-ultimo-erro` e `x-falhou-em`.
- **Reprocessar a DLQ**: na interface de gestão (<http://localhost:15672>), Queues →
  `notificacoes.transferencias.dlq` → *Get messages* para inspecionar e *Move messages* para
  `notificacoes.transferencias.fila` depois de corrigir a causa. A contagem de tentativas
  recomeça e `x-reprocessamentos` é incrementado.
- **Trace ponta a ponta**: no Aspire Dashboard, o trace do `POST /api/v1/transferencias` mostra
  `banco.eventos.topic publish` e `notificacoes.transferencias.fila process` (serviço
  `bank-worker`), mesmo quando a publicação acontece depois (o contexto W3C fica no outbox).
- **Configuração** (seção `Mensageria`): `Host`, `Porta`, `VirtualHost`, `Usuario`, `Senha`,
  `IntervalosNovaTentativaMs`, `Prefetch`, `IntervaloDespachoMs`, `TamanhoLoteDespacho` e
  `TimeoutConfirmacaoMs`. No Compose, vêm das variáveis `RABBITMQ_*` do `.env`.

## Frontend

Aplicação React + TypeScript em `src/frontend/`, definida em
[`specs/004-frontend-base`](../specs/004-frontend-base/spec.md) —
[contrato](../specs/004-frontend-base/contracts/frontend.md) e
[roteiro de validação](../specs/004-frontend-base/quickstart.md). Telas de login, cadastro
PF/PJ, início e perfil, em desktop e mobile, seguindo o [protótipo](design/prototipo.md);
transferência, comprovante e extrato vêm da [spec 007](#transferências-comprovante-e-extrato-frontend).

**Pré-requisito:** Node.js 26.13.0 (`src/frontend/.nvmrc`). Para só usar o frontend, basta o
Compose: <http://localhost:3000>.

```bash
cd src/frontend
npm ci

# desenvolvimento: http://localhost:5173, com /api → API pelo SDK (5080) e /otlp → coletor (4318)
npm run dev
# usando a API do Compose em vez do SDK:
BANK_API_URL=http://localhost:8080 npm run dev

# qualidade
npm run lint && npm run typecheck && npm test && npm run build

# ponta a ponta (desktop e mobile, API real): Compose no ar com os prazos curtos da sessão
# (na raiz: docker compose --env-file .env.e2e.example up -d --build --wait)
npx playwright install chromium   # uma vez
npm run e2e                        # contra http://localhost:3000 (E2E_BASE_URL muda o alvo)

# tipos da API a partir do OpenAPI (API no ar; BANK_API_URL muda a origem)
npm run api:tipos
```

- A API e o coletor são acessados pela mesma origem: o proxy do Vite (desenvolvimento) e o
  nginx (Compose) encaminham `/api` e `/otlp`. A API não tem CORS.
- **Sessão** ([spec 016](../specs/016-sessao-refresh-token/spec.md)): o token de acesso (5 min) fica só em
  memória; a credencial de renovação vai no cookie `bank_renovacao` (`HttpOnly; Secure; SameSite=Strict`,
  só em `/api/v1/autenticacao`), e o banco guarda apenas o SHA-256 dela. Um 401 renova a sessão e repete a
  chamada uma vez; recarregar a página renova antes de mostrar a tela. A sessão vale para todas as abas,
  vence após 15 min sem renovação ou 8 h após o login, e "Sair" a revoga no servidor. O aviso "Sua sessão
  vai expirar" aparece 60 s antes (ou na metade do prazo, se ele for menor que 2 min).
- **Prazos da sessão** (`.env`): `JWT_EXPIRACAO_SEGUNDOS`, `SESSAO_INATIVIDADE_SEGUNDOS`,
  `SESSAO_TOLERANCIA_REUSO_SEGUNDOS` e `SESSAO_LIMITE_ABSOLUTO_HORAS`. Para ver a renovação e o aviso sem
  esperar, e para rodar o e2e, use os valores de `.env.e2e.example` (token de 10 s, inatividade de 25 s,
  tolerância de 5 s), os mesmos do CI. O Worker remove as sessões encerradas há mais de 7 dias.
- No Compose a telemetria do navegador está ligada: o frontend aparece no Aspire Dashboard
  como `bank-frontend`, no mesmo trace das chamadas à `bank-api`. Em produção ela vem
  desligada (`VITE_TELEMETRIA_HABILITADA`).
- Métricas no Aspire Dashboard (<http://localhost:18888/metrics>): a tela abre sem recurso
  selecionado — escolha `bank-api` (meter `Bank.Usuarios`: cadastros e logins) ou
  `bank-frontend` (carregamento e navegações).
- Depois de alterar o frontend, reconstrua a imagem: `docker compose up -d --build frontend`.

### Transferências, comprovante e extrato (frontend)

Definidos em [`specs/007-frontend-transferencias`](../specs/007-frontend-transferencias/spec.md) —
[contrato do frontend v1.1.0](../specs/007-frontend-transferencias/contracts/frontend.md) e
[roteiro de validação](../specs/007-frontend-transferencias/quickstart.md).

| Rota | Tela |
| --- | --- |
| `/transferir` | Etapa Dados (só PF; PJ vê "Indisponível para Pessoa Jurídica") |
| `/transferir/confirmar` | Confirmação e envio |
| `/transferencias/:id` | Comprovante |
| `/extrato?direcao=entradas\|saidas&pagina=N` | Extrato (PF e PJ) |

- **Chave de idempotência**: gerada no navegador ao chegar à confirmação e guardada com a
  transferência em andamento em `sessionStorage` (`bank.transferencia`). Reenvios dos mesmos dados
  (duplo clique, falha de rede, recarregar, voltar do navegador) usam a mesma chave; mudar
  destinatário, valor ou descrição gera outra. Os comprovantes recebidos na aba ficam em
  `bank.comprovantes`; tudo é apagado ao sair ou quando a sessão expira.
- **Limites** (spec 009, frontend v1.2.0): a etapa Dados lê `GET /limites` e mostra
  "Limite agora: R$ …" na dica do valor; acima dele, "Continuar" não avança e mostra o aviso
  "Limite excedido" (o mesmo da recusa da API na confirmação).
- **Dinheiro em centavos**: valores digitados e saldos são convertidos para centavos inteiros
  (`src/formatacao/valor.ts`); nenhuma conta de dinheiro usa ponto flutuante.
- **Comprovante depois**: todo lançamento de transferência do extrato e do início leva ao
  comprovante, carregado pela consulta `GET /api/v1/transferencias/{id}` (spec 008) quando não
  está guardado na aba — para pagador e recebedor, em qualquer sessão.
- **Saldo para testar**: toda PF começa com R$ 10,00 de demonstração. Para ter mais, crie outras
  PF e transfira os R$ 10,00 delas para a conta de teste. Os testes ponta a ponta fazem o mesmo
  pela API (`acumularSaldo` e `criarLancamentos` em `src/frontend/e2e/apoio.ts`), sem acessar o
  banco.

### Dados da conta e nome One Bank (frontend)

Definidos em [`specs/013-frontend-dados-bancarios`](../specs/013-frontend-dados-bancarios/spec.md) —
[contrato do frontend v1.3.0](../specs/013-frontend-dados-bancarios/contracts/frontend.md) e
[roteiro de validação](../specs/013-frontend-dados-bancarios/quickstart.md).

- O início (PF e PJ) mostra o cartão "Dados da conta" com o `contaBancaria` de
  `GET /api/v1/carteiras/minha`, sempre como texto, e copia só os dados bancários. Para ver uma
  **conta poupança**, use o script `criar-conta-poupanca.sql` descrito acima.
- O nome do banco na interface vem de `NOME_BANCO` (`src/marca.ts`); `src/marca.test.ts` falha se
  o nome antigo voltar. O nome ao lado do código do banco vem da API.
- Agência e conta nunca vão para a telemetria do navegador.

## Fluxo de desenvolvimento (SDD)

1. Crie a branch da spec a partir de `main` atualizada, com o mesmo nome da pasta da
   spec: `NNN-nome-curto` (ex.: `002-setup-api`).
2. Especifique: `/speckit-specify` (e `/speckit-clarify` se houver ambiguidades).
3. Planeje: `/speckit-plan` — o plano passa pelo *Constitution Check*.
4. Gere as tarefas: `/speckit-tasks` (opcional: `/speckit-analyze` para checar
   consistência entre spec, plano e tarefas).
5. Implemente: `/speckit-implement`.
6. Abra o PR para `main` referenciando a issue (`Closes #N`). O merge é feito
   por merge commit (sem squash), preservando o histórico da branch.

Mudanças sem spec (emendas da constituição, documentação, CI, manutenção) usam a
branch `<tipo>/<issue>-nome-curto`, por exemplo `docs/1-constituicao`.

### Commits

[Conventional Commits](https://www.conventionalcommits.org/):
`tipo(escopo): descrição`, com `tipo` em `feat`, `fix`, `docs`, `test`, `refactor`,
`build`, `ci` ou `chore`.

## Cartão, cobranças, faturas e recebíveis no frontend (spec 015)

- **Rotas:** PF em `/cartao`, `/cartao/faturas` e `/cartao/faturas/AAAA-MM`; PJ em `/cobrancas`, `/cobrancas/nova` e
  `/cobrancas/recebiveis`; a cobrança, para todos, em `/c/XXXX-XXXX`.
- **Código curto:** gerado pela API; a migration `AdicionarCodigoCobranca` preenche as cobranças antigas.
- **Dados do cartão:** só em memória. `bank.operacoes` guarda apenas chaves de idempotência.
- **e2e de faturas:** `prepararFatura` (`e2e/apoioCartao.ts`) antedata a fatura por `docker compose exec postgres psql`;
  precisa do Compose da raiz em execução.
