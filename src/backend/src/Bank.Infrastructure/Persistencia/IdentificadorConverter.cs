using Bank.Domain.Comum;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bank.Infrastructure.Persistencia;

/// <summary>
/// Grava qualquer identificador tipado do Domain como <c>uuid</c> (issue #43). Registrado por
/// convenção em <see cref="BankDbContext"/>; o schema não muda. Não atua em parâmetros de SQL puro.
/// </summary>
internal sealed class IdentificadorConverter<TId>() : ValueConverter<TId, Guid>(id => id.Valor, valor => Criar(valor))
    where TId : struct, IIdentificador<TId>
{
    // Árvores de expressão não chamam membros estáticos abstratos de interface (CS8927).
    private static TId Criar(Guid valor) => TId.De(valor);
}
