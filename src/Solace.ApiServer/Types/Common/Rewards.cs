namespace Solace.ApiServer.Types.Common;

// TODO: determine format
internal sealed record Rewards(
    int? Rubies,
    int? ExperiencePoints,
    int? Level,
    RewardsItem[] Inventory,
    Guid[] Buildplates,
    RewardsChallenge[] Challenges,
    string[] PersonaItems,
    RewardsUtilityBlock[] UtilityBlocks
);
