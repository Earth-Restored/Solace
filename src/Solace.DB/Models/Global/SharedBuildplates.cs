namespace Solace.DB.Models.Global;

public sealed class SharedBuildplateEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account Account { get; set; } = null!;

    public required int Size { get; set; }

    public required int Offset { get; set; }

    public required int Scale { get; set; }

    public required bool Night { get; set; }

    public required DateTimeOffset Created { get; set; }

    public required DateTimeOffset BuildplateLastModifed { get; set; }

    public required DateTimeOffset LastViewed { get; set; }

    public required int NumberOfTimesViewed { get; set; }

    public HotbarItem?[] Hotbar { get; set; } = new HotbarItem[7];

    public required Guid ServerDataObjectId { get; set; }

    public sealed record HotbarItem(
        Guid Uuid,
        int Count,
        Guid? InstanceId,
        int Wear
    );
}
