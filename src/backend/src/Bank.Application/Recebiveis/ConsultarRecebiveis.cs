using Bank.Application.Abstracoes;
using Bank.Domain.Recebiveis;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;

namespace Bank.Application.Recebiveis;

/// <summary>Parâmetros como chegam da query string.</summary>
public sealed record ListarRecebiveisConsulta(UsuarioId LojistaUsuarioId, string? Situacao, string? Pagina, string? TamanhoPagina);

/// <summary>Recebíveis do lojista, dos mais novos para os mais antigos (specs/014-cartao-credito-cobrancas, FR-033).</summary>
public sealed class ListarRecebiveis(IRecebivelConsultas consultas)
{
    public const string CampoSituacao = "situacao";

    public async Task<Resultado<PaginaDe<DadosRecebivel>>> ExecutarAsync(ListarRecebiveisConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var situacao = Entradas.LerFiltro<SituacaoRecebivel>(consulta.Situacao, CampoSituacao);
        var paginacao = Paginacao.Ler(consulta.Pagina, consulta.TamanhoPagina);
        if (!situacao.Sucesso || !paginacao.Sucesso)
        {
            return Resultado.Falha<PaginaDe<DadosRecebivel>>([.. situacao.Erros, .. paginacao.Erros]);
        }

        var pagina = paginacao.Valor;
        var resultado = await consultas.ListarAsync(consulta.LojistaUsuarioId, situacao.Valor, pagina.Deslocamento, pagina.Tamanho, cancellationToken);
        return Resultado.Ok(new PaginaDe<DadosRecebivel>(resultado.Itens, pagina.Numero, pagina.Tamanho, resultado.TotalItens));
    }
}

public sealed record ObterRecebivelConsulta(UsuarioId LojistaUsuarioId, string? RecebivelId);

/// <summary>Um recebível do próprio lojista; de outro, como inexistente.</summary>
public sealed class ObterRecebivel(IRecebivelConsultas consultas)
{
    public const string CampoIdentificador = "recebivelId";

    public async Task<Resultado<DadosRecebivel>> ExecutarAsync(ObterRecebivelConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var id = Entradas.LerIdentificador(consulta.RecebivelId, CampoIdentificador);
        if (!id.Sucesso)
        {
            return Resultado.Falha<DadosRecebivel>(id.Erros);
        }

        var dados = await consultas.ObterAsync(consulta.LojistaUsuarioId, RecebivelId.De(id.Valor), cancellationToken);
        return dados is null ? Resultado.Falha<DadosRecebivel>(ErrosRecebivel.NaoEncontrado) : Resultado.Ok(dados);
    }
}
