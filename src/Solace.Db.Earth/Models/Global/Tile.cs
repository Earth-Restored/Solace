namespace Solace.Db.Earth.Models.Global;

public sealed class Tile : IEntityWithId<long>
{
    public long Id { get; set; }

    public required Guid ObjectStoreId { get; set; }
}