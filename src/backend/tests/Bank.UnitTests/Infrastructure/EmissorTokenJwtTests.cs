using Bank.Domain.Usuarios;
using Bank.Infrastructure.Seguranca;
using Bank.UnitTests.Application;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Bank.UnitTests.Infrastructure;

public class EmissorTokenJwtTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    private static readonly JwtOptions Opcoes = new()
    {
        Emissor = "bank-api",
        Audiencia = "bank-api",
        ExpiracaoSegundos = 3600,
        ChaveAssinatura = new string('k', 48),
    };

    [Fact]
    public async Task Emitir_TokenHs256ComAsClaimsDoContrato()
    {
        var usuario = Usuario.Criar(
            "Lucas Silva",
            Email.Criar("lucas@exemplo.com.br").Valor,
            Documento.Criar("52998224725", TipoConta.PessoaFisica).Valor,
            "hash",
            10m,
            Agora);

        var emitido = new EmissorTokenJwt(Options.Create(Opcoes), new RelogioFixo(Agora)).Emitir(usuario);

        Assert.Equal(3600, emitido.ExpiraEmSegundos);
        var token = new JsonWebToken(emitido.Token);
        Assert.Equal("HS256", token.Alg);
        Assert.Equal(usuario.Id.ToString(), token.Subject);
        Assert.Equal("lucas@exemplo.com.br", token.GetClaim("email").Value);
        Assert.Equal("PessoaFisica", token.GetClaim("tipo_conta").Value);
        Assert.Equal(usuario.Carteira.Id.ToString(), token.GetClaim("carteira_id").Value);
        Assert.False(string.IsNullOrEmpty(token.Id));
        Assert.Equal("bank-api", token.Issuer);
        Assert.Contains("bank-api", token.Audiences);
        Assert.Equal(Agora.UtcDateTime, token.IssuedAt);
        Assert.Equal(TimeSpan.FromSeconds(3600), token.ValidTo - token.IssuedAt);

        // O token é válido com os parâmetros que a API usa.
        var validacao = await new JsonWebTokenHandler().ValidateTokenAsync(emitido.Token, ParametrosSemTempo());
        Assert.True(validacao.IsValid, validacao.Exception?.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("curta-demais-31-bytes-xxxxxxxxx")]
    public void Validador_ChaveAusenteOuCurta_Falha(string chave)
    {
        var resultado = new ValidadorJwtOptions().Validate(null, new JwtOptions { Emissor = "e", Audiencia = "a", ChaveAssinatura = chave });

        Assert.True(resultado.Failed);
        Assert.Contains("Jwt:ChaveAssinatura", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validador_ConfiguracaoCompleta_Sucesso()
    {
        Assert.True(new ValidadorJwtOptions().Validate(null, Opcoes).Succeeded);
    }

    private static Microsoft.IdentityModel.Tokens.TokenValidationParameters ParametrosSemTempo()
    {
        var parametros = Opcoes.CriarParametrosValidacao();
        parametros.ValidateLifetime = false; // o token foi emitido com relógio fixo no passado
        return parametros;
    }
}
