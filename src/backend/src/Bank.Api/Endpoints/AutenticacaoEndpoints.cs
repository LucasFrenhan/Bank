using Bank.Api.ErrorHandling;
using Bank.Api.Seguranca;
using Bank.Application.Autenticacao;
using Bank.Application.Usuarios;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bank.Api.Endpoints;

/// <summary>
/// Cadastro, login, renovação e saída (contracts/usuarios-carteiras-api.md; specs/016-sessao-refresh-token,
/// contracts/autenticacao-sessao-api.md). Somente transporte: converte o
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
            .WithSummary("Valida as credenciais, inicia a sessão e emite o token de acesso (JWT).")
            .WithDescription("Grava a credencial de renovação no cookie `bank_renovacao` (HttpOnly, Secure, SameSite=Strict, Path=/api/v1/autenticacao). Um cookie de sessão anterior é revogado.")
            .Produces<LoginResposta>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/renovar", RenovarAsync)
            .WithName("RenovarSessao")
            .WithSummary("Troca a credencial de renovação do cookie por uma nova e emite um novo token de acesso.")
            .WithDescription("Lê e regrava o cookie `bank_renovacao`. Qualquer recusa (cookie ausente, vencido, revogado, sessão inativa, limite absoluto ou reuso) responde 401 `nao-autenticado` e remove o cookie.")
            .Produces<LoginResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/sair", SairAsync)
            .WithName("Sair")
            .WithSummary("Encerra a sessão do cookie no servidor e remove o cookie.")
            .WithDescription("Idempotente: responde 204 com ou sem cookie. O token de acesso já emitido vale até expirar.")
            .Produces(StatusCodes.Status204NoContent);

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

    private static async Task<IResult> LoginAsync(LoginRequisicao requisicao, Autenticar casoDeUso, HttpContext contexto, CancellationToken cancellationToken)
    {
        var resultado = await casoDeUso.ExecutarAsync(
            new AutenticarComando(requisicao.Email, requisicao.Senha, CookieRenovacao.Ler(contexto.Request)),
            cancellationToken);

        return resultado.Sucesso ? Emitir(contexto.Response, resultado.Valor) : resultado.ParaProblema();
    }

    private static async Task<IResult> RenovarAsync(RenovarSessao casoDeUso, HttpContext contexto, CancellationToken cancellationToken)
    {
        var resultado = await casoDeUso.ExecutarAsync(CookieRenovacao.Ler(contexto.Request), cancellationToken);
        if (resultado.Sucesso)
        {
            return Emitir(contexto.Response, resultado.Valor);
        }

        CookieRenovacao.Remover(contexto.Response);
        return resultado.ParaProblema();
    }

    private static async Task<IResult> SairAsync(EncerrarSessao casoDeUso, HttpContext contexto, CancellationToken cancellationToken)
    {
        await casoDeUso.ExecutarAsync(CookieRenovacao.Ler(contexto.Request), cancellationToken);
        CookieRenovacao.Remover(contexto.Response);
        return TypedResults.NoContent();
    }

    /// <summary>Cookie novo, <c>no-store</c> e o corpo 1.1.0 (o valor da credencial nunca vai no corpo).</summary>
    private static Ok<LoginResposta> Emitir(HttpResponse resposta, SessaoEmitida sessao)
    {
        CookieRenovacao.Escrever(resposta, sessao.Credencial, sessao.CredencialExpiraEmSegundos);
        resposta.Headers.CacheControl = "no-store";
        return TypedResults.Ok(new LoginResposta(
            sessao.Token,
            sessao.TipoToken,
            sessao.ExpiraEmSegundos,
            sessao.SessaoExpiraPorInatividadeEmSegundos,
            sessao.SessaoExpiraEmSegundos));
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

    /// <summary>Corpo do login e da renovação (contrato 1.1.0: prazos da sessão em segundos, aditivos).</summary>
    public sealed record LoginResposta(
        string TokenAcesso,
        string TipoToken,
        int ExpiraEmSegundos,
        int SessaoExpiraPorInatividadeEmSegundos,
        int SessaoExpiraEmSegundos);
}
