using Bank.Application.Abstracoes;
using Bank.Domain.Carteiras;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests.Application;

/// <summary>Banco em memória para os casos de uso de transferência.</summary>
internal sealed class BancoFalso : ICarteiraConsultas, IUsuarioConsultas, ITransferenciaRepositorio, IUnidadeDeTransferencia
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    public List<Usuario> Usuarios { get; } = [];

    public List<ExecucaoTransferencia> Gravadas { get; } = [];

    private ExecucaoTransferencia? _pendente;

    /// <summary>Simula outra requisição gravando a mesma chave durante a confirmação.</summary>
    public Action? AntesDeConfirmar { get; set; }

    public bool ForcarChaveDuplicada { get; set; }

    public bool OmitirCarteirasNoBloqueio { get; set; }

    public int Desfeitas { get; private set; }

    public Usuario Criar(string nome, string documento, TipoConta tipo, decimal saldo)
    {
        var usuario = Usuario.Criar(nome, Email.Criar($"{Guid.NewGuid():N}@exemplo.com.br").Valor, Documento.Criar(documento, tipo).Valor, "hash", saldo, Agora);
        Usuarios.Add(usuario);
        return usuario;
    }

    public Task<DadosCarteira?> ObterDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var u = Usuarios.Find(x => x.Id == usuarioId);
        return Task.FromResult(u is null ? null : new DadosCarteira(u.Carteira.Id, u.Id, u.NomeCompleto, u.TipoConta, u.Carteira.Saldo, u.Carteira.Moeda, u.Carteira.AtualizadoEm));
    }

    public Task<DadosTitular?> ObterTitularAsync(Guid usuarioId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Destinatario?> ObterDestinatarioAsync(string documento, CancellationToken cancellationToken)
    {
        var u = Usuarios.Find(x => x.Documento == documento);
        return Task.FromResult(u is null ? null : new Destinatario(u.Id, u.Carteira.Id, u.NomeCompleto, u.Documento, u.TipoConta));
    }

    public Task<Comprovante?> ObterComprovantePorChaveAsync(Guid carteiraOrigemId, Guid chaveIdempotencia, CancellationToken cancellationToken)
    {
        var e = Gravadas.Find(x => x.Transferencia.CarteiraOrigemId == carteiraOrigemId && x.Transferencia.ChaveIdempotencia == chaveIdempotencia);
        if (e is null)
        {
            return Task.FromResult<Comprovante?>(null);
        }

        var pagador = Usuarios.Single(u => u.Carteira.Id == e.Transferencia.CarteiraOrigemId);
        var recebedor = Usuarios.Single(u => u.Carteira.Id == e.Transferencia.CarteiraDestinoId);
        return Task.FromResult<Comprovante?>(new Comprovante(
            e.Transferencia.Id, chaveIdempotencia,
            new PagadorComprovante(pagador.Id, pagador.NomeCompleto),
            new RecebedorComprovante(recebedor.Id, recebedor.NomeCompleto, recebedor.Documento, recebedor.TipoConta),
            e.Transferencia.Valor, e.Transferencia.Descricao, e.Transferencia.Status, e.Transferencia.CriadoEm));
    }

    public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<Carteira>> BloquearCarteirasAsync(Guid carteiraA, Guid carteiraB, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Carteira>>(OmitirCarteirasNoBloqueio
            ? []
            : [.. Usuarios.Select(u => u.Carteira).Where(c => c.Id == carteiraA || c.Id == carteiraB).OrderBy(c => c.Id)]);

    public void Adicionar(ExecucaoTransferencia execucao) => _pendente = execucao;

    public Task<ResultadoConfirmacao> ConfirmarAsync(CancellationToken cancellationToken)
    {
        AntesDeConfirmar?.Invoke();
        if (ForcarChaveDuplicada)
        {
            _pendente = null;
            return Task.FromResult(ResultadoConfirmacao.ChaveDuplicada);
        }

        Gravadas.Add(_pendente!);
        _pendente = null;
        return Task.FromResult(ResultadoConfirmacao.Ok);
    }

    public Task DesfazerAsync(CancellationToken cancellationToken)
    {
        Desfeitas++;
        _pendente = null;
        return Task.CompletedTask;
    }
}
