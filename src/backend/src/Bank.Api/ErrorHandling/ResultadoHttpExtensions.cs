using Bank.Domain.Comum;

namespace Bank.Api.ErrorHandling;

/// <summary>
/// Traduz falhas tipadas da Application em <c>ProblemDetails</c> com <c>type</c> estável
/// <c>urn:bank:problema:&lt;codigo&gt;</c> (constituição, Princípio III; research R6;
/// contracts/usuarios-carteiras-api.md). O <c>detail</c> nunca repete dados enviados.
/// </summary>
public static class ResultadoHttpExtensions
{
    public const string PrefixoTipo = "urn:bank:problema:";

    private static readonly Dictionary<string, string> Titulos = new(StringComparer.Ordinal)
    {
        [Erro.CodigoValidacao] = "Dados inválidos",
        ["email-ja-cadastrado"] = "E-mail já cadastrado",
        ["documento-ja-cadastrado"] = "Documento já cadastrado",
        ["credenciais-invalidas"] = "Credenciais inválidas",
        ["carteira-nao-encontrada"] = "Carteira não encontrada",
        ["usuario-nao-encontrado"] = "Usuário não encontrado",
        ["destinatario-nao-encontrado"] = "Destinatário não encontrado",
        ["autotransferencia"] = "Transferência para a própria conta",
        ["saldo-insuficiente"] = "Saldo insuficiente",
        ["idempotencia-divergente"] = "Chave de idempotência já usada com outros dados",
        ["limite-excedido"] = "Limite de transferência excedido",

        // Cartão de crédito (specs/014-cartao-credito-cobrancas, FR-036)
        ["cartao-ja-existente"] = "Cartão já existente",
        ["cartao-nao-encontrado"] = "Cartão não encontrado",
        ["cartao-nao-autorizado"] = "Cartão não autorizado",
        ["cartao-bloqueado-fatura-vencida"] = "Cartão bloqueado por fatura vencida",
        ["limite-cartao-insuficiente"] = "Limite do cartão insuficiente",
        ["cobranca-nao-encontrada"] = "Cobrança não encontrada",
        ["cobranca-nao-pendente"] = "Cobrança já paga ou cancelada",
        ["fatura-nao-encontrada"] = "Fatura não encontrada",
        ["fatura-nao-disponivel"] = "Fatura não disponível para pagamento",
        ["recebivel-nao-encontrado"] = "Recebível não encontrado",
        ["recebivel-nao-antecipavel"] = "Recebível não pode ser antecipado",
    };

    public static string Tipo(string codigo) => PrefixoTipo + codigo;

    /// <summary>Converte os erros de uma falha na resposta HTTP correspondente.</summary>
    public static IResult ParaProblema(this Resultado resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        if (resultado.Sucesso)
        {
            throw new InvalidOperationException("Somente falhas são convertidas em ProblemDetails.");
        }

        var erros = resultado.Erros;

        if (erros.Any(e => e.Tipo == TipoErro.Validacao))
        {
            var porCampo = erros
                .Where(e => e.Tipo == TipoErro.Validacao)
                .GroupBy(e => e.Campo ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Mensagem).ToArray(), StringComparer.Ordinal);

            return TypedResults.ValidationProblem(
                porCampo,
                detail: "Um ou mais campos são inválidos.",
                title: Titulos[Erro.CodigoValidacao],
                type: Tipo(Erro.CodigoValidacao));
        }

        var erro = erros[0];
        return TypedResults.Problem(
            detail: erro.Mensagem,
            statusCode: Status(erro.Tipo),
            title: Titulos.GetValueOrDefault(erro.Codigo, erro.Codigo),
            type: Tipo(erro.Codigo),
            extensions: erro.Detalhes is null ? null : new Dictionary<string, object?>(erro.Detalhes, StringComparer.Ordinal));
    }

    private static int Status(TipoErro tipo) => tipo switch
    {
        TipoErro.Validacao => StatusCodes.Status400BadRequest,
        TipoErro.Conflito => StatusCodes.Status409Conflict,
        TipoErro.NaoAutorizado => StatusCodes.Status401Unauthorized,
        TipoErro.NaoEncontrado => StatusCodes.Status404NotFound,
        TipoErro.RegraDeNegocio => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };
}
