using Scalar.AspNetCore;

namespace Bank.Api.OpenApi;

/// <summary>
/// Documento OpenAPI nativo e UI do Scalar (specs/002-setup-api, R4). Disponíveis somente
/// fora de produção (constituição, Princípio V; FR-005).
/// </summary>
public static class OpenApiExtensions
{
    public const string NomeDocumento = "v1";

    public static IServiceCollection AddDocumentacaoApi(this IServiceCollection services)
    {
        services.AddOpenApi(NomeDocumento);
        return services;
    }

    public static WebApplication MapDocumentacaoApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsProduction())
        {
            return app;
        }

        app.MapOpenApi();                    // /openapi/v1.json
        app.MapScalarApiReference(opcoes =>  // /scalar/v1
            opcoes.WithTitle("Bank API"));

        return app;
    }
}
