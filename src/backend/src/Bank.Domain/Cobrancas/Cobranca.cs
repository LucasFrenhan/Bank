using Bank.Domain.Cartoes;
using Bank.Domain.Carteiras;
using Bank.Domain.Comum;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.Domain.Cobrancas;

public enum StatusCobranca
{
    Pendente = 1,
    Paga = 2,
    Cancelada = 3,
}

/// <summary>
/// Cobrança emitida por uma PJ (specs/014-cartao-credito-cobrancas, FR-009, FR-010, FR-013).
/// <see cref="StatusCobranca.Paga"/> e <see cref="StatusCobranca.Cancelada"/> são terminais; a recusa
/// mantém <see cref="StatusCobranca.Pendente"/>.
/// </summary>
public sealed class Cobranca
{
    public const string CampoDescricao = DescricaoTransferencia.Campo;
    public const int TamanhoMaximoDescricao = DescricaoTransferencia.TamanhoMaximo;

    private Cobranca()
    {
    }

    public CobrancaId Id { get; private set; }

    public UsuarioId LojistaUsuarioId { get; private set; }

    public CarteiraId CarteiraDestinoId { get; private set; }

    public decimal Valor { get; private set; }

    public string Descricao { get; private set; } = string.Empty;

    public StatusCobranca Status { get; private set; }

    public UsuarioId? PagadorUsuarioId { get; private set; }

    public string? CodigoBandeira { get; private set; }

    public string? UltimosDigitosCartao { get; private set; }

    public string? CodigoAutorizacao { get; private set; }

    public DateTimeOffset? PagaEm { get; private set; }

    public MotivoRecusa? UltimaRecusaMotivo { get; private set; }

    public DateTimeOffset? UltimaRecusaEm { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    /// <summary>Descrição obrigatória: 1 a 200 caracteres após remover espaços das pontas.</summary>
    public static Resultado<string> ValidarDescricao(string? descricao)
    {
        var normalizada = DescricaoTransferencia.Normalizar(descricao);
        if (!normalizada.Sucesso)
        {
            return Resultado.Falha<string>(normalizada.Erros);
        }

        return normalizada.Valor is { } texto
            ? Resultado.Ok(texto)
            : Resultado.Falha<string>(Erro.Validacao(CampoDescricao, "Informe a descrição da cobrança."));
    }

    public static Cobranca Emitir(UsuarioId lojista, CarteiraId carteiraDestino, ValorTransferencia valor, string descricao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(valor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);

        return new Cobranca
        {
            Id = CobrancaId.Novo(agora),
            LojistaUsuarioId = lojista,
            CarteiraDestinoId = carteiraDestino,
            Valor = valor.Valor,
            Descricao = descricao,
            Status = StatusCobranca.Pendente,
            CriadoEm = agora,
        };
    }

    public Resultado Cancelar(DateTimeOffset agora)
    {
        if (Status != StatusCobranca.Pendente)
        {
            return Resultado.Falha(ErrosCobranca.NaoPendente);
        }

        Status = StatusCobranca.Cancelada;
        CanceladaEm = agora;
        return Resultado.Ok();
    }

    /// <summary>Registra a recusa da bandeira; a cobrança continua pendente.</summary>
    public void RegistrarRecusa(AutorizacaoCartao autorizacao)
    {
        ArgumentNullException.ThrowIfNull(autorizacao);
        ExigirPendente();
        if (autorizacao.Motivo is null)
        {
            throw new ArgumentException("A autorização não é uma recusa.", nameof(autorizacao));
        }

        UltimaRecusaMotivo = autorizacao.Motivo;
        UltimaRecusaEm = autorizacao.CriadoEm;
    }

    internal void MarcarPaga(AutorizacaoCartao autorizacao)
    {
        ExigirPendente();
        Status = StatusCobranca.Paga;
        PagadorUsuarioId = autorizacao.PagadorUsuarioId;
        CodigoBandeira = autorizacao.CodigoBandeira;
        UltimosDigitosCartao = autorizacao.UltimosDigitos;
        CodigoAutorizacao = autorizacao.CodigoAutorizacao;
        PagaEm = autorizacao.CriadoEm;
    }

    private void ExigirPendente()
    {
        if (Status != StatusCobranca.Pendente)
        {
            throw new InvalidOperationException("A cobrança não está pendente.");
        }
    }
}
