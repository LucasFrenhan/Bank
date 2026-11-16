using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bank.IntegrationTests;

/// <summary>
/// Hospeda a API em memória no ambiente <c>Testing</c> (habilita os endpoints de teste e
/// desliga a exportação OTLP — specs/002-setup-api, research R5 e R7).
/// </summary>
public class BankApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
    }
}
