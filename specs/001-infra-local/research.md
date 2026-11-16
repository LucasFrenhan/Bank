# Research: Ambiente de Infraestrutura Local

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-11-16

Tags de imagem consultadas nos registries oficiais (Docker Hub e MCR) em 2026-11-16.

## R1. Coletor de telemetria

- **Decision**: Aspire Dashboard standalone (`mcr.microsoft.com/dotnet/aspire-dashboard`),
  recebendo OTLP diretamente das aplicações, sem OpenTelemetry Collector intermediário.
- **Rationale**: decisão do time (2026-11-16). Um único contêiner recebe e exibe os três
  sinais (traces, métricas e logs) com correlação entre eles, atendendo o FR-004 sem peças
  extras. É mantido pela Microsoft e integrado ao ecossistema .NET da constituição.
- **Alternatives considered**:
  - Jaeger + Prometheus: exibe traces e métricas, mas não logs; exigiria Loki + Grafana e um
    OpenTelemetry Collector para rotear os sinais (4+ contêineres).
  - Grafana LGTM (`grafana/otel-lgtm`): completo, mas imagem grande e mais pesada para o
    ciclo local; pode ser reavaliado se surgir necessidade de retenção ou dashboards.
- **Consequências**: o dashboard guarda a telemetria **somente em memória** (perdida ao
  reiniciar o contêiner) e tem limites de itens por sinal. Isso é aceitável: a persistência
  do FR-007 se aplica apenas ao banco e ao broker.

## R2. Versões das imagens (FR-012)

| Serviço | Imagem | Motivo |
| --- | --- | --- |
| PostgreSQL | `postgres:18.7-alpine` | Última minor estável da major 18; a 19 ainda está em RC (`19rc2`). |
| RabbitMQ | `rabbitmq:4.3.7-management-alpine` | Última patch da 4.3; a 4.4.0 acabou de sair (`.0`) e é evitada no início do projeto. Inclui o plugin de Management. |
| Aspire Dashboard | `mcr.microsoft.com/dotnet/aspire-dashboard:13.6.5` | Última versão estável publicada. |

- **Rationale**: tags com versão completa garantem o mesmo ambiente em todas as máquinas;
  variantes `alpine` reduzem download e tempo de subida (SC-002).
- **Alternatives considered**: tags flutuantes (`18`, `4-management`, `latest`) — rejeitadas
  pelo FR-012. Fixar por digest (`@sha256`) — mais rígido do que o necessário para ambiente
  local; atualizações serão feitas por PR (futuramente via Dependabot).

## R3. Verificações de saúde (FR-005)

- **PostgreSQL**: `pg_isready -U $POSTGRES_USER -d $POSTGRES_DB` — reporta pronto apenas
  quando o servidor aceita conexões no banco do projeto.
- **RabbitMQ**: `rabbitmq-diagnostics -q check_port_connectivity` — confirma que os
  listeners (AMQP e Management) aceitam conexões; `ping` foi rejeitado porque responde
  antes de as portas estarem abertas.
- **Aspire Dashboard**: a imagem é mínima e pode não conter `curl`/`wget`/shell.
  Decisão: verificar na implementação, nesta ordem de preferência:
  1. requisição HTTP ao frontend (`http://localhost:18888`) com ferramenta presente na imagem;
  2. teste de porta via `bash` (`/dev/tcp`) se houver bash;
  3. se a imagem não tiver nenhuma ferramenta, usar uma imagem derivada mínima
     (`docker/aspire-dashboard/Dockerfile`, `FROM` a tag fixada) que acrescente apenas a
     ferramenta de checagem, e registrar o motivo em
     [Complexity Tracking](plan.md#complexity-tracking). Validar a saúde só manualmente
     **não** é aceitável: a constituição exige healthcheck em toda a infraestrutura, assim
     como o FR-005 e o SC-003.
- **Parâmetros comuns**: `interval: 5s`, `timeout: 5s`, `retries: 12`, `start_period`
  de 10s (Postgres/Dashboard) e 30s (RabbitMQ, que demora mais a subir). Com isso, a
  falha é declarada em no máximo ~70–90s, coerente com o SC-002 (≤ 60s em condições
  normais).

## R4. Dependências entre serviços (FR-006)

- **Decision**: os três serviços desta spec são independentes entre si (o dashboard não
  depende do banco nem do broker, e vice-versa — ver edge case do coletor fora do ar).
  O FR-006 é atendido estruturalmente: todo serviço tem healthcheck, então serviços
  futuros (API — issue #3) declaram `depends_on: { <serviço>: { condition: service_healthy } }`.
- **Validação**: `docker compose up -d --wait` só retorna sucesso quando todos os serviços
  estão `healthy`; é o comando de subida documentado.

## R5. Persistência e reset (FR-007, FR-008)

- **PostgreSQL 18**: a imagem oficial mudou o `PGDATA` a partir da 18
  (`/var/lib/postgresql/18/docker`) e declara o volume em `/var/lib/postgresql`. O volume
  nomeado MUST ser montado em `/var/lib/postgresql` (não em `/var/lib/postgresql/data`,
  como nas versões anteriores), o que também facilita upgrades de major.
- **RabbitMQ**: os dados ficam em `/var/lib/rabbitmq/mnesia/<nome-do-nó>`, e o nome do nó
  deriva do hostname do contêiner. O serviço MUST ter `hostname` fixo; caso contrário,
  cada recriação do contêiner gera um nó novo e os dados "somem".
- **Reset**: `docker compose down -v` remove contêineres e volumes nomeados do projeto.
- **Nome do projeto**: `name: bank` no arquivo Compose, para que volumes, rede e
  contêineres tenham nomes previsíveis independentemente da pasta do clone.

## R6. Configuração local e segredos (FR-009, FR-010)

- **Decision**: `.env.example` versionado na raiz com valores padrão de desenvolvimento;
  `.env` local (já ignorado pelo `.gitignore`). No `docker-compose.yml`, toda variável usa
  valor padrão (`${POSTGRES_PORT:-5432}`), de modo que o ambiente sobe mesmo sem `.env`
  (edge case da spec).
- **Portas no host**: publicadas somente em `127.0.0.1`, para não expor na rede serviços
  com credenciais padrão e um dashboard sem autenticação.
- **Banco e vhost do projeto**: banco `bank` criado via `POSTGRES_DB`; vhost `bank` no
  RabbitMQ via `RABBITMQ_DEFAULT_VHOST`.

## R7. Endpoints OTLP e autenticação do dashboard

- O dashboard escuta, dentro do contêiner, UI em `18888`, OTLP/gRPC em `18889` e
  OTLP/HTTP em `18890`.
- **Decision**: publicar OTLP nas portas padrão do OpenTelemetry no host —
  `4317` (gRPC) → `18889` e `4318` (HTTP) → `18890` — para que SDKs funcionem com a
  configuração padrão (`OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317`). UI em
  `18888`.
- **Autenticação**: `DASHBOARD__FRONTEND__AUTHMODE=Unsecured` e
  `DASHBOARD__OTLP__AUTHMODE=Unsecured`, aceitável porque as portas só escutam em
  `127.0.0.1` (R6) e o ambiente é exclusivamente local. Evita o token de login gerado a
  cada reinício, que prejudicaria o SC-001.

## R8. Verificação automatizada

- **Decision**: workflow do GitHub Actions (`.github/workflows/infra-local.yml`) disparado
  em PRs que alterem `docker-compose.yml`, `.env.example` ou o próprio workflow:
  `docker compose config -q` (validação de sintaxe) e `docker compose up -d --wait
  --wait-timeout 120` seguido de `docker compose down -v`.
- **Rationale**: o Princípio VII exige que a qualidade seja demonstrada por verificação
  automatizada; o `--wait` comprova FR-001, FR-005 e SC-003 em todo PR.
- O timeout de 120s é maior que os 60s do SC-002 de propósito: no CI as imagens não estão
  em cache. O SC-002 é medido manualmente (T009).
- Este workflow **não** deve ser marcado como check obrigatório na proteção da `main`:
  por causa do filtro de paths, PRs que não o disparam ficariam aguardando o check para
  sempre. O check obrigatório será o pipeline geral de CI (sem filtro), quando existir.
- **Alternatives considered**: somente validação manual pelo quickstart — rejeitada por
  não impedir regressões.
