using static Bank.IntegrationTests.ApiTransferencias;

namespace Bank.IntegrationTests;

/// <summary>
/// Par de usuários reais para os payloads publicados à mão nos testes de mensageria
/// (specs/011-notificacoes-auditoria, research R8): as notificações referenciam <c>usuarios</c> por
/// chave estrangeira (<c>RESTRICT</c>), então ids aleatórios levariam a mensagem à DLQ.
/// </summary>
internal static class UsuariosDeTeste
{
    public static async Task<(Guid Pagador, Guid Recebedor)> CriarParAsync(BankApiFactory api)
    {
        ArgumentNullException.ThrowIfNull(api);

        using var client = api.CreateClient();
        var pf = await CriarPfAsync(client);
        var pj = await CriarPjAsync(client);
        return (pf.UsuarioId, pj.UsuarioId);
    }
}
