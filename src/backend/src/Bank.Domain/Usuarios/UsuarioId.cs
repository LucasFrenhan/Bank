using Bank.Domain.Comum;

namespace Bank.Domain.Usuarios;

/// <summary>Identificador de um <see cref="Usuario"/>.</summary>
public readonly record struct UsuarioId(Guid Valor) : IIdentificador<UsuarioId>
{
    public static UsuarioId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static UsuarioId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
