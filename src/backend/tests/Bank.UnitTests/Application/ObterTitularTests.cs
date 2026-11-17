using Bank.Application.Abstracoes;
using Bank.Application.Usuarios;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Caso de uso de consulta dos dados do titular (specs/005-usuarios-me).</summary>
public class ObterTitularTests
{
    private static readonly DadosTitular Titular = new(
        Guid.CreateVersion7(),
        "Empresa Exemplo Ltda",
        "contato@exemplo.com.br",
        "12ABC34501DE35",
        TipoConta.PessoaJuridica,
        new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Executar_UsuarioExistente_DevolveOsDadosDaConsulta()
    {
        var casoDeUso = new ObterTitular(new ConsultasFalsas(Titular));

        var resultado = await casoDeUso.ExecutarAsync(Titular.UsuarioId, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Titular, resultado.Valor);
    }

    [Fact]
    public async Task Executar_UsuarioInexistente_UsuarioNaoEncontrado()
    {
        var casoDeUso = new ObterTitular(new ConsultasFalsas(Titular));

        var resultado = await casoDeUso.ExecutarAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.Equal(ErrosUsuario.NaoEncontrado, Assert.Single(resultado.Erros));
    }

    private sealed class ConsultasFalsas(DadosTitular titular) : IUsuarioConsultas
    {
        public Task<DadosTitular?> ObterTitularAsync(Guid usuarioId, CancellationToken cancellationToken) =>
            Task.FromResult(titular.UsuarioId == usuarioId ? titular : null);

        public Task<Destinatario?> ObterDestinatarioAsync(string documento, CancellationToken cancellationToken) =>
            Task.FromResult<Destinatario?>(null);
    }
}
