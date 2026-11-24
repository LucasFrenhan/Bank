using Bank.Application.Abstracoes;
using Bank.Application.Cartoes;
using Bank.Application.Cobrancas;
using Bank.Application.Faturas;
using Bank.Application.Observabilidade;
using Bank.Application.Processamento;
using Bank.Application.Recebiveis;
using Bank.Application.Transferencias;
using Bank.Domain.Carteiras;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Recebiveis;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Relógio que os testes avançam.</summary>
internal sealed class RelogioDeTeste(DateTimeOffset agora) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = agora;

    public override DateTimeOffset GetUtcNow() => Agora;
}

/// <summary>Deriva dados previsíveis: o número vem do identificador e da variação; o CVV é sempre 123.</summary>
internal sealed class DadosCartaoFalsos : IDadosCartao
{
    public const string CvvPadrao = "123";

    public DadosDerivados Derivar(CartaoId cartaoId, Bandeira bandeira, int variacao, ValidadeCartao validade)
    {
        var numero = NumeroCartao.DePrefixo((bandeira.Codigo == Bandeira.CodigoVisa ? "40000" : "55555") + $"{variacao}{Math.Abs(cartaoId.GetHashCode()) % 1_000_000_000:D9}");
        return new DadosDerivados(numero, Cvv.Criar(CvvPadrao).Valor, "impressao-" + variacao + cartaoId);
    }

    public DadosDerivados Recuperar(Cartao cartao) => Derivar(cartao.Id, Bandeira.DeCodigo(cartao.CodigoBandeira)!, cartao.Variacao, cartao.Validade);

    public bool Conferem(Cartao cartao, NumeroCartao numero, ValidadeCartao validade, Cvv cvv)
    {
        var dados = Recuperar(cartao);
        return dados.Numero.Valor == numero.Valor && cartao.Validade == validade && dados.Cvv.Valor == cvv.Valor;
    }
}

/// <summary>
/// Banco em memória para os casos de uso da spec 014. Desfazer não reverte objetos já alterados (os testes
/// verificam o que foi gravado em <see cref="ConfirmarAsync"/>); a atomicidade real é dos testes de integração.
/// </summary>
internal sealed class BancoCartoes : IUsuarioConsultas, ICarteiraConsultas, ICartaoRepositorio, ICobrancaRepositorio, ICobrancaConsultas, IFaturaRepositorio,
    IFaturaConsultas, IRecebivelRepositorio, IRecebivelConsultas, ICarteiraRepositorio, IUnidadeDeTrabalho, IDisposable
{
    private readonly List<Action> _pendentes = [];
    private bool _emTransacao;

    private readonly MetricasCapturadasBase _metricasCartoes = new(f => new MetricasCartoes(f));
    private readonly MetricasCapturadasBase _metricasProcessamento = new(f => new MetricasProcessamento(f));

    public RelogioDeTeste Relogio { get; } = new(CartoesDeTeste.Agora);

    public CalendarioBrasilia Calendario { get; } = new();

    public DadosCartaoFalsos Dados { get; } = new();

    public CartoesOptions Opcoes { get; } = new();

    public List<Usuario> Usuarios { get; } = [];

    public List<Cartao> Cartoes { get; } = [];

    public List<Cobranca> Cobrancas { get; } = [];

    public List<AutorizacaoCartao> Autorizacoes { get; } = [];

    public List<ExecucaoPagamento> Pagamentos { get; } = [];

    public List<Fatura> Faturas { get; } = [];

    public List<LancamentoFatura> LancamentosFatura { get; } = [];

    public List<Recebivel> Recebiveis { get; } = [];

    public List<Lancamento> Lancamentos { get; } = [];

    /// <summary>Resultado das próximas confirmações, em ordem (simula restrições únicas).</summary>
    public Queue<ResultadoGravacao> Confirmacoes { get; } = new();

    /// <summary>Executado quando uma entidade é bloqueada (simula outra requisição).</summary>
    public Action? AoBloquear { get; set; }

    /// <summary>Falha ao bloquear o item com este identificador.</summary>
    public Guid? FalharAoBloquear { get; set; }

    public int Desfeitas { get; private set; }

    public MetricasCartoes MetricasCartoes => (MetricasCartoes)_metricasCartoes.Instancia;

    public MetricasProcessamento MetricasProcessamento => (MetricasProcessamento)_metricasProcessamento.Instancia;

    public IEnumerable<(string Nome, double Valor, Dictionary<string, string?> Tags)> Medicoes => [.. _metricasCartoes.Medicoes, .. _metricasProcessamento.Medicoes];

    public void Dispose()
    {
        _metricasCartoes.Dispose();
        _metricasProcessamento.Dispose();
    }

    public Usuario CriarPf(decimal saldo = 5000m) => Criar("Lucas Silva", "52998224725", TipoConta.PessoaFisica, saldo);

    public Usuario CriarPj() => Criar("Cafeteria Ltda", "12ABC34501DE35", TipoConta.PessoaJuridica, 0m);

    /// <summary>PF com cartão já emitido.</summary>
    public (Usuario Pf, Cartao Cartao, DadosDerivados Dados) CriarPfComCartao(decimal limite = 5000m, decimal saldo = 5000m)
    {
        var pf = CriarPf(saldo);
        var id = CartaoId.Novo(Relogio.Agora);
        var validade = ValidadeCartao.DaEmissao(CartoesDeTeste.Hoje);
        var dados = Dados.Derivar(id, Bandeira.Visa, 0, validade);
        var cartao = Cartao.Emitir(id, pf.Id, Bandeira.Visa, pf.NomeCompleto, validade, 0, dados.Numero.UltimosDigitos, dados.ImpressaoDigital, limite, Relogio.Agora);
        Cartoes.Add(cartao);
        return (pf, cartao, dados);
    }

    public Cobranca CriarCobranca(Usuario lojista, decimal valor = 299.90m)
    {
        var cobranca = Cobranca.Emitir(lojista.Id, lojista.Carteira.Id, ValorTransferencia.Criar(valor).Valor, "Café", CodigoCobranca.Novo(), Relogio.Agora);
        Cobrancas.Add(cobranca);
        return cobranca;
    }

    /// <summary>Compra aprovada no dia do relógio, com a fatura do ciclo.</summary>
    public ExecucaoPagamento Comprar(Cartao cartao, Usuario lojista, decimal valor)
    {
        var hoje = Calendario.Hoje(Relogio.Agora).Data;
        var fatura = ObterOuAbrirAsync(cartao.Id, CompetenciaFatura.De(hoje), Relogio.Agora, default).Result;
        if (!Faturas.Contains(fatura))
        {
            Faturas.Add(fatura);
        }

        var execucao = PagamentoCobranca.Executar(CriarCobranca(lojista, valor), cartao, fatura, lojista.NomeCompleto, hoje, Guid.NewGuid(), Relogio.Agora).Valor;
        Pagamentos.Add(execucao);
        Recebiveis.Add(execucao.Recebivel);
        _pendentes.Clear();
        return execucao;
    }

    public PagarCobranca PagarCobranca(LoggerCapturado<PagarCobranca>? logger = null) =>
        new(this, this, this, this, Dados, this, Calendario, Relogio, MetricasCartoes, logger ?? new LoggerCapturado<PagarCobranca>());

    public ObterFatura ObterFatura() => new(this, this, new MonitorDeOpcoes<CartoesOptions>(Opcoes), Calendario, Relogio);

    public PagarFatura PagarFatura() =>
        new(this, this, this, this, this, ObterFatura(), new MonitorDeOpcoes<CartoesOptions>(Opcoes), Calendario, Relogio, MetricasCartoes, new LoggerCapturado<PagarFatura>());

    public AnteciparRecebivel AnteciparRecebivel() =>
        new(this, this, this, this, Calendario, Relogio, MetricasCartoes, new LoggerCapturado<AnteciparRecebivel>());

    public ProcessarDia ProcessarDia(LoggerCapturado<ProcessarDia>? logger = null) =>
        new(this, this, this, this, this, Calendario, Relogio, MetricasProcessamento, logger ?? new LoggerCapturado<ProcessarDia>());

    public Task IniciarAsync(CancellationToken cancellationToken)
    {
        Assert.False(_emTransacao, "Transação já iniciada.");
        _emTransacao = true;
        return Task.CompletedTask;
    }

    public Task<ResultadoGravacao> ConfirmarAsync(CancellationToken cancellationToken)
    {
        Assert.True(_emTransacao, "Sem transação.");
        _emTransacao = false;
        var resultado = Confirmacoes.TryDequeue(out var forcado) ? forcado : ResultadoGravacao.Ok;
        if (resultado == ResultadoGravacao.Ok)
        {
            _pendentes.ForEach(gravar => gravar());
        }

        _pendentes.Clear();
        return Task.FromResult(resultado);
    }

    public Task DesfazerAsync(CancellationToken cancellationToken)
    {
        _emTransacao = false;
        _pendentes.Clear();
        Desfeitas++;
        return Task.CompletedTask;
    }

    public Task<DadosTitular?> ObterTitularAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var u = Usuarios.Find(x => x.Id == usuarioId);
        return Task.FromResult(u is null ? null : new DadosTitular(u.Id, u.NomeCompleto, u.Email, u.Documento, u.TipoConta, u.CriadoEm));
    }

    public Task<Destinatario?> ObterDestinatarioAsync(string documento, CancellationToken cancellationToken) => throw new NotSupportedException();

    Task<DadosCarteira?> ICarteiraConsultas.ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken)
    {
        var u = Usuarios.Find(x => x.Id == usuarioId);
        return Task.FromResult(u is null ? null : new DadosCarteira(u.Carteira.Id, u.Id, u.NomeCompleto, u.TipoConta, u.Carteira.Saldo, u.Carteira.Moeda, u.Carteira.AtualizadoEm, new DadosContaBancaria("001", "One Bank", "0001", "00000001", "6", u.Carteira.TipoContaBancaria)));
    }

    Task<Cartao?> ICartaoRepositorio.ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken) =>
        Task.FromResult(Cartoes.Find(c => c.UsuarioId == usuarioId));

    public Task<Cartao?> BloquearAsync(CartaoId cartaoId, CancellationToken cancellationToken) => Bloquear(cartaoId.Valor, Cartoes.Find(c => c.Id == cartaoId));

    public void Adicionar(Cartao cartao) => _pendentes.Add(() => Cartoes.Add(cartao));

    public Task<decimal> SomarComprasALancarAsync(CartaoId cartaoId, CancellationToken cancellationToken) =>
        Task.FromResult(ALancar().Where(c => c.CartaoId == cartaoId).Sum(c => c.Valor));

    public void Adicionar(Cobranca cobranca) => _pendentes.Add(() => Cobrancas.Add(cobranca));

    public Task<Cobranca?> BloquearAsync(CobrancaId cobrancaId, CancellationToken cancellationToken) => Bloquear(cobrancaId.Valor, Cobrancas.Find(c => c.Id == cobrancaId));

    public void Adicionar(AutorizacaoCartao autorizacao) => _pendentes.Add(() => Autorizacoes.Add(autorizacao));

    public void Adicionar(ExecucaoPagamento execucao) => _pendentes.Add(() =>
    {
        Autorizacoes.Add(execucao.Autorizacao);
        Pagamentos.Add(execucao);
        Recebiveis.Add(execucao.Recebivel);
    });

    public Task<PagamentoAprovado?> ObterPagamentoPorChaveAsync(UsuarioId pagador, Guid chaveIdempotencia, CancellationToken cancellationToken)
    {
        var p = Pagamentos.Find(x => x.Autorizacao.PagadorUsuarioId == pagador && x.Autorizacao.ChaveIdempotencia == chaveIdempotencia);
        return Task.FromResult(p is null ? null : new PagamentoAprovado(p.Compra.CobrancaId, p.Compra.Valor, p.Autorizacao.CodigoBandeira!, p.Autorizacao.UltimosDigitos, p.Autorizacao.CodigoAutorizacao!, p.Compra.Estabelecimento, p.Autorizacao.CriadoEm));
    }

    public Task<DadosCobranca?> ObterAsync(CobrancaId cobrancaId, CancellationToken cancellationToken) =>
        Task.FromResult(Cobrancas.Where(c => c.Id == cobrancaId).Select(Projetar).SingleOrDefault());

    public Task<DadosCobranca?> ObterPorCodigoAsync(CodigoCobranca codigo, CancellationToken cancellationToken) =>
        Task.FromResult(Cobrancas.Where(c => c.Codigo == codigo).Select(Projetar).SingleOrDefault());

    public Task<Pagina<DadosCobranca>> ListarDoLojistaAsync(UsuarioId lojista, StatusCobranca? status, int deslocamento, int quantidade, CancellationToken cancellationToken)
    {
        var todas = Cobrancas.Where(c => c.LojistaUsuarioId == lojista && (status is null || c.Status == status)).Select(Projetar).ToList();
        return Task.FromResult(new Pagina<DadosCobranca>([.. todas.Skip(deslocamento).Take(quantidade)], todas.Count));
    }

    public Task<Fatura> ObterOuAbrirAsync(CartaoId cartaoId, CompetenciaFatura competencia, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var fatura = Faturas.Find(f => f.CartaoId == cartaoId && f.Competencia == competencia.PrimeiroDiaDoMes);
        if (fatura is null)
        {
            fatura = Fatura.Abrir(cartaoId, competencia, agora);
            _pendentes.Add(() => Faturas.Add(fatura));
        }

        return Task.FromResult(fatura);
    }

    public Task<Fatura?> ObterAsync(CartaoId cartaoId, CompetenciaFatura competencia, CancellationToken cancellationToken) =>
        Task.FromResult(Faturas.Find(f => f.CartaoId == cartaoId && f.Competencia == competencia.PrimeiroDiaDoMes));

    public Task<CompetenciaFatura?> ObterCompetenciaPorChaveAsync(CartaoId cartaoId, Guid chaveIdempotencia, CancellationToken cancellationToken)
    {
        var fatura = Faturas.Find(f => f.CartaoId == cartaoId && f.ChaveIdempotencia == chaveIdempotencia);
        return Task.FromResult<CompetenciaFatura?>(fatura is null ? null : CompetenciaFatura.DoMes(fatura.Competencia));
    }

    public Task<bool> ExisteVencidaAsync(CartaoId cartaoId, DateOnly hoje, CancellationToken cancellationToken) =>
        Task.FromResult(Faturas.Exists(f => f.CartaoId == cartaoId && f.Situacao(hoje) == SituacaoFatura.Vencida));

    public Task<IReadOnlyList<CartaoId>> ListarCartoesAProcessarAsync(DateOnly hoje, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CartaoId>>([.. Faturas
            .Where(f => f.LiberadaEm is null && (f.Fechamento < hoje || ALancar().Any(c => c.FaturaId == f.Id && c.DataCompra < hoje)))
            .Select(f => f.CartaoId).Distinct()]);

    public Task<IReadOnlyList<FaturaAProcessar>> ListarNaoLiberadasAsync(CartaoId cartaoId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FaturaAProcessar>>([.. Faturas
            .Where(f => f.CartaoId == cartaoId && f.LiberadaEm is null)
            .Select(f => new FaturaAProcessar(f, [.. ALancar().Where(c => c.FaturaId == f.Id)]))]);

    public void Adicionar(LancamentoFatura lancamento) => _pendentes.Add(() => LancamentosFatura.Add(lancamento));

    public Task<IReadOnlyList<Fatura>> ListarAsync(CartaoId cartaoId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Fatura>>([.. Faturas.Where(f => f.CartaoId == cartaoId)]);

    public Task<IReadOnlyList<ItemFatura>> ListarLancamentosAsync(FaturaId faturaId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ItemFatura>>([.. LancamentosFatura.Where(l => l.FaturaId == faturaId).Select(l => new ItemFatura(l.Estabelecimento, l.Descricao, l.Valor, l.DataCompra))]);

    public async Task<RecebivelBloqueado?> BloquearAsync(RecebivelId recebivelId, CancellationToken cancellationToken)
    {
        var recebivel = await Bloquear(recebivelId.Valor, Recebiveis.Find(r => r.Id == recebivelId));
        return recebivel is null ? null : new RecebivelBloqueado(recebivel, "Café");
    }

    public Task<RecebivelId?> ObterPorChaveAsync(UsuarioId lojista, Guid chaveIdempotencia, CancellationToken cancellationToken) =>
        Task.FromResult<RecebivelId?>(Recebiveis.Find(r => r.LojistaUsuarioId == lojista && r.ChaveIdempotencia == chaveIdempotencia)?.Id);

    public Task<IReadOnlyList<RecebivelId>> ListarDevidosAsync(DateOnly hoje, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RecebivelId>>([.. Recebiveis.Where(r => r.Situacao == SituacaoRecebivel.Pendente && r.DataPrevista <= hoje).Select(r => r.Id)]);

    public Task<DadosRecebivel?> ObterAsync(UsuarioId lojista, RecebivelId recebivelId, CancellationToken cancellationToken) =>
        Task.FromResult(Recebiveis.Where(r => r.LojistaUsuarioId == lojista && r.Id == recebivelId).Select(Projetar).SingleOrDefault());

    public Task<Pagina<DadosRecebivel>> ListarAsync(UsuarioId lojista, SituacaoRecebivel? situacao, int deslocamento, int quantidade, CancellationToken cancellationToken)
    {
        var todos = Recebiveis.Where(r => r.LojistaUsuarioId == lojista && (situacao is null || r.Situacao == situacao)).Select(Projetar).ToList();
        return Task.FromResult(new Pagina<DadosRecebivel>([.. todos.Skip(deslocamento).Take(quantidade)], todos.Count));
    }

    public Task<Carteira?> BloquearAsync(CarteiraId carteiraId, CancellationToken cancellationToken) =>
        Bloquear(carteiraId.Valor, Usuarios.Find(u => u.Carteira.Id == carteiraId)?.Carteira);

    public void Adicionar(Lancamento lancamento) => _pendentes.Add(() => Lancamentos.Add(lancamento));

    private Task<T?> Bloquear<T>(Guid id, T? entidade)
        where T : class
    {
        Assert.True(_emTransacao, "Bloqueio fora de transação.");
        if (FalharAoBloquear == id)
        {
            throw new InvalidOperationException("Falha de teste.");
        }

        AoBloquear?.Invoke();
        return Task.FromResult(entidade);
    }

    private IEnumerable<CompraCartao> ALancar() => Pagamentos.Select(p => p.Compra).Where(c => !LancamentosFatura.Exists(l => l.CompraId == c.Id));

    private Usuario Criar(string nome, string documento, TipoConta tipo, decimal saldo)
    {
        var usuario = Usuario.Criar(nome, Email.Criar($"{Guid.NewGuid():N}@exemplo.com.br").Valor, Documento.Criar(documento, tipo).Valor, "hash", ContasDeTeste.Corrente(Usuarios.Count + 1), saldo, Relogio.Agora);
        Usuarios.Add(usuario);
        return usuario;
    }

    private DadosCobranca Projetar(Cobranca c)
    {
        var lojista = Usuarios.Find(u => u.Id == c.LojistaUsuarioId)!;
        return new DadosCobranca(c.Id, c.Codigo, c.LojistaUsuarioId, lojista.NomeCompleto, lojista.Documento, c.Valor, c.Descricao, c.Status, c.CriadoEm, c.PagaEm, c.CanceladaEm,
            c.PagadorUsuarioId, c.CodigoBandeira, c.UltimosDigitosCartao, c.CodigoAutorizacao, c.UltimaRecusaMotivo, c.UltimaRecusaEm);
    }

    private static DadosRecebivel Projetar(Recebivel r) =>
        new(r.Id, r.CobrancaId, "Café", r.Situacao, r.ValorBruto, r.Taxa, r.ValorLiquido, r.DataPrevista, r.RecebidoEm, r.CriadoEm);
}
