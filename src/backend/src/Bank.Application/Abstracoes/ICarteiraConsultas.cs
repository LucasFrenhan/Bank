using Bank.Domain.Bancos;
using Bank.Domain.Carteiras;
using Bank.Domain.Usuarios;

namespace Bank.Application.Abstracoes;

/// <summary>Dados de leitura da carteira de um usuário.</summary>
public sealed record DadosCarteira(
    CarteiraId CarteiraId,
    UsuarioId UsuarioId,
    string NomeTitular,
    TipoConta TipoConta,
    decimal Saldo,
    string Moeda,
    DateTimeOffset AtualizadoEm,
    DadosContaBancaria ContaBancaria);

/// <summary>Dados bancários da carteira, com o nome do banco (specs/012-dados-bancarios-carteira, FR-009).</summary>
public sealed record DadosContaBancaria(
    string CodigoBanco,
    string NomeBanco,
    string Agencia,
    string Numero,
    string Digito,
    TipoContaBancaria Tipo);

/// <summary>Consultas de leitura de carteiras (implementadas em Infrastructure).</summary>
public interface ICarteiraConsultas
{
    Task<DadosCarteira?> ObterDoUsuarioAsync(UsuarioId usuarioId, CancellationToken cancellationToken);
}
