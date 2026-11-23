using Bank.Application.Abstracoes;
using Bank.Application.Observabilidade;
using Bank.Domain.Bancos;
using Bank.Domain.Carteiras;
using Bank.Domain.Comum;
using Bank.Domain.Usuarios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bank.Application.Usuarios;

/// <summary>Dados de cadastro; todos opcionais para que ausências virem erros por campo (research R6).</summary>
public sealed record RegistrarUsuarioComando(
    string? NomeCompleto,
    string? Email,
    string? Documento,
    string? TipoConta,
    string? Senha);

public sealed record UsuarioRegistrado(
    UsuarioId UsuarioId,
    CarteiraId CarteiraId,
    string NomeCompleto,
    string Email,
    string Documento,
    TipoConta TipoConta,
    decimal SaldoInicial,
    string Moeda);

/// <summary>
/// Caso de uso de cadastro (US1): valida todos os campos, garante unicidade de e-mail e
/// documento e cria usuário + carteira atomicamente (RN-01 a RN-05). A carteira nasce sempre como
/// conta corrente no banco 001, agência 0001, com o próximo número sequencial; o comando não
/// escolhe o tipo (specs/012-dados-bancarios-carteira, FR-005, FR-006).
/// </summary>
public sealed partial class RegistrarUsuario(
    IUsuarioRepositorio repositorio,
    IGeradorNumeroConta geradorNumeroConta,
    IHasherSenha hasherSenha,
    IOptions<SaldoInicialOptions> saldoInicial,
    TimeProvider relogio,
    MetricasUsuarios metricas,
    ILogger<RegistrarUsuario> logger)
{
    public async Task<Resultado<UsuarioRegistrado>> ExecutarAsync(RegistrarUsuarioComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var validacao = Validar(comando);
        if (!validacao.Sucesso)
        {
            return Resultado.Falha<UsuarioRegistrado>(validacao.Erros);
        }

        var (nome, email, documento) = validacao.Valor;

        if (await repositorio.ExisteEmailAsync(email.Valor, cancellationToken))
        {
            return Resultado.Falha<UsuarioRegistrado>(ErrosUsuario.EmailJaCadastrado);
        }

        if (await repositorio.ExisteDocumentoAsync(documento.Valor, cancellationToken))
        {
            return Resultado.Falha<UsuarioRegistrado>(ErrosUsuario.DocumentoJaCadastrado);
        }

        var saldo = documento.TipoConta == TipoConta.PessoaFisica
            ? saldoInicial.Value.PessoaFisica
            : saldoInicial.Value.PessoaJuridica;

        // Só depois das validações e das verificações de duplicidade, para não gastar números à toa.
        var numeroConta = await geradorNumeroConta.ProximoAsync(cancellationToken);
        var conta = ContaBancaria.Nova(Banco.OneBank, ContaBancaria.AgenciaPadrao, numeroConta, TipoContaBancaria.ContaCorrente);

        var usuario = Usuario.Criar(
            nome,
            email,
            documento,
            hasherSenha.GerarHash(comando.Senha!),
            conta,
            saldo,
            relogio.GetUtcNow());

        // A verificação prévia não cobre cadastros simultâneos; a base é a garantia final (FR-008).
        switch (await repositorio.AdicionarAsync(usuario, cancellationToken))
        {
            case ResultadoPersistencia.EmailDuplicado:
                return Resultado.Falha<UsuarioRegistrado>(ErrosUsuario.EmailJaCadastrado);
            case ResultadoPersistencia.DocumentoDuplicado:
                return Resultado.Falha<UsuarioRegistrado>(ErrosUsuario.DocumentoJaCadastrado);
        }

        metricas.RegistrarCadastro(usuario.TipoConta);
        LogUsuarioRegistrado(logger, usuario.Id.Valor, usuario.TipoConta);

        return Resultado.Ok(new UsuarioRegistrado(
            usuario.Id,
            usuario.Carteira.Id,
            usuario.NomeCompleto,
            usuario.Email,
            usuario.Documento,
            usuario.TipoConta,
            Dinheiro.NormalizarEscala(usuario.Carteira.Saldo),
            usuario.Carteira.Moeda));
    }

    /// <summary>Acumula os erros de todos os campos em uma única resposta (FR-011).</summary>
    private static Resultado<(string Nome, Email Email, Documento Documento)> Validar(RegistrarUsuarioComando comando)
    {
        var erros = new List<Erro>();

        var nome = Usuario.ValidarNome(comando.NomeCompleto);
        erros.AddRange(nome.Erros);

        var email = Email.Criar(comando.Email);
        erros.AddRange(email.Erros);

        var tipoConta = TiposConta.Converter(comando.TipoConta);
        erros.AddRange(tipoConta.Erros);

        Resultado<Documento>? documento = null;
        if (tipoConta.Sucesso)
        {
            documento = Documento.Criar(comando.Documento, tipoConta.Valor);
            erros.AddRange(documento.Erros);
        }
        else
        {
            erros.AddRange(Documento.ValidarSemTipo(comando.Documento).Erros);
        }

        erros.AddRange(PoliticaSenha.Validar(comando.Senha));

        return erros.Count > 0
            ? Resultado.Falha<(string, Email, Documento)>(erros)
            : Resultado.Ok((nome.Valor, email.Valor, documento!.Valor));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário {UsuarioId} cadastrado com conta {TipoConta}")]
    private static partial void LogUsuarioRegistrado(ILogger logger, Guid usuarioId, TipoConta tipoConta);
}
