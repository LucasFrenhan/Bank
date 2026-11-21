using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using Testcontainers.RabbitMq;

namespace Bank.IntegrationTests;

/// <summary>Um broker RabbitMQ e um vhost exclusivo dentro dele.</summary>
public sealed record BrokerDeTeste(RabbitMqContainer Container, string VirtualHost)
{
    public string Host => Container.Hostname;

    public int Porta => Container.GetMappedPublicPort(RabbitMqCompartilhado.PortaAmqp);

    /// <summary>Lê uma fila pela API de gestão (argumentos, durabilidade, mensagens).</summary>
    public async Task<JsonElement> ObterFilaAsync(string fila, CancellationToken cancellationToken)
    {
        using var http = RabbitMqCompartilhado.ClienteGestao(Container);
        using var resposta = await http.GetAsync(new Uri($"api/queues/{Uri.EscapeDataString(VirtualHost)}/{Uri.EscapeDataString(fila)}", UriKind.Relative), cancellationToken);
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>Bindings de uma fila pela API de gestão (origem e routing key).</summary>
    public async Task<IReadOnlyList<(string Origem, string RoutingKey)>> ObterBindingsAsync(string fila, CancellationToken cancellationToken)
    {
        using var http = RabbitMqCompartilhado.ClienteGestao(Container);
        using var resposta = await http.GetAsync(new Uri($"api/queues/{Uri.EscapeDataString(VirtualHost)}/{Uri.EscapeDataString(fila)}/bindings", UriKind.Relative), cancellationToken);
        resposta.EnsureSuccessStatusCode();
        var bindings = await resposta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return [.. bindings.EnumerateArray().Select(b => (b.GetProperty("source").GetString()!, b.GetProperty("routing_key").GetString()!))];
    }

    /// <summary>Lê uma exchange pela API de gestão.</summary>
    public async Task<JsonElement> ObterExchangeAsync(string exchange, CancellationToken cancellationToken)
    {
        using var http = RabbitMqCompartilhado.ClienteGestao(Container);
        using var resposta = await http.GetAsync(new Uri($"api/exchanges/{Uri.EscapeDataString(VirtualHost)}/{Uri.EscapeDataString(exchange)}", UriKind.Relative), cancellationToken);
        resposta.EnsureSuccessStatusCode();
        return await resposta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }
}

/// <summary>
/// Um contêiner RabbitMQ por execução de testes, com a imagem do Compose (constituição,
/// Princípio VII; specs/010-mensageria-rabbitmq, research R12). Cada fábrica recebe um vhost
/// próprio, o que isola as classes paralelas; os testes que param o broker usam um contêiner
/// exclusivo (<see cref="CriarExclusivoAsync"/>).
/// </summary>
public static class RabbitMqCompartilhado
{
    public const string Imagem = "rabbitmq:4.3.7-management-alpine";
    public const string Usuario = "bank";
    public const string Senha = "bank_test_password";
    public const ushort PortaAmqp = 5672;
    public const ushort PortaGestao = 15672;

    private static readonly Lazy<Task<RabbitMqContainer>> Container = new(() => IniciarAsync(portaFixa: false));

    /// <summary>Vhost novo no contêiner compartilhado.</summary>
    public static async Task<BrokerDeTeste> CriarVirtualHostAsync()
    {
        var container = await Container.Value;
        return await CriarVirtualHostAsync(container);
    }

    /// <summary>
    /// Contêiner só do chamador, com porta AMQP fixa no host (sobrevive a parar e religar),
    /// que deve descartá-lo.
    /// </summary>
    public static async Task<BrokerDeTeste> CriarExclusivoAsync()
    {
        var container = await IniciarAsync(portaFixa: true);
        return await CriarVirtualHostAsync(container);
    }

    internal static HttpClient ClienteGestao(RabbitMqContainer container)
    {
        var http = new HttpClient { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(PortaGestao)}/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Usuario}:{Senha}")));
        return http;
    }

    private static async Task<BrokerDeTeste> CriarVirtualHostAsync(RabbitMqContainer container)
    {
        var vhost = $"bank_testes_{Guid.NewGuid():N}";
        using var http = ClienteGestao(container);
        using (var criar = await http.PutAsync(new Uri($"api/vhosts/{vhost}", UriKind.Relative), null))
        {
            criar.EnsureSuccessStatusCode();
        }

        using var permissoes = await http.PutAsJsonAsync(
            new Uri($"api/permissions/{vhost}/{Usuario}", UriKind.Relative),
            new { configure = ".*", write = ".*", read = ".*" });
        permissoes.EnsureSuccessStatusCode();

        return new BrokerDeTeste(container, vhost);
    }

    private static async Task<RabbitMqContainer> IniciarAsync(bool portaFixa)
    {
        var builder = new RabbitMqBuilder(Imagem)
            .WithUsername(Usuario)
            .WithPassword(Senha)
            .WithPortBinding(PortaGestao, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(PortaAmqp).UntilInternalTcpPortIsAvailable(PortaGestao));

        if (portaFixa)
        {
            builder = builder.WithPortBinding(PortaLivre(), PortaAmqp);
        }

        var container = builder.Build();
        await container.StartAsync();
        return container;
    }

    private static int PortaLivre()
    {
        using var ouvinte = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        ouvinte.Start();
        return ((System.Net.IPEndPoint)ouvinte.LocalEndpoint).Port;
    }
}
