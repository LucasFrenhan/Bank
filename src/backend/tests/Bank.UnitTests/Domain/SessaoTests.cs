using Bank.Domain.Sessoes;
using Bank.Domain.Usuarios;
using Bank.UnitTests.Application;

namespace Bank.UnitTests.Domain;

/// <summary>Regras do agregado <see cref="Sessao"/> (specs/016-sessao-refresh-token, data-model; US1, US3, US4).</summary>
public sealed class SessaoTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly LimitesSessao Limites = new(TimeSpan.FromMinutes(15), TimeSpan.FromHours(8), TimeSpan.FromSeconds(30));
    private static readonly HashCredencial H1 = GeradorFalso.Hash("cred-1");
    private static readonly HashCredencial H2 = GeradorFalso.Hash("cred-2");
    private static readonly HashCredencial H3 = GeradorFalso.Hash("cred-3");
    private static readonly HashCredencial H4 = GeradorFalso.Hash("cred-4");

    private static Sessao Nova() => Sessao.Iniciar(UsuarioId.Novo(Inicio), H1, Inicio, Limites);

    // ---------- Iniciar e prazos (fundação) ----------

    [Fact]
    public void Iniciar_DefineLimitesEUmaCredencialAtiva()
    {
        var sessao = Nova();

        Assert.Equal(Inicio, sessao.IniciadaEm);
        Assert.Equal(Inicio, sessao.UltimaRenovacaoEm);
        Assert.Equal(Inicio.AddHours(8), sessao.ExpiraEm);
        var credencial = Assert.Single(sessao.Credenciais);
        Assert.True(credencial.Ativa);
        Assert.Equal(H1, credencial.Hash);
        Assert.Equal(Inicio, credencial.EmitidaEm);
        Assert.Equal(Inicio.AddMinutes(15), credencial.ExpiraEm);
        Assert.Equal(sessao.Id, credencial.SessaoId);
        Assert.False(sessao.Revogada);
    }

    [Fact]
    public void Iniciar_InatividadeIgualAoAbsoluto_CredencialVenceNoLimiteAbsoluto()
    {
        var limites = new LimitesSessao(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromSeconds(30));

        var sessao = Sessao.Iniciar(UsuarioId.Novo(Inicio), H1, Inicio, limites);

        Assert.Equal(sessao.ExpiraEm, sessao.CredencialAtiva.ExpiraEm);
    }

    [Fact]
    public void Prazos_EmSegundosRestantes_NuncaNegativos()
    {
        var sessao = Nova();

        Assert.Equal(900, sessao.PrazoInatividade(Inicio, Limites));
        Assert.Equal(28800, sessao.PrazoAbsoluto(Inicio));
        Assert.Equal(899, sessao.PrazoInatividade(Inicio.AddMilliseconds(500), Limites));
        Assert.Equal(0, sessao.PrazoInatividade(Inicio.AddHours(1), Limites));
        Assert.Equal(0, sessao.PrazoAbsoluto(Inicio.AddHours(9)));
    }

    [Fact]
    public void Revogar_Idempotente_PrimeiroMotivoVale()
    {
        var sessao = Nova();

        Assert.True(sessao.Revogar(MotivoRevogacao.Logout, Inicio.AddMinutes(1)));
        Assert.False(sessao.Revogar(MotivoRevogacao.Reuso, Inicio.AddMinutes(2)));

        Assert.Equal(MotivoRevogacao.Logout, sessao.MotivoRevogacao);
        Assert.Equal(Inicio.AddMinutes(1), sessao.RevogadaEm);
    }

    // ---------- Renovar (US1) ----------

    [Fact]
    public void Renovar_CredencialAtiva_Rotaciona()
    {
        var sessao = Nova();
        var agora = Inicio.AddMinutes(5);

        var resultado = sessao.Renovar(H1, H2, agora, Limites);

        var nova = Assert.IsType<ResultadoRenovacao.Renovada>(resultado).Nova;
        var antiga = sessao.Credenciais.Single(c => c.Hash == H1);
        Assert.Equal(agora, antiga.RotacionadaEm);
        Assert.Equal(nova.Id, antiga.SubstitutaId);
        Assert.Same(nova, sessao.CredencialAtiva);
        Assert.Equal(H2, nova.Hash);
        Assert.Equal(agora, sessao.UltimaRenovacaoEm);
        Assert.Equal(agora.AddMinutes(15), nova.ExpiraEm);
        Assert.Single(sessao.Credenciais, c => c.Ativa);
    }

    [Fact]
    public void Renovar_SessaoRevogada_Recusa()
    {
        var sessao = Nova();
        sessao.Revogar(MotivoRevogacao.Logout, Inicio);

        var resultado = sessao.Renovar(H1, H2, Inicio.AddMinutes(1), Limites);

        Assert.Equal(MotivoRecusa.Revogada, Assert.IsType<ResultadoRenovacao.Recusada>(resultado).Motivo);
        Assert.Single(sessao.Credenciais);
    }

    [Fact]
    public void Renovar_CredencialDeOutraSessao_ErroDeProgramacao()
    {
        var sessao = Nova();

        Assert.Throws<InvalidOperationException>(() => sessao.Renovar(H3, H2, Inicio.AddMinutes(1), Limites));
    }

    // ---------- Tolerância e reuso (US3) ----------

    [Fact]
    public void Renovar_CredencialTrocadaHa30sExatos_AceitaERotacionaAAtiva()
    {
        var sessao = Nova();
        var rotacao = Inicio.AddMinutes(5);
        sessao.Renovar(H1, H2, rotacao, Limites);

        var resultado = sessao.Renovar(H1, H3, rotacao.AddSeconds(30), Limites);

        var nova = Assert.IsType<ResultadoRenovacao.Renovada>(resultado).Nova;
        Assert.Equal(H3, nova.Hash);
        Assert.Equal(nova.Id, sessao.Credenciais.Single(c => c.Hash == H2).SubstitutaId);
        Assert.Single(sessao.Credenciais, c => c.Ativa);
        Assert.False(sessao.Revogada);
    }

    [Fact]
    public void Renovar_CredencialTrocadaHaMaisQueATolerancia_ReusoRevogaASessao()
    {
        var sessao = Nova();
        var rotacao = Inicio.AddMinutes(5);
        sessao.Renovar(H1, H2, rotacao, Limites);
        var agora = rotacao.AddSeconds(30).AddTicks(1);

        var resultado = sessao.Renovar(H1, H3, agora, Limites);

        Assert.IsType<ResultadoRenovacao.ReusoDetectado>(resultado);
        Assert.Equal(MotivoRevogacao.Reuso, sessao.MotivoRevogacao);
        Assert.Equal(agora, sessao.RevogadaEm);
        Assert.Equal(H2, sessao.CredencialAtiva.Hash);

        // A credencial mais nova também deixa de valer.
        Assert.IsType<ResultadoRenovacao.Recusada>(sessao.Renovar(H2, H4, agora.AddSeconds(1), Limites));
    }

    [Fact]
    public void Renovar_CredencialDeDuasRotacoesAtras_Reuso()
    {
        var sessao = Nova();
        sessao.Renovar(H1, H2, Inicio.AddMinutes(1), Limites);
        sessao.Renovar(H2, H3, Inicio.AddMinutes(2), Limites);

        var resultado = sessao.Renovar(H1, H4, Inicio.AddMinutes(3), Limites);

        Assert.IsType<ResultadoRenovacao.ReusoDetectado>(resultado);
    }

    // ---------- Inatividade e limite absoluto (US4) ----------

    [Fact]
    public void Renovar_NoLimiteDeInatividade_Recusa()
    {
        var sessao = Nova();

        var resultado = sessao.Renovar(H1, H2, Inicio.AddMinutes(15), Limites);

        Assert.Equal(MotivoRecusa.Inativa, Assert.IsType<ResultadoRenovacao.Recusada>(resultado).Motivo);
        Assert.False(sessao.Revogada);
    }

    [Fact]
    public void Renovar_NoLimiteAbsoluto_RecusaMesmoComUsoContinuo()
    {
        var sessao = Nova();
        var agora = Inicio;
        for (var i = 1; agora.AddMinutes(10) < sessao.ExpiraEm; i++)
        {
            agora = agora.AddMinutes(10);
            Assert.IsType<ResultadoRenovacao.Renovada>(
                sessao.Renovar(sessao.CredencialAtiva.Hash, GeradorFalso.Hash("c" + i), agora, Limites));
        }

        var resultado = sessao.Renovar(sessao.CredencialAtiva.Hash, H4, sessao.ExpiraEm, Limites);

        Assert.Equal(MotivoRecusa.LimiteAbsoluto, Assert.IsType<ResultadoRenovacao.Recusada>(resultado).Motivo);
    }

    [Fact]
    public void Renovar_PertoDoLimiteAbsoluto_CredencialEPrazosEncolhem()
    {
        var limites = new LimitesSessao(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(30));
        var sessao = Sessao.Iniciar(UsuarioId.Novo(Inicio), H1, Inicio, limites);
        sessao.Renovar(H1, H2, Inicio.AddMinutes(10), limites);
        var agora = Inicio.AddMinutes(17);

        var nova = Assert.IsType<ResultadoRenovacao.Renovada>(sessao.Renovar(H2, H3, agora, limites)).Nova;

        Assert.Equal(sessao.ExpiraEm, nova.ExpiraEm);
        Assert.Equal(180, sessao.PrazoInatividade(agora, limites));
        Assert.Equal(180, sessao.PrazoAbsoluto(agora));
    }

    // ---------- Limites ----------

    [Theory]
    [InlineData(0, 8, 30)]
    [InlineData(900, 0, 30)]
    [InlineData(900, 8, 0)]
    [InlineData(36000, 8, 30)]
    [InlineData(900, 8, 900)]
    public void Limites_Invalidos_Lancam(int inatividadeSegundos, int absolutoHoras, int toleranciaSegundos)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LimitesSessao(
            TimeSpan.FromSeconds(inatividadeSegundos), TimeSpan.FromHours(absolutoHoras), TimeSpan.FromSeconds(toleranciaSegundos)));
    }
}
