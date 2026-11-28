using Bank.SharedKernel;

namespace Bank.Domain.Usuarios;

/// <summary>Perfis de conta suportados (RN-01). Imutável após o cadastro.</summary>
public enum TipoConta
{
    PessoaFisica = 1,
    PessoaJuridica = 2,
}

public static class TiposConta
{
    public const string Campo = "tipoConta";

    /// <summary>
    /// Converte o texto exato (<c>PessoaFisica</c> ou <c>PessoaJuridica</c>); diferenças de
    /// caixa, abreviações e números são rejeitados.
    /// </summary>
    public static Resultado<TipoConta> Converter(string? valor) => valor switch
    {
        nameof(TipoConta.PessoaFisica) => Resultado.Ok(TipoConta.PessoaFisica),
        nameof(TipoConta.PessoaJuridica) => Resultado.Ok(TipoConta.PessoaJuridica),
        _ => Resultado.Falha<TipoConta>(Erro.Validacao(Campo, "Informe 'PessoaFisica' ou 'PessoaJuridica'.")),
    };
}
