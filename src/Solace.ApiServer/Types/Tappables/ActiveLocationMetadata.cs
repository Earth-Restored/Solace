using Solace.ApiServer.Types.Common;

namespace Solace.ApiServer.Types.Tappables;

internal sealed record ActiveLocationMetadata(
    Guid RewardId,
    Rarity Rarity
);
