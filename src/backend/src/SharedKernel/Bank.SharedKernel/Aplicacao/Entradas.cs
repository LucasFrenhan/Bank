using System.Globalization;

namespace Bank.SharedKernel.Aplicacao;

/// <summary>Página pedida, já validada.</summary>
public readonly record struct Paginacao(int Numero, int Tamanho)
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;
    public const string CampoPagina = "pagina";
    public const string CampoTamanho = "tamanhoPagina";

    public int Deslocamento => (int)Math.Min((Numero - 1L) * Tamanho, int.MaxValue);

    /// <summary>Lê os parâmetros da query string; os dois erros vêm juntos.</summary>
    public static Resultado<Paginacao> Ler(string? pagina, string? tamanhoPagina)
    {
        var erros = new List<Erro>();

        var numero = 1;
        if (pagina is not null && (!int.TryParse(pagina, NumberStyles.None, CultureInfo.InvariantCulture, out numero) || numero < 1))
        {
            erros.Add(Erro.Validacao(CampoPagina, "A página deve ser um número inteiro maior ou igual a 1."));
        }

        var tamanho = TamanhoPadrao;
        if (tamanhoPagina is not null && (!int.TryParse(tamanhoPagina, NumberStyles.None, CultureInfo.InvariantCulture, out tamanho) || tamanho is < 1 or > TamanhoMaximo))
        {
            erros.Add(Erro.Validacao(CampoTamanho, $"O tamanho da página deve estar entre 1 e {TamanhoMaximo}."));
        }

        return erros.Count > 0 ? Resultado.Falha<Paginacao>(erros) : Resultado.Ok(new Paginacao(numero, tamanho));
    }
}

/// <summary>Página de resposta dos casos de uso.</summary>
public sealed record PaginaDe<T>(IReadOnlyList<T> Itens, int PaginaAtual, int TamanhoPagina, int TotalItens);

/// <summary>Leitura de entradas comuns aos casos de uso da specs/014-cartao-credito-cobrancas.</summary>
public static class Entradas
{
    public const string CampoChave = "Idempotency-Key";

    private const long TicksPorMicrossegundo = TimeSpan.TicksPerMillisecond / 1000;

    public static Resultado<Guid> LerChaveIdempotencia(string? texto) =>
        Guid.TryParse(texto, out var chave) && chave != Guid.Empty
            ? Resultado.Ok(chave)
            : Resultado.Falha<Guid>(Erro.Validacao(CampoChave, "Informe no cabeçalho Idempotency-Key um UUID que identifique esta operação."));

    public static Resultado<Guid> LerIdentificador(string? texto, string campo) =>
        Guid.TryParse(texto, out var valor) && valor != Guid.Empty
            ? Resultado.Ok(valor)
            : Resultado.Falha<Guid>(Erro.Validacao(campo, "Informe um identificador válido."));

    /// <summary>Valor de um enum pelo nome exato; ausente é aceito (nulo).</summary>
    public static Resultado<TEnum?> LerFiltro<TEnum>(string? texto, string campo)
        where TEnum : struct, Enum
    {
        if (texto is null)
        {
            return Resultado.Ok<TEnum?>(null);
        }

        foreach (var nome in Enum.GetNames<TEnum>())
        {
            if (string.Equals(nome, texto, StringComparison.Ordinal))
            {
                return Resultado.Ok<TEnum?>(Enum.Parse<TEnum>(nome));
            }
        }

        return Resultado.Falha<TEnum?>(Erro.Validacao(campo, $"Use um destes valores: {string.Join(", ", Enum.GetNames<TEnum>())}."));
    }

    /// <summary>Instante com precisão de microssegundos, a mesma do <c>timestamptz</c>.</summary>
    public static DateTimeOffset AgoraEmMicrossegundos(this TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);
        var agora = relogio.GetUtcNow();
        return agora.AddTicks(-(agora.Ticks % TicksPorMicrossegundo));
    }
}

/// <summary>Erros comuns de idempotência dos casos de uso da specs/014-cartao-credito-cobrancas.</summary>
public static class ErrosIdempotencia
{
    /// <summary>Código estável do contrato (specs/006-transferencias), comum a todos os comandos idempotentes.</summary>
    public const string CodigoDivergente = "idempotencia-divergente";

    /// <summary>Mesmo código das transferências, com texto genérico.</summary>
    public static readonly Erro Divergente =
        new(CodigoDivergente, "Esta chave de idempotência já foi usada com outros dados.", TipoErro.Conflito);
}
