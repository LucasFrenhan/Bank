using System.Security.Claims;
using Bank.Api.Seguranca;
using Bank.Domain.Permissoes;
using Microsoft.AspNetCore.Authorization;

namespace Bank.UnitTests.Api;

public class PermissaoRequirementHandlerTests
{
    [Theory]
    [InlineData("PessoaFisica", Permissao.RealizarTransferencia, true)]
    [InlineData("PessoaJuridica", Permissao.RealizarTransferencia, false)]
    [InlineData("PessoaJuridica", Permissao.GerarCobrancaCartao, true)]
    [InlineData("PessoaFisica", Permissao.GerarCobrancaCartao, false)]
    [InlineData("pessoafisica", Permissao.ReceberTransferencia, false)]
    [InlineData("Desconhecido", Permissao.ReceberTransferencia, false)]
    [InlineData(null, Permissao.ReceberTransferencia, false)]
    public async Task Handle_AutorizaConformeOPerfilDaClaim(string? tipoConta, Permissao permissao, bool autorizado)
    {
        var claims = tipoConta is null ? Array.Empty<Claim>() : [new Claim("tipo_conta", tipoConta)];
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var requisito = new PermissaoRequirement(permissao);
        var contexto = new AuthorizationHandlerContext([requisito], usuario, null);

        await new PermissaoRequirementHandler().HandleAsync(contexto);

        Assert.Equal(autorizado, contexto.HasSucceeded);
    }

    [Fact]
    public void Nome_SegueOPrefixoDoContrato()
    {
        Assert.Equal("Permissao:RealizarTransferencia", PoliticasAutorizacao.Nome(Permissao.RealizarTransferencia));
    }
}
