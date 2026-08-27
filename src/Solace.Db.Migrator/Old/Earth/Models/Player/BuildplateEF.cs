namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class BuildplateEF : IEntityWithId<Guid>, IVersionedEntity
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid? TemplateId { get; set; }

    public required string Name { get; set; }

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int Scale { get; set; }

    public required bool Night { get; set; }

    public required long LastModified { get; set; }

    public required string ServerDataObjectId { get; set; }

    public required string PreviewObjectId { get; set; }
}