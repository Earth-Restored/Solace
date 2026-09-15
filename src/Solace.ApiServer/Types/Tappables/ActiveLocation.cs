using Solace.ApiServer.Types.Common;

namespace Solace.ApiServer.Types.Tappables;

internal sealed record ActiveLocation(
    Guid Id,
    string TileId,
    Coordinate Coordinate,
    string SpawnTime,
    string ExpirationTime,
    ActiveLocationType Type,
    string Icon,
    ActiveLocationMetadata Metadata,
    ActiveLocationTappableMetadata? TappableMetadata,
    ActiveLocationEncounterMetadata? EncounterMetadata
);
