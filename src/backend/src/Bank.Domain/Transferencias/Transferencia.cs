using Bank.Domain.Carteiras;
using Bank.Domain.Usuarios;
using Bank.SharedKernel;

namespace Bank.Domain.Transferencias;

public enum StatusTransferencia
{
    Concluida = 1,
}

/// <summary>
/// Fato publicado após cada transferência concluída (evento <c>transferencia.concluida</c> v1;
/// nomes e descrição acrescentados pela specs/010-mensageria-rabbitmq, FR-011).
/// </summary>
public sealed record TransferenciaConcluida(
    EventoId EventoId,
    TransferenciaId TransferenciaId,
    UsuarioId PagadorUsuarioId,
    UsuarioId RecebedorUsuarioId,
    decimal Valor,
    DateTimeOffset OcorridoEm,
    string PagadorNome,
    string RecebedorNome,
    string? Descricao)
{
    public const string Tipo = "transferencia.concluida";
    public const int Versao = 1;
}

/// <summary>
/// Nomes do pagador e do recebedor no momento da transferência, para o evento
/// (specs/010-mensageria-rabbitmq, FR-011). Não fazem parte da transferência gravada.
/// </summary>
public sealed record NomesTitulares(string Pagador, string Recebedor);

/// <summary>Tudo o que uma transferência concluída grava, numa única transação (FR-006).</summary>
public sealed record ExecucaoTransferencia(
    Transferencia Transferencia,
    Lancamento Saida,
    Lancamento Entrada,
    TransferenciaConcluida Evento);

/// <summary>
/// Transferência imutável entre carteiras (RN-02 a RN-06 da #8). As carteiras chegam já
/// bloqueadas pela infraestrutura; aqui ficam as regras: autotransferência, saldo, débito,
/// crédito, lançamentos e evento.
/// </summary>
public sealed class Transferencia
{
    private Transferencia()
    {
    }

    public TransferenciaId Id { get; private set; }

    public Guid ChaveIdempotencia { get; private set; }

    public CarteiraId CarteiraOrigemId { get; private set; }

    public CarteiraId CarteiraDestinoId { get; private set; }

    public decimal Valor { get; private set; }

    public string? Descricao { get; private set; }

    public StatusTransferencia Status { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public static Resultado<ExecucaoTransferencia> Executar(
        Carteira origem,
        Carteira destino,
        ValorTransferencia valor,
        string? descricao,
        Guid chaveIdempotencia,
        DateTimeOffset agora,
        NomesTitulares titulares)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(destino);
        ArgumentNullException.ThrowIfNull(valor);
        ArgumentNullException.ThrowIfNull(titulares);

        if (origem.Id == destino.Id)
        {
            return Resultado.Falha<ExecucaoTransferencia>(ErrosTransferencia.Autotransferencia);
        }

        var debito = origem.Debitar(valor.Valor, agora);
        if (!debito.Sucesso)
        {
            return Resultado.Falha<ExecucaoTransferencia>(debito.Erros);
        }

        destino.Creditar(valor.Valor, agora);

        var transferencia = new Transferencia
        {
            Id = TransferenciaId.Novo(agora),
            ChaveIdempotencia = chaveIdempotencia,
            CarteiraOrigemId = origem.Id,
            CarteiraDestinoId = destino.Id,
            Valor = valor.Valor,
            Descricao = descricao,
            Status = StatusTransferencia.Concluida,
            CriadoEm = agora,
        };

        return Resultado.Ok(new ExecucaoTransferencia(
            transferencia,
            Lancamento.DeTransferencia(origem.Id, DirecaoLancamento.Saida, valor.Valor, transferencia.Id, descricao, agora),
            Lancamento.DeTransferencia(destino.Id, DirecaoLancamento.Entrada, valor.Valor, transferencia.Id, descricao, agora),
            new TransferenciaConcluida(
                EventoId.Novo(agora), transferencia.Id, origem.UsuarioId, destino.UsuarioId, valor.Valor, agora,
                titulares.Pagador, titulares.Recebedor, descricao)));
    }
}
