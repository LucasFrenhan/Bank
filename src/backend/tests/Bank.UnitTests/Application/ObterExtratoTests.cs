using Bank.Application.Abstracoes;
using Bank.Application.Transferencias;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Extrato da própria carteira (spec 006, US4; FR-012 a FR-014).</summary>
public sealed class ObterExtratoTests
{
    private readonly BancoFalso _banco = new();
    private readonly ExtratoFalso _extrato = new();
    private readonly ObterExtrato _casoDeUso;
    private readonly Usuario _pf;

    public ObterExtratoTests()
    {
        _casoDeUso = new ObterExtrato(_banco, _extrato);
        _pf = _banco.Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, 10m);
    }

    [Fact]
    public async Task Padrao_Pagina1Tamanho20SemFiltro()
    {
        var resultado = await Executar(null, null, null);

        Assert.Equal((1, 20), (resultado.Valor.PaginaAtual, resultado.Valor.TamanhoPagina));
        Assert.Equal((_pf.Carteira.Id, (DirecaoLancamento?)null, 0, 20), _extrato.UltimaChamada);
    }

    [Fact]
    public async Task Pagina3Tamanho10Saida_DeslocamentoEFiltro()
    {
        var resultado = await Executar("3", "10", "Saida");

        Assert.True(resultado.Sucesso);
        Assert.Equal((_pf.Carteira.Id, (DirecaoLancamento?)DirecaoLancamento.Saida, 20, 10), _extrato.UltimaChamada);
    }

    [Theory]
    [InlineData("0", null, null, "pagina")]
    [InlineData("abc", null, null, "pagina")]
    [InlineData(null, "0", null, "tamanhoPagina")]
    [InlineData(null, "101", null, "tamanhoPagina")]
    [InlineData(null, null, "saida", "direcao")]
    [InlineData(null, null, "Todas", "direcao")]
    public async Task ParametroInvalido_ErroNoParametro(string? pagina, string? tamanho, string? direcao, string campo)
    {
        var resultado = await Executar(pagina, tamanho, direcao);

        var erro = Assert.Single(resultado.Erros);
        Assert.Equal((TipoErro.Validacao, campo), (erro.Tipo, erro.Campo));
        Assert.Null(_extrato.UltimaChamada);
    }

    [Fact]
    public async Task Contrapartes_CpfMascaradoCnpjCompletoEAberturaSemContraparte()
    {
        var agora = DateTimeOffset.UtcNow;
        _extrato.Itens =
        [
            new(Guid.NewGuid(), TipoLancamento.Transferencia, DirecaoLancamento.Saida, Guid.NewGuid(), new ContraparteExtrato("Ana", "11144477735", TipoConta.PessoaFisica), 1m, null, agora),
            new(Guid.NewGuid(), TipoLancamento.Transferencia, DirecaoLancamento.Saida, Guid.NewGuid(), new ContraparteExtrato("Loja", "12ABC34501DE35", TipoConta.PessoaJuridica), 1m, null, agora),
            new(Guid.NewGuid(), TipoLancamento.Abertura, DirecaoLancamento.Entrada, null, null, 10m, Lancamento.DescricaoAbertura, agora),
        ];

        var itens = (await Executar(null, null, null)).Valor.Itens;

        Assert.Equal("•••.444.777-••", itens[0].Contraparte!.Documento);
        Assert.Equal("12ABC34501DE35", itens[1].Contraparte!.Documento);
        Assert.Null(itens[2].Contraparte);
    }

    [Fact]
    public async Task UsuarioInexistente_NaoEncontrado()
    {
        var resultado = await _casoDeUso.ExecutarAsync(new ObterExtratoConsulta(Guid.NewGuid(), null, null, null), CancellationToken.None);

        Assert.Equal(ErrosUsuario.NaoEncontrado, Assert.Single(resultado.Erros));
    }

    private Task<Resultado<Extrato>> Executar(string? pagina, string? tamanho, string? direcao) =>
        _casoDeUso.ExecutarAsync(new ObterExtratoConsulta(_pf.Id, pagina, tamanho, direcao), CancellationToken.None);

    private sealed class ExtratoFalso : IExtratoConsultas
    {
        public IReadOnlyList<ItemExtrato> Itens { get; set; } = [];

        public (Guid, DirecaoLancamento?, int, int)? UltimaChamada { get; private set; }

        public Task<PaginaExtrato> ObterAsync(Guid carteiraId, DirecaoLancamento? direcao, int deslocamento, int quantidade, CancellationToken cancellationToken)
        {
            UltimaChamada = (carteiraId, direcao, deslocamento, quantidade);
            return Task.FromResult(new PaginaExtrato(Itens, Itens.Count));
        }
    }
}
