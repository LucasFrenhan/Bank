using Microsoft.Extensions.Options;

namespace Bank.Infrastructure.Mensageria;

/// <summary>
/// Configuração da mensageria (seção <c>Mensageria</c>; specs/010-mensageria-rabbitmq, research
/// R11). <see cref="Senha"/> vem de variável de ambiente ou user-secrets e nunca vai para log.
/// Os intervalos não têm padrão no código: o binder de configuração mescla arrays por índice.
/// </summary>
public sealed class MensageriaOptions
{
    public const string Secao = "Mensageria";

    public string Host { get; set; } = string.Empty;

    public int Porta { get; set; } = 5672;

    public string VirtualHost { get; set; } = string.Empty;

    public string Usuario { get; set; } = string.Empty;

    public string Senha { get; set; } = string.Empty;

    /// <summary>Espera antes de cada nova tentativa do consumidor, em ms (FR-018a).</summary>
    public int[] IntervalosNovaTentativaMs { get; set; } = [];

    public ushort Prefetch { get; set; } = 10;

    public int IntervaloDespachoMs { get; set; } = 1000;

    public int TamanhoLoteDespacho { get; set; } = 50;

    public int TimeoutConfirmacaoMs { get; set; } = 5000;

    public override string ToString() => $"amqp://{Usuario}@{Host}:{Porta}/{VirtualHost}";
}

/// <summary>Valida a configuração da mensageria na partida; a mensagem identifica a chave.</summary>
internal sealed class ValidadorMensageriaOptions : IValidateOptions<MensageriaOptions>
{
    public ValidateOptionsResult Validate(string? name, MensageriaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var falhas = new List<string>();

        Obrigatorio(options.Host, nameof(MensageriaOptions.Host), falhas);
        Obrigatorio(options.VirtualHost, nameof(MensageriaOptions.VirtualHost), falhas);
        Obrigatorio(options.Usuario, nameof(MensageriaOptions.Usuario), falhas);
        Obrigatorio(options.Senha, nameof(MensageriaOptions.Senha), falhas);

        if (options.Porta is < 1 or > 65535)
        {
            falhas.Add($"{MensageriaOptions.Secao}:{nameof(MensageriaOptions.Porta)} deve estar entre 1 e 65535.");
        }

        var intervalos = options.IntervalosNovaTentativaMs;
        if (intervalos.Length == 0 || intervalos.Any(ms => ms <= 0) || intervalos.Zip(intervalos.Skip(1)).Any(par => par.Second <= par.First))
        {
            falhas.Add($"{MensageriaOptions.Secao}:{nameof(MensageriaOptions.IntervalosNovaTentativaMs)} deve ter ao menos um intervalo, todos positivos e crescentes.");
        }

        if (options.Prefetch < 1)
        {
            falhas.Add($"{MensageriaOptions.Secao}:{nameof(MensageriaOptions.Prefetch)} deve ser ao menos 1.");
        }

        if (options.IntervaloDespachoMs < 1)
        {
            falhas.Add($"{MensageriaOptions.Secao}:{nameof(MensageriaOptions.IntervaloDespachoMs)} deve ser positivo.");
        }

        if (options.TamanhoLoteDespacho < 1)
        {
            falhas.Add($"{MensageriaOptions.Secao}:{nameof(MensageriaOptions.TamanhoLoteDespacho)} deve ser ao menos 1.");
        }

        if (options.TimeoutConfirmacaoMs < 100)
        {
            falhas.Add($"{MensageriaOptions.Secao}:{nameof(MensageriaOptions.TimeoutConfirmacaoMs)} deve ser ao menos 100.");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }

    private static void Obrigatorio(string valor, string chave, List<string> falhas)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            falhas.Add($"{MensageriaOptions.Secao}:{chave} é obrigatório.");
        }
    }
}
