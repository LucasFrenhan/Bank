namespace Bank.Api.ErrorHandling;

/// <summary>
/// Toda resposta de erro em <c>ProblemDetails</c> (constituição, Princípio III;
/// specs/002-setup-api, R6).
/// </summary>
public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddTratamentoDeErros(this IServiceCollection services)
    {
        services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = ProblemDetailsTraceIdCustomizer.Customizar);

        // JSON malformado / binding inválido → 400 (e não exceção tratada como 500)
        services.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = false);

        return services;
    }

    public static IApplicationBuilder UseTratamentoDeErros(this IApplicationBuilder app)
    {
        // Sem página de exceção do desenvolvedor: o formato é o mesmo em todos os ambientes.
        app.UseExceptionHandler();

        // Converte respostas de erro sem corpo (404, 405, 400) em ProblemDetails.
        app.UseStatusCodePages();

        return app;
    }
}
