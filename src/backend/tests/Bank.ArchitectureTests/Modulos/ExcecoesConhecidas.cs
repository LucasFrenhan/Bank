using System.Text;
using System.Text.RegularExpressions;

namespace Bank.ArchitectureTests.Modulos;

/// <summary>Violação de fronteira aceita temporariamente: regra, tipos envolvidos, motivo e issue que a remove.</summary>
public sealed record ExcecaoConhecida(string Regra, string Origem, string Destino, string Motivo, string Issue)
{
    public Violacao Chave => new(Regra, Origem, Destino);
}

/// <summary>Resultado da comparação entre as violações reais e a lista de exceções.</summary>
public sealed record ComparacaoExcecoes(
    IReadOnlyList<Violacao> Novas,
    IReadOnlyList<ExcecaoConhecida> Obsoletas,
    IReadOnlyList<ExcecaoConhecida> Incompletas)
{
    public bool Ok => Novas.Count == 0 && Obsoletas.Count == 0 && Incompletas.Count == 0;

    public string Descrever()
    {
        var texto = new StringBuilder();
        if (Novas.Count > 0)
        {
            texto.AppendLine("Violações novas (corrija no código; a lista de exceções não recebe entradas — constituição, Princípio VII):")
                .AppendLine(RegrasFronteira.Descrever(Novas));
        }

        if (Obsoletas.Count > 0)
        {
            texto.AppendLine("Exceções conhecidas sem violação correspondente (remova de ExcecoesConhecidas):");
            foreach (var e in Obsoletas)
            {
                texto.AppendLine($"{e.Regra} {e.Origem} → {e.Destino} ({e.Issue})");
            }
        }

        if (Incompletas.Count > 0)
        {
            texto.AppendLine("Exceções incompletas (regra existente, origem, destino, motivo e issue no formato #N são obrigatórios):");
            foreach (var e in Incompletas)
            {
                texto.AppendLine($"{e.Regra} {e.Origem} → {e.Destino} (motivo: \"{e.Motivo}\", issue: \"{e.Issue}\")");
            }
        }

        return texto.ToString();
    }
}

/// <summary>
/// Lista de exceções conhecidas às regras de fronteira (constituição v1.3.0, Princípio VII; specs/017-modulos-fundacao,
/// research R7 e contracts/regras-fronteira.md). Começa vazia e só diminui: nenhum PR acrescenta entradas, e cada
/// extração de módulo corrige no próprio PR as violações que revelar. Uma entrada sem violação correspondente
/// quebra o build até ser removida.
/// </summary>
public static partial class ExcecoesConhecidas
{
    public static readonly IReadOnlyList<ExcecaoConhecida> Todas =
    [
        // Formato de uma entrada (comparada por regra, origem e destino):
        // new("MOD-03", "Bank.Modulos.X.Application.Y", "Bank.Domain.Z", "motivo", "#123"),
    ];

    public static ComparacaoExcecoes Comparar(IEnumerable<Violacao> violacoes, IEnumerable<ExcecaoConhecida> excecoes)
    {
        var reais = violacoes.ToHashSet();
        var lista = excecoes.ToList();
        var chaves = lista.Select(e => e.Chave).ToHashSet();

        return new ComparacaoExcecoes(
            Novas: [.. reais.Where(v => !chaves.Contains(v))],
            Obsoletas: [.. lista.Where(e => !reais.Contains(e.Chave))],
            Incompletas: [.. lista.Where(e => !Completa(e))]);
    }

    private static bool Completa(ExcecaoConhecida e) =>
        RegrasFronteira.Descricoes.ContainsKey(e.Regra) &&
        !string.IsNullOrWhiteSpace(e.Origem) &&
        !string.IsNullOrWhiteSpace(e.Destino) &&
        !string.IsNullOrWhiteSpace(e.Motivo) &&
        IssueRegex().IsMatch(e.Issue);

    [GeneratedRegex(@"^#\d+$")]
    private static partial Regex IssueRegex();
}
