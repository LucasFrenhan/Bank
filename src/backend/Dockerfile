# API do Bank — build multi-stage (specs/002-setup-api, research R8)
# Contexto de build: src/backend

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-alpine3.24 AS build
WORKDIR /source

# Restore em camada própria para aproveitar o cache
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Bank.Domain/Bank.Domain.csproj src/Bank.Domain/
COPY src/Bank.Application/Bank.Application.csproj src/Bank.Application/
COPY src/Bank.Infrastructure/Bank.Infrastructure.csproj src/Bank.Infrastructure/
COPY src/Bank.Api/Bank.Api.csproj src/Bank.Api/
RUN dotnet restore src/Bank.Api/Bank.Api.csproj

COPY src/ src/
RUN dotnet publish src/Bank.Api/Bank.Api.csproj -c Release -o /app --no-restore

# Alpine: inclui o wget do BusyBox, usado pelo healthcheck do Compose
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24 AS final
WORKDIR /app
COPY --from=build /app .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Bank.Api.dll"]
