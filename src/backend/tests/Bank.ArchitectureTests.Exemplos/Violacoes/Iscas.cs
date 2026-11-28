// Tipos-isca com nomes do núcleo antigo, do Shared Kernel e dos hosts, só para provar as regras de fronteira
// (specs/017-modulos-fundacao, research R5). Nunca fazem parte da arquitetura real.

namespace Bank.Domain.Exemplo
{
    internal static class TipoDoNucleoAntigo
    {
        public static string Valor() => "núcleo antigo";
    }
}

namespace Bank.SharedKernel.Infrastructure.Exemplo
{
    internal static class ConversorDeExemplo
    {
        public static string Converter() => "conversor";
    }
}
