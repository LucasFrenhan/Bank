# Backend do Bank — build multi-stage com dois alvos (specs/002-setup-api, research R8;
# specs/010-mensageria-rabbitmq, research R1):
#   api    — API HTTP (alvo padrão do Compose: target: api)
#   worker — despachante do outbox e consumidores do RabbitMQ (target: worker)
# Contexto de build: src/backend

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-alpine3.24 AS build
WORKDIR /source

# Restore em camada própria para aproveitar o cache
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Bank.Domain/Bank.Domain.csproj src/Bank.Domain/
COPY src/Bank.Application/Bank.Application.csproj src/Bank.Application/
COPY src/Bank.Infrastructure/Bank.Infrastructure.csproj src/Bank.Infrastructure/
COPY src/Bank.Api/Bank.Api.csproj src/Bank.Api/
COPY src/Bank.Worker/Bank.Worker.csproj src/Bank.Worker/
RUN dotnet restore src/Bank.Api/Bank.Api.csproj \
 && dotnet restore src/Bank.Worker/Bank.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Bank.Api/Bank.Api.csproj -c Release -o /app/api --no-restore \
 && dotnet publish src/Bank.Worker/Bank.Worker.csproj -c Release -o /app/worker --no-restore

# Alpine: inclui o wget do BusyBox, usado pelos healthchecks do Compose
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24 AS base
WORKDIR /app
# Fuso de Brasília para o limite diário: a imagem Alpine não traz /usr/share/zoneinfo
# (specs/009-limite-transferencia, research R4)
RUN apk add --no-cache tzdata
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM base AS worker
COPY --from=build /app/worker .
USER app
ENTRYPOINT ["dotnet", "Bank.Worker.dll"]

# Último estágio: continua sendo o alvo padrão de um "docker build" sem --target
FROM base AS api
COPY --from=build /app/api .
USER app
ENTRYPOINT ["dotnet", "Bank.Api.dll"]
