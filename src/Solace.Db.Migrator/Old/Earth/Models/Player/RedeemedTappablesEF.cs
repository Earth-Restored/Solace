namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class RedeemedTappablesEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public Dictionary<Guid, long> Tappables = [];
}