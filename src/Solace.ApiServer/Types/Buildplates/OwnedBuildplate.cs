namespace Solace.ApiServer.Types.Buildplates;

internal sealed record OwnedBuildplate(
    string Id,
    string TemplateId,
    Dimension Dimension,
    Offset Offset,
    int BlocksPerMeter,
    OwnedBuildplateType Type,
    SurfaceOrientation SurfaceOrientation,
    string Model,
    int Order,
    bool Locked,
    int RequiredLevel,
    bool IsModified,
    string LastUpdated,
    int NumberOfBlocks,
    string ETag
);
