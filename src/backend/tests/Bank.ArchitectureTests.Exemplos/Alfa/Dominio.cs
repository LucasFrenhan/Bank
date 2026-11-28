using Bank.Modulos.Alfa.Contratos;

namespace Bank.Modulos.Alfa.Domain;

internal sealed class Alfa(AlfaId id, string nome)
{
    public AlfaId Id { get; } = id;

    public string Nome { get; } = nome;
}
