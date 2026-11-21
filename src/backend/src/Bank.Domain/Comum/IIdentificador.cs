namespace Bank.Domain.Comum;

/// <summary>
/// Identificador tipado sobre um <see cref="Guid"/> (issue #43): impede trocar, por exemplo, um
/// <c>UsuarioId</c> por um <c>CarteiraId</c>. Sem dependência de infraestrutura; os conversores
/// (EF Core) ficam na Infrastructure e as fronteiras (HTTP, SQL puro, evento) seguem em <see cref="Guid"/>.
/// </summary>
public interface IIdentificador<TSelf>
    where TSelf : struct, IIdentificador<TSelf>
{
    Guid Valor { get; }

    /// <summary>Embrulha um valor já existente (base, token, rota).</summary>
    static abstract TSelf De(Guid valor);
}
