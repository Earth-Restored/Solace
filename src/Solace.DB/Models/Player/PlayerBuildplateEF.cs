namespace Solace.DB.Models.Player;

public sealed class PlayerBuildplateEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public Guid? TemplateId { get; set; }

    public required string Name { get; set; }

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int BlocksPerMeter { get; set; }

    public required bool Night { get; set; }

    public required DateTimeOffset LastModified { get; set; }

    public required Guid ServerDataObjectId { get; set; }

    public required Guid PreviewObjectId { get; set; }
}