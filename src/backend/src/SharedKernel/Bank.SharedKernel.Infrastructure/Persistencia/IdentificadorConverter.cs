using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bank.SharedKernel.Infrastructure.Persistencia;

/// <summary>
/// Grava qualquer identificador tipado como <c>uuid</c> (issue #43). Registrado por convenção no
/// <c>DbContext</c> de cada Infrastructure (núcleo antigo e módulos; specs/017-modulos-fundacao, R1); o
/// schema não muda. Não atua em parâmetros de SQL puro.
/// </summary>
public sealed class IdentificadorConverter<TId>() : ValueConverter<TId, Guid>(id => id.Valor, valor => Criar(valor))
    where TId : struct, IIdentificador<TId>
{
    // Árvores de expressão não chamam membros estáticos abstratos de interface (CS8927).
    private static TId Criar(Guid valor) => TId.De(valor);
}
