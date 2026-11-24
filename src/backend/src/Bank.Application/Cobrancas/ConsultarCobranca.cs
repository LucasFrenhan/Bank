using Bank.Application.Abstracoes;
using Bank.Application.Comum;
using Bank.Domain.Cobrancas;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;

namespace Bank.Application.Cobrancas;

/// <summary>Identificador como chega da rota (texto); inválido vira erro no campo.</summary>
public sealed record ConsultarCobrancaConsulta(UsuarioId UsuarioId, string? CobrancaId);

/// <summary>Consulta de uma cobrança por qualquer usuário autenticado (specs/014-cartao-credito-cobrancas, FR-011).</summary>
public sealed class ConsultarCobranca(ICobrancaConsultas consultas)
{
    public const string CampoIdentificador = "cobrancaId";

    public async Task<Resultado<DetalheCobranca>> ExecutarAsync(ConsultarCobrancaConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var id = Entradas.LerIdentificador(consulta.CobrancaId, CampoIdentificador);
        if (!id.Sucesso)
        {
            return Resultado.Falha<DetalheCobranca>(id.Erros);
        }

        var dados = await consultas.ObterAsync(CobrancaId.De(id.Valor), cancellationToken);
        return dados is null
            ? Resultado.Falha<DetalheCobranca>(ErrosCobranca.NaoEncontrada)
            : Resultado.Ok(DetalheCobranca.Para(consulta.UsuarioId, dados));
    }
}

/// <summary>Parâmetros como chegam da query string.</summary>
public sealed record ListarMinhasCobrancasConsulta(UsuarioId LojistaUsuarioId, string? Status, string? Pagina, string? TamanhoPagina);

/// <summary>Cobranças emitidas pelo lojista (specs/014-cartao-credito-cobrancas, FR-012).</summary>
public sealed class ListarMinhasCobrancas(ICobrancaConsultas consultas)
{
    public const string CampoStatus = "status";

    public async Task<Resultado<PaginaDe<DetalheCobranca>>> ExecutarAsync(ListarMinhasCobrancasConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var status = Entradas.LerFiltro<StatusCobranca>(consulta.Status, CampoStatus);
        var paginacao = Paginacao.Ler(consulta.Pagina, consulta.TamanhoPagina);
        if (!status.Sucesso || !paginacao.Sucesso)
        {
            return Resultado.Falha<PaginaDe<DetalheCobranca>>([.. status.Erros, .. paginacao.Erros]);
        }

        var pagina = paginacao.Valor;
        var resultado = await consultas.ListarDoLojistaAsync(consulta.LojistaUsuarioId, status.Valor, pagina.Deslocamento, pagina.Tamanho, cancellationToken);
        return Resultado.Ok(new PaginaDe<DetalheCobranca>(
            [.. resultado.Itens.Select(d => DetalheCobranca.Para(consulta.LojistaUsuarioId, d))], pagina.Numero, pagina.Tamanho, resultado.TotalItens));
    }
}
