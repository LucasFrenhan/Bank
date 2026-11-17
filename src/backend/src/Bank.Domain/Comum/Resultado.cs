namespace Bank.Domain.Comum;

/// <summary>
/// Resultado tipado de uma operação: sucesso ou lista de erros esperados. Exceções ficam
/// reservadas a falhas inesperadas (constituição, Princípio III; research R6).
/// </summary>
public class Resultado
{
    private static readonly Resultado SucessoSemValor = new([]);

    protected Resultado(IReadOnlyList<Erro> erros)
    {
        Erros = erros;
    }

    public IReadOnlyList<Erro> Erros { get; }

    public bool Sucesso => Erros.Count == 0;

    public static Resultado Ok() => SucessoSemValor;

    public static Resultado<T> Ok<T>(T valor) => new(valor, []);

    public static Resultado Falha(IEnumerable<Erro> erros) => new(ValidarErros(erros));

    public static Resultado Falha(Erro erro) => Falha([erro]);

    public static Resultado<T> Falha<T>(IEnumerable<Erro> erros) => new(default, ValidarErros(erros));

    public static Resultado<T> Falha<T>(Erro erro) => Falha<T>([erro]);

    private static Erro[] ValidarErros(IEnumerable<Erro> erros)
    {
        ArgumentNullException.ThrowIfNull(erros);
        var lista = erros.ToArray();
        if (lista.Length == 0)
        {
            throw new ArgumentException("Uma falha precisa de ao menos um erro.", nameof(erros));
        }

        return lista;
    }
}

/// <summary>Resultado com valor em caso de sucesso.</summary>
public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    internal Resultado(T? valor, IReadOnlyList<Erro> erros)
        : base(erros)
    {
        _valor = valor;
    }

    /// <summary>Valor do sucesso. Acessá-lo em uma falha é erro de programação.</summary>
    public T Valor => Sucesso
        ? _valor!
        : throw new InvalidOperationException("Resultado com falha não possui valor.");
}
