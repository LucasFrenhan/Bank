using Bank.Api.ErrorHandling;
using Bank.Application.Autenticacao;
using Bank.Application.Usuarios;

namespace Bank.Api.Endpoints;

/// <summary>
/// Cadastro e login (contracts/usuarios-carteiras-api.md). Somente transporte: converte o
/// corpo em comando, chama o caso de uso e traduz o resultado (constituição, Princípio II).
/// </summary>
public static class AutenticacaoEndpoints
{
    public const string Grupo = "/api/v1/autenticacao";

    public static IEndpointRouteBuilder MapAutenticacaoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Grupo).WithTags("Autenticação");

        grupo.MapPost("/registrar", RegistrarAsync)
            .WithName("Registrar")
            .WithSummary("Cadastra um usuário PF ou PJ e cria sua carteira.")
            .Produces<UsuarioRegistradoResposta>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Valida as credenciais e emite o token de acesso (JWT).")
            .Produces<LoginResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> RegistrarAsync(RegistrarRequisicao requisicao, RegistrarUsuario casoDeUso, CancellationToken cancellationToken)
    {
        var resultado = await casoDeUso.ExecutarAsync(
            new RegistrarUsuarioComando(requisicao.NomeCompleto, requisicao.Email, requisicao.Documento, requisicao.TipoConta, requisicao.Senha),
            cancellationToken);

        if (!resultado.Sucesso)
        {
            return resultado.ParaProblema();
        }

        var usuario = resultado.Valor;
        return TypedResults.Created(
            CarteirasEndpoints.RotaMinha,
            new UsuarioRegistradoResposta(
                usuario.UsuarioId.Valor,
                usuario.CarteiraId.Valor,
                usuario.NomeCompleto,
                usuario.Email,
                usuario.Documento,
                usuario.TipoConta.ToString(),
                usuario.SaldoInicial,
                usuario.Moeda));
    }

    private static async Task<IResult> LoginAsync(LoginRequisicao requisicao, Autenticar casoDeUso, CancellationToken cancellationToken)
    {
        var resultado = await casoDeUso.ExecutarAsync(new AutenticarComando(requisicao.Email, requisicao.Senha), cancellationToken);

        return resultado.Sucesso
            ? TypedResults.Ok(new LoginResposta(resultado.Valor.Token, resultado.Valor.TipoToken, resultado.Valor.ExpiraEmSegundos))
            : resultado.ParaProblema();
    }

    /// <summary>Campos opcionais: ausências e valores inválidos viram erros por campo (research R6).</summary>
    public sealed record RegistrarRequisicao(string? NomeCompleto, string? Email, string? Documento, string? TipoConta, string? Senha);

    public sealed record UsuarioRegistradoResposta(
        Guid UsuarioId,
        Guid CarteiraId,
        string NomeCompleto,
        string Email,
        string Documento,
        string TipoConta,
        decimal SaldoInicial,
        string Moeda);

    public sealed record LoginRequisicao(string? Email, string? Senha);

    public sealed record LoginResposta(string TokenAcesso, string TipoToken, int ExpiraEmSegundos);
}
