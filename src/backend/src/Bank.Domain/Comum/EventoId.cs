namespace Bank.Domain.Comum;

/// <summary>Identificador de um evento de integração (ex.: <c>transferencia.concluida</c>).</summary>
public readonly record struct EventoId(Guid Valor) : IIdentificador<EventoId>
{
    public static EventoId De(Guid valor) => new(valor);

    /// <summary>Novo identificador UUIDv7, ordenado pelo instante de criação.</summary>
    public static EventoId Novo(DateTimeOffset agora) => new(Guid.CreateVersion7(agora));

    public override string ToString() => Valor.ToString();
}
