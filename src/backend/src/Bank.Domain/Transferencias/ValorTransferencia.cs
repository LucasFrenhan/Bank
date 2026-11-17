using Bank.Domain.Comum;

namespace Bank.Domain.Transferencias;

/// <summary>
/// Valor de uma transferência (FR-004): ≥ R$ 0,01, no máximo 2 casas decimais (sem
/// arredondar) e dentro de <c>numeric(18,2)</c>.
/// </summary>
public sealed record ValorTransferencia
{
    public const string Campo = "valor";
    public const decimal Minimo = 0.01m;
    public const decimal Maximo = 9_999_999_999_999_999.99m;

    private ValorTransferencia(decimal valor)
    {
        Valor = valor;
    }

    public decimal Valor { get; }

    public static Resultado<ValorTransferencia> Criar(decimal? valor)
    {
        if (valor is null)
        {
            return Resultado.Falha<ValorTransferencia>(Erro.Validacao(Campo, "Informe o valor."));
        }

        if (valor < Minimo)
        {
            return Resultado.Falha<ValorTransferencia>(Erro.Validacao(Campo, "O valor deve ser de pelo menos R$ 0,01."));
        }

        if (!Dinheiro.TemAteDuasCasas(valor.Value))
        {
            return Resultado.Falha<ValorTransferencia>(Erro.Validacao(Campo, "O valor deve ter no máximo 2 casas decimais."));
        }

        return valor > Maximo
            ? Resultado.Falha<ValorTransferencia>(Erro.Validacao(Campo, "O valor excede o máximo permitido."))
            : Resultado.Ok(new ValorTransferencia(Dinheiro.NormalizarEscala(valor.Value)));
    }
}

/// <summary>Descrição opcional da transferência: espaços removidos, vazia → nula, até 200 caracteres.</summary>
public static class DescricaoTransferencia
{
    public const string Campo = "descricao";
    public const int TamanhoMaximo = 200;

    public static Resultado<string?> Normalizar(string? descricao)
    {
        var normalizada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();

        return normalizada is { Length: > TamanhoMaximo }
            ? Resultado.Falha<string?>(Erro.Validacao(Campo, $"A descrição deve ter no máximo {TamanhoMaximo} caracteres."))
            : Resultado.Ok(normalizada);
    }
}
