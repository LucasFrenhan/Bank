using System.Globalization;

namespace Bank.Infrastructure.Mensageria;

/// <summary>Para onde uma entrega que falhou é republicada, e com quais headers.</summary>
/// <param name="Execucao">Execução que falhou (1 = primeira).</param>
/// <param name="IntervaloMs">Espera até a próxima execução; nulo quando vai para a DLQ.</param>
/// <param name="Motivo"><c>mensagem-invalida</c> ou <c>tentativas-esgotadas</c> na DLQ; nulo na espera.</param>
internal sealed record DestinoFalha(
    string Exchange,
    string RoutingKey,
    Dictionary<string, object?> Headers,
    int Execucao,
    int Reprocessamentos,
    int? IntervaloMs,
    string? Motivo)
{
    public bool ParaDlq => Motivo is not null;
}

/// <summary>
/// Novas tentativas e DLQ (specs/010-mensageria-rabbitmq, FR-018, FR-018a–c, research R7;
/// contracts/topologia-e-mensagens.md): na falha da execução N ≤ número de intervalos, a cópia vai
/// para a fila de espera do N-ésimo intervalo; depois, para a DLQ com os headers de diagnóstico.
/// Mensagem inválida vai direto para a DLQ. Uma entrega com <c>x-falhou-em</c> veio da DLQ
/// (reprocessamento manual) e recomeça a contagem.
/// </summary>
internal static class PoliticaNovaTentativa
{
    public const string Tentativas = "x-tentativas";
    public const string Reprocessamentos = "x-reprocessamentos";
    public const string Motivo = "x-motivo";
    public const string UltimoErro = "x-ultimo-erro";
    public const string FalhouEm = "x-falhou-em";

    public const string MotivoMensagemInvalida = "mensagem-invalida";
    public const string MotivoTentativasEsgotadas = "tentativas-esgotadas";

    public const int TamanhoMaximoErro = 500;

    /// <summary>Headers de controle que nunca são copiados como estão (o <c>x-death</c> é do broker).</summary>
    private static readonly HashSet<string> Controle = new(StringComparer.Ordinal)
    {
        Tentativas, Reprocessamentos, Motivo, UltimoErro, FalhouEm, "x-death", "x-first-death-exchange",
        "x-first-death-queue", "x-first-death-reason", "x-last-death-exchange", "x-last-death-queue", "x-last-death-reason",
    };

    /// <summary>Execução atual (1 = primeira) e reprocessamentos, a partir dos headers da entrega.</summary>
    public static (int Execucao, int Reprocessamentos) Contar(IDictionary<string, object?>? headers)
    {
        var reprocessamentos = CabecalhosMensagem.LerInteiro(headers, Reprocessamentos) ?? 0;
        if (CabecalhosMensagem.LerTexto(headers, FalhouEm) is not null)
        {
            return (1, reprocessamentos + 1);
        }

        return ((CabecalhosMensagem.LerInteiro(headers, Tentativas) ?? 0) + 1, reprocessamentos);
    }

    /// <param name="erro">Exceção do processamento; nulo quando a mensagem é inválida.</param>
    /// <param name="erroLeitura">Motivo da invalidez, quando <paramref name="erro"/> é nulo.</param>
    public static DestinoFalha Decidir(
        DescritorFila fila,
        IReadOnlyList<int> intervalosMs,
        IDictionary<string, object?>? headersOriginais,
        Exception? erro,
        string? erroLeitura,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(intervalosMs);

        var (execucao, reprocessamentos) = Contar(headersOriginais);
        var headers = CopiarSemControle(headersOriginais);
        headers[Tentativas] = execucao;
        if (reprocessamentos > 0)
        {
            headers[Reprocessamentos] = reprocessamentos;
        }

        if (erro is not null && execucao <= intervalosMs.Count)
        {
            var intervalo = intervalosMs[execucao - 1];
            return new DestinoFalha(string.Empty, TopologiaMensageria.FilaEspera(fila.Fila, intervalo), headers, execucao, reprocessamentos, intervalo, null);
        }

        var motivo = erro is null ? MotivoMensagemInvalida : MotivoTentativasEsgotadas;
        headers[Motivo] = motivo;
        headers[UltimoErro] = Truncar(erro is null ? $"MensagemInvalida: {erroLeitura}" : $"{erro.GetType().Name}: {erro.Message}");
        headers[FalhouEm] = agora.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        return new DestinoFalha(TopologiaMensageria.ExchangeDlx, fila.RoutingKeyDlq, headers, execucao, reprocessamentos, null, motivo);
    }

    private static Dictionary<string, object?> CopiarSemControle(IDictionary<string, object?>? headers)
    {
        var copia = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (chave, valor) in headers ?? new Dictionary<string, object?>())
        {
            if (!Controle.Contains(chave))
            {
                copia[chave] = valor;
            }
        }

        return copia;
    }

    private static string Truncar(string texto) => texto.Length <= TamanhoMaximoErro ? texto : texto[..TamanhoMaximoErro];
}
