using Bank.Domain.Carteiras;
using Bank.Domain.Cartoes;
using Bank.Domain.Cobrancas;
using Bank.Domain.Transferencias;
using Bank.Domain.Usuarios;

namespace Bank.UnitTests;

/// <summary>Cartões, cobranças e pagamentos para os testes da spec 014.</summary>
internal static class CartoesDeTeste
{
    public static readonly DateTimeOffset Agora = new(2026, 11, 24, 15, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Hoje = new(2026, 11, 24);
    public static readonly NumeroCartao Numero = NumeroCartao.DePrefixo("400000123456789");

    public static Cartao Cartao(decimal limite = 5000m, UsuarioId? usuario = null) => Bank.Domain.Cartoes.Cartao.Emitir(
        CartaoId.Novo(Agora), usuario ?? UsuarioId.Novo(Agora), Bandeira.Visa, "Lucas Silva", ValidadeCartao.DaEmissao(Hoje), 0,
        Numero.UltimosDigitos, "impressao", limite, Agora);

    public static Cobranca Cobranca(decimal valor = 299.90m, string descricao = "Café Especial", UsuarioId? lojista = null) =>
        Bank.Domain.Cobrancas.Cobranca.Emitir(lojista ?? UsuarioId.Novo(Agora), CarteiraId.Novo(Agora), ValorTransferencia.Criar(valor).Valor, descricao, CodigoCobranca.Novo(), Agora);

    public static Fatura Fatura(Cartao cartao, DateOnly? dataCompra = null) =>
        Bank.Domain.Cartoes.Fatura.Abrir(cartao.Id, CompetenciaFatura.De(dataCompra ?? Hoje), Agora);

    public static ExecucaoPagamento Pagar(Cobranca cobranca, Cartao cartao, Fatura fatura, DateOnly? dataCompra = null) =>
        PagamentoCobranca.Executar(cobranca, cartao, fatura, "Cafeteria Ltda", dataCompra ?? Hoje, Guid.NewGuid(), Agora).Valor;
}
