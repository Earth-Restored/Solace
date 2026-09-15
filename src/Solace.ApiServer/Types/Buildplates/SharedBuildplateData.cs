namespace Solace.ApiServer.Types.Buildplates;

internal sealed record SharedBuildplateData(
    Dimension Dimension,
    Offset Offset,
    int BlocksPerMeter,
    SharedBuildplateDataType Type,
    SurfaceOrientation SurfaceOrientation,
    string Model,
    int Order
);
