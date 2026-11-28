// Violações com origem fora dos módulos: Shared Kernel e host (contracts/regras-fronteira.md).

using Bank.Modulos.Alfa.Contratos;

namespace Bank.SharedKernel.Exemplo
{
    /// <summary>MOD-05: a parte pura do Shared Kernel depende de um módulo.</summary>
    internal static class KernelUsaModulo
    {
        public static string Nome(AlfaResumo resumo) => resumo.Nome;
    }
}

namespace Bank.SharedKernel.Infrastructure.Exemplo
{
    /// <summary>MOD-05: a infraestrutura comum do Shared Kernel depende de um módulo.</summary>
    internal static class InfraComumUsaModulo
    {
        public static string Nome(AlfaResumo resumo) => resumo.Nome;
    }
}

namespace Bank.Api.Exemplo
{
    /// <summary>MOD-06: um host usa o interno de um módulo.</summary>
    internal static class HostUsaInterno
    {
        public static string Nome(Modulos.Alfa.Domain.Alfa alfa) => alfa.Nome;
    }
}
