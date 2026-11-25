using System.Collections.Frozen;
using Solace.WebPortal.Common.Features.Store;

namespace Solace.WebPortal.Common.Features.Seasons;

public static class SeasonLanguageConstants
{
    public static readonly FrozenDictionary<string, string> LanguageNames = Constants.LanguageNames
        .ToFrozenDictionary(
            item => item.Key.Replace('-', '_'),
            item => item.Value,
            StringComparer.Ordinal);
}
