using System.Globalization;
using Bank.Application.Abstracoes;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

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
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public async Task<Resultado<Extrato>> ExecutarAsync(ObterExtratoConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var validacao = Validar(consulta);
        if (!validacao.Sucesso)
        {
            return Resultado.Falha<Extrato>(validacao.Erros);
        }

        var (pagina, tamanho, direcao) = validacao.Valor;

        var carteira = await carteiras.ObterDoUsuarioAsync(consulta.UsuarioId, cancellationToken);
        if (carteira is null)
        {
            return Resultado.Falha<Extrato>(ErrosUsuario.NaoEncontrado);
        }

        var resultado = await extrato.ObterAsync(carteira.CarteiraId, direcao, (pagina - 1) * tamanho, tamanho, cancellationToken);

        var itens = resultado.Itens
            .Select(item => item.Contraparte is null
                ? item
                : item with { Contraparte = item.Contraparte with { Documento = Documento.Ocultar(item.Contraparte.Documento) } })
            .ToList();

        return Resultado.Ok(new Extrato(itens, pagina, tamanho, resultado.TotalItens));
    }

    private static Resultado<(int Pagina, int Tamanho, DirecaoLancamento? Direcao)> Validar(ObterExtratoConsulta consulta)
    {
        var erros = new List<Erro>();

        var pagina = 1;
        if (consulta.Pagina is not null && (!int.TryParse(consulta.Pagina, NumberStyles.None, CultureInfo.InvariantCulture, out pagina) || pagina < 1))
        {
            erros.Add(Erro.Validacao("pagina", "A página deve ser um número inteiro maior ou igual a 1."));
        }

        var tamanho = TamanhoPadrao;
        if (consulta.TamanhoPagina is not null && (!int.TryParse(consulta.TamanhoPagina, NumberStyles.None, CultureInfo.InvariantCulture, out tamanho) || tamanho is < 1 or > TamanhoMaximo))
        {
            erros.Add(Erro.Validacao("tamanhoPagina", $"O tamanho da página deve estar entre 1 e {TamanhoMaximo}."));
        }

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
            ? Resultado.Falha<(int, int, DirecaoLancamento?)>(erros)
            : Resultado.Ok((pagina, tamanho, direcao));
    }
}
