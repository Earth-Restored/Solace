using System.Collections.Frozen;

namespace Solace.WebPortal.Common.Features.Store;

public static class Constants
{
    public static readonly FrozenDictionary<string, string> LanguageNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bg-BG"] = "Bulgarian (Bulgaria)",
        ["cs-CZ"] = "Czech (Czechia)",
        ["da-DK"] = "Danish (Denmark)",
        ["de-DE"] = "German (Germany)",
        ["el-GR"] = "Greek (Greece)",
        ["en-GB"] = "English (United Kingdom)",
        ["en-US"] = "English (United States)",
        ["es-ES"] = "Spanish (Spain)",
        ["es-MX"] = "Spanish (Mexico)",
        ["fi-FI"] = "Finnish (Finland)",
        ["fr-CA"] = "French (Canada)",
        ["fr-FR"] = "French (France)",
        ["hu-HU"] = "Hungarian (Hungary)",
        ["id-ID"] = "Indonesian (Indonesia)",
        ["it-IT"] = "Italian (Italy)",
        ["ja-JP"] = "Japanese (Japan)",
        ["ko-KR"] = "Korean (South Korea)",
        ["nb-NO"] = "Norwegian Bokmål (Norway)",
        ["nl-NL"] = "Dutch (Netherlands)",
        ["pl-PL"] = "Polish (Poland)",
        ["pt-BR"] = "Portuguese (Brazil)",
        ["pt-PT"] = "Portuguese (Portugal)",
        ["ru-RU"] = "Russian (Russia)",
        ["sk-SK"] = "Slovak (Slovakia)",
        ["sv-SE"] = "Swedish (Sweden)",
        ["tr-TR"] = "Turkish (Türkiye)",
        ["uk-UA"] = "Ukrainian (Ukraine)",
        ["zh-CN"] = "Chinese (Simplified, China)",
        ["zh-TW"] = "Chinese (Traditional, Taiwan)"
    }.ToFrozenDictionary();
}
