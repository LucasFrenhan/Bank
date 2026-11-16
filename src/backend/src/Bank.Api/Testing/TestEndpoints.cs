namespace Bank.Api.Testing;

/// <summary>
/// Endpoints usados apenas pelos testes de integração para provar o tratamento de falhas
/// pelo pipeline real. Mapeados somente no ambiente <c>Testing</c> e fora do OpenAPI
/// (specs/002-setup-api, R7 e Complexity Tracking).
/// </summary>
public static class TestEndpoints
{
    public const string AmbienteDeTestes = "Testing";

    public static WebApplication MapTestEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsEnvironment(AmbienteDeTestes))
        {
            return app;
        }

        var grupo = app.MapGroup("/__test").ExcludeFromDescription();

        grupo.MapGet("/falha", () =>
        {
            throw new InvalidOperationException("segredo-interno: esta mensagem nunca deve chegar ao consumidor");
        });

        grupo.MapPost("/eco", (MensagemEco mensagem) => Results.Ok(mensagem));

        return app;
    }

    public sealed record MensagemEco(string Texto);
}
