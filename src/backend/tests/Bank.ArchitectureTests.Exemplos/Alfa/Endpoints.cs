using Bank.Modulos.Alfa.Contratos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bank.Modulos.Alfa.Endpoints;

internal static class AlfaEndpoints
{
    public const string Rota = "/exemplos/alfa/{id:guid}";

    public static IEndpointRouteBuilder MapAlfa(this IEndpointRouteBuilder rotas)
    {
        rotas.MapGet(Rota, (Guid id, IAlfaConsultas consultas) =>
            consultas.Obter(new AlfaId(id)) is { } resumo ? Results.Ok(resumo) : Results.NotFound());
        return rotas;
    }
}
