namespace Solace.Db.Migrator.Old.Earth.Models.Global;

public sealed class Secret : IEntityWithId<string>
{
    public required string Id { get; set; }

    public required string Value { get; set; }
}