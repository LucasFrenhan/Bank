using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Bank.Api.OpenApi;

/// <summary>
/// Documento OpenAPI nativo e UI do Scalar (specs/002-setup-api, R4). Disponíveis somente
/// fora de produção (constituição, Princípio V; FR-005). Publica o esquema de segurança
/// <c>Bearer</c> (JWT) e o aplica às operações com autorização, permitindo autenticar pelo
/// Scalar (specs/003-usuarios-carteiras, contrato).
/// </summary>
public static class OpenApiExtensions
{
    public const string NomeDocumento = "v1";
    public const string EsquemaBearer = "Bearer";

    public static IServiceCollection AddDocumentacaoApi(this IServiceCollection services)
    {
        services.AddOpenApi(NomeDocumento, opcoes =>
        {
            opcoes.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Components ??= new OpenApiComponents();
                documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                documento.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Token obtido em POST /api/v1/autenticacao/login.",
                };
                return Task.CompletedTask;
            });

            opcoes.AddOperationTransformer((operacao, contexto, _) =>
            {
                var exigeAutorizacao = contexto.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any();
                if (exigeAutorizacao)
                {
                    operacao.Security ??= [];
                    operacao.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(EsquemaBearer, contexto.Document)] = [],
                    });
                }

                return Task.CompletedTask;
            });
        });

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
