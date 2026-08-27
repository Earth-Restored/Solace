namespace Solace.Db.Migrator.Old.Earth.Models.Global;

public sealed class Tile : IEntityWithId<ulong>
{
    public ulong Id { get; set; }

    public required string ObjectStoreId { get; set; }
}