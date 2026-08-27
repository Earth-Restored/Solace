namespace Solace.Db.Migrator.Old.Earth.Models.Global;

public sealed class TemplateBuildplateEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public required string Name { get; set; }

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int Scale { get; set; } // blocks per meter

    public required bool Night { get; set; }

    public required string ServerDataObjectId { get; set; }

    public required string PreviewObjectId { get; set; }
}