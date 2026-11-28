using Bank.SharedKernel.Aplicacao;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bank.Api.Endpoints;

/// <summary>Apoio comum aos endpoints: cabeçalho de idempotência e página de resposta.</summary>
public static class EndpointsComuns
{
    public const string CabecalhoIdempotencia = Entradas.CampoChave;

    /// <summary>Marca o <c>Idempotency-Key</c> como obrigatório no OpenAPI.</summary>
    public static RouteHandlerBuilder ComChaveIdempotencia(this RouteHandlerBuilder rota, string descricao) =>
        rota.AddOpenApiOperationTransformer((OpenApiOperation operacao, OpenApiOperationTransformerContext _, CancellationToken _) =>
        {
            foreach (var parametro in operacao.Parameters ?? [])
            {
                if (parametro is OpenApiParameter cabecalho && cabecalho.Name == CabecalhoIdempotencia)
                {
                    cabecalho.Required = true;
                    cabecalho.Description = descricao;
                }
            }

            return Task.CompletedTask;
        });
}

/// <summary>Código e nome da bandeira (specs/014-cartao-credito-cobrancas).</summary>
public sealed record BandeiraResposta(string Codigo, string Nome)
{
    public static BandeiraResposta De(Domain.Cartoes.Bandeira bandeira)
    {
        ArgumentNullException.ThrowIfNull(bandeira);
        return new BandeiraResposta(bandeira.Codigo, bandeira.Nome);
    }
}
