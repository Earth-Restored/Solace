using Solace.Common.Utils;

namespace Solace.DB.Models.Global;

public sealed class Tile : IEntityWithId<long>
{
    public long Id { get; set; }

    public required Guid ObjectStoreId { get; set; }
}