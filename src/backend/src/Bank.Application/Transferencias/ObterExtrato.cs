using Bank.Application.Abstracoes;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;
using Bank.SharedKernel.Aplicacao;

namespace Bank.Application.Transferencias;

/// <summary>Parâmetros como chegam da query string; inválidos viram erros por parâmetro.</summary>
public sealed record ObterExtratoConsulta(UsuarioId UsuarioId, string? Pagina, string? TamanhoPagina, string? Direcao);

public sealed record Extrato(IReadOnlyList<ItemExtrato> Itens, int PaginaAtual, int TamanhoPagina, int TotalItens);

/// <summary>
/// Extrato da própria carteira (spec 006, US4; FR-012 a FR-014): lançamentos paginados, filtro
/// de direção e documento de terceiros mascarado quando for CPF.
/// </summary>
public sealed class ObterExtrato(ICarteiraConsultas carteiras, IExtratoConsultas extrato)
{
    public async Task<Resultado<Extrato>> ExecutarAsync(ObterExtratoConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var validacao = Validar(consulta);
        if (!validacao.Sucesso)
        {
            return Resultado.Falha<Extrato>(validacao.Erros);
        }

        var (paginacao, direcao) = validacao.Valor;

        var carteira = await carteiras.ObterDoUsuarioAsync(consulta.UsuarioId, cancellationToken);
        if (carteira is null)
        {
            return Resultado.Falha<Extrato>(ErrosUsuario.NaoEncontrado);
        }

        var resultado = await extrato.ObterAsync(carteira.CarteiraId, direcao, paginacao.Deslocamento, paginacao.Tamanho, cancellationToken);

        var itens = resultado.Itens
            .Select(item => item.Contraparte is null
                ? item
                : item with { Contraparte = item.Contraparte with { Documento = Documento.Ocultar(item.Contraparte.Documento) } })
            .ToList();

        return Resultado.Ok(new Extrato(itens, paginacao.Numero, paginacao.Tamanho, resultado.TotalItens));
    }

    private static Resultado<(Paginacao Paginacao, DirecaoLancamento? Direcao)> Validar(ObterExtratoConsulta consulta)
    {
        var paginacao = Paginacao.Ler(consulta.Pagina, consulta.TamanhoPagina);
        var erros = new List<Erro>(paginacao.Erros);

        DirecaoLancamento? direcao = consulta.Direcao switch
        {
            null => null,
            nameof(DirecaoLancamento.Entrada) => DirecaoLancamento.Entrada,
            nameof(DirecaoLancamento.Saida) => DirecaoLancamento.Saida,
            _ => null,
        };
        if (consulta.Direcao is not null && direcao is null)
        {
            erros.Add(Erro.Validacao("direcao", "Use 'Entrada' ou 'Saida'."));
        }

        return erros.Count > 0
            ? Resultado.Falha<(Paginacao, DirecaoLancamento?)>(erros)
            : Resultado.Ok((paginacao.Valor, direcao));
    }
}
