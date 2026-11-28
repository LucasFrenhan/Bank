// Módulo violador: um tipo por regra de fronteira (contracts/regras-fronteira.md). O nome de cada tipo diz a
// regra que ele viola; DeteccaoRegrasTests espera exatamente essas violações.

using Bank.Domain.Exemplo;
using Bank.Modulos.Alfa.Infrastructure;
using Bank.SharedKernel.Infrastructure.Exemplo;

namespace Bank.Modulos.Gama.Infrastructure
{
    internal static class RecursoDaInfra
    {
        public static string Valor() => "infraestrutura de Gama";
    }

    /// <summary>MOD-02: usa o DbContext de outro módulo.</summary>
    internal static class UsaAlfaDbContext
    {
        public static Guid Contexto(AlfaDbContext contexto) => contexto.ContextId.InstanceId;
    }
}

namespace Bank.Modulos.Gama.Application
{
    /// <summary>MOD-01: usa um tipo de domínio de outro módulo.</summary>
    internal static class UsaDominioDeAlfa
    {
        public static string Nome(Alfa.Domain.Alfa alfa) => alfa.Nome;
    }

    /// <summary>MOD-03: depende do núcleo antigo.</summary>
    internal static class UsaNucleoAntigo
    {
        public static string Valor() => TipoDoNucleoAntigo.Valor();
    }

    /// <summary>Convenção do ponto de registro: tipo público fora de Modulo&lt;Nome&gt; e dos contratos.</summary>
    public static class TipoPublicoIndevido
    {
        public static string Valor() => "público";
    }
}

namespace Bank.Modulos.Gama.Domain
{
    /// <summary>MOD-04: o domínio depende da infraestrutura do próprio módulo.</summary>
    internal static class UsaInfraDoProprioModulo
    {
        public static string Valor() => Infrastructure.RecursoDaInfra.Valor();
    }

    /// <summary>MOD-07: o domínio usa a infraestrutura comum do Shared Kernel.</summary>
    internal static class UsaInfraComum
    {
        public static string Valor() => ConversorDeExemplo.Converter();
    }
}

namespace Bank.Modulos.Gama.Endpoints
{
    /// <summary>MOD-07: o endpoint usa a infraestrutura comum do Shared Kernel.</summary>
    internal static class EndpointUsaInfraComum
    {
        public static string Valor() => ConversorDeExemplo.Converter();
    }
}

namespace Bank.Modulos.Gama.Contratos
{
    /// <summary>MOD-04: o contrato público depende do interno do próprio módulo.</summary>
    internal static class ContratoUsaInterno
    {
        public static string Valor() => Infrastructure.RecursoDaInfra.Valor();
    }
}
