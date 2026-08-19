using Solace.Common.Utils;
using Solace.Db.Earth;

namespace Solace.ApiServer.Utils;

internal sealed class EarthApiResponse
{
    public object? Result { get; }
    public Dictionary<string, int?>? Updates { get; } = [with(StringComparer.Ordinal)];

    public EarthApiResponse(object results)
    {
        Result = results;
    }

    public EarthApiResponse(object? results, UpdatesResponse? updates)
    {
        Result = results;
        if (updates is null)
        {
            Updates = null;
        }
        else
        {
            Updates.AddRange(updates.Map);
        }
    }

    internal sealed class UpdatesResponse
    {
        public Dictionary<string, int?> Map = [with(StringComparer.Ordinal)];

        public UpdatesResponse(ResultsEF results)
            : this(results.Profile, results.Inventory, results.Crafting, results.Smelting, results.Boosts, results.Buildplates, results.Journal, results.Challenges, results.Tokens)
        {
        }

        public UpdatesResponse(int? profile = null, int? inventory = null, int? crafting = null, int? smelting = null, int? boosts = null, int? buildplates = null, int? journal = null, int? challenges = null, int? tokens = null)
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
}