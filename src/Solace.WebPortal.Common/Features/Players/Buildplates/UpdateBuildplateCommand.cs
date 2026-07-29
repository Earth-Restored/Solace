namespace Solace.WebPortal.Common.Features.Players.Buildplates;

public sealed record UpdateBuildplateCommand(
    string? Name,
    int? BlocksPerMeter
);
