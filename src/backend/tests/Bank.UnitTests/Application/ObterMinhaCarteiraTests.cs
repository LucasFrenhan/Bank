using Bank.Application.Abstracoes;
using Bank.Application.Carteiras;
using Bank.Domain.Carteiras;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

public class ObterMinhaCarteiraTests
{
    private static readonly UsuarioId UsuarioId = UsuarioId.De(Guid.CreateVersion7());

    [Fact]
    public async Task Executar_UsuarioExistente_DevolveCarteiraComEscala2()
    {
        var dados = new DadosCarteira(CarteiraId.De(Guid.CreateVersion7()), UsuarioId, "Lucas", TipoConta.PessoaFisica, 10m, "BRL", DateTimeOffset.UtcNow);
        var casoDeUso = new ObterMinhaCarteira(new ConsultasFalsas(dados));

        var resultado = await casoDeUso.ExecutarAsync(UsuarioId, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(UsuarioId, resultado.Valor.UsuarioId);
        Assert.Equal("10.00", resultado.Valor.Saldo.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Executar_UsuarioInexistente_CarteiraNaoEncontrada()
    {
        var casoDeUso = new ObterMinhaCarteira(new ConsultasFalsas(null));

        var resultado = await casoDeUso.ExecutarAsync(UsuarioId, CancellationToken.None);

        Assert.Equal(ErrosCarteira.NaoEncontrada, Assert.Single(resultado.Erros));
    }

    private sealed class ConsultasFalsas(DadosCarteira? dados) : ICarteiraConsultas
    {
        public Task<DadosCarteira?> ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(dados?.UsuarioId == usuarioId ? dados : null);
    }
}
