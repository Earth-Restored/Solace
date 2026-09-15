using Solace.Db.Earth;

namespace Solace.ApiServer.Utils;

internal sealed class EarthUpdatesResponse
{
    public Dictionary<string, int?> Map = [with(StringComparer.Ordinal)];

    public EarthUpdatesResponse(ResultsEF results)
        : this(results.Profile, results.Inventory, results.Crafting, results.Smelting, results.Boosts, results.Buildplates, results.Journal, results.Challenges, results.Tokens)
    {
    }

    public EarthUpdatesResponse(int? profile = null, int? inventory = null, int? crafting = null, int? smelting = null, int? boosts = null, int? buildplates = null, int? journal = null, int? challenges = null, int? tokens = null)
    {
        Set(profile, "characterProfile");
        Set(inventory, "inventory");
        Set(crafting, "crafting");
        Set(smelting, "smelting");
        Set(boosts, "boosts");
        Set(buildplates, "buildplates");
        Set(journal, "playerJournal");
        Set(challenges, "challenges");
        Set(tokens, "tokens");
    }

    private void Set(int? version, string @as)
    {
        if (version is not null)
        {
            Map[@as] = version;
        }
    }
}
