namespace Solace.ApiServer.Types.Common;

// TODO: determine format
internal sealed record Rewards(
    int? Rubies,
    int? ExperiencePoints,
    int? Level,
    Rewards.Item[] Inventory,
    Guid[] Buildplates,
    Rewards.Challenge[] Challenges,
    string[] PersonaItems,
    Rewards.UtilityBlock[] UtilityBlocks
)
{
    internal sealed record Item(
        Guid Id,
        int Amount
    );

    internal sealed record Challenge(
        Guid Id
    );

    internal sealed record UtilityBlock();
}
