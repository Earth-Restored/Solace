#pragma warning disable CA1716
namespace Solace.DB.Earth.Models.Global;
#pragma warning restore CA1716

public sealed class EncounterBuildplateEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int Scale { get; set; }

    public required Guid ServerDataObjectId { get; set; }
}
