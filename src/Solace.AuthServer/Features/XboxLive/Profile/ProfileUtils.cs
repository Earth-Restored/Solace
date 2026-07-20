using System.Text.Json.Serialization;
using Solace.DB.Models;

namespace Solace.AuthServer.Features.XboxLive.Profile;

public static class ProfileUtils
{
    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record ProfileSettingsResponse(
        IEnumerable<ProfileUser> ProfileUsers
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record ProfileUser(
        Guid Id,
        Guid HostId,
        IEnumerable<ProfileSetting> Settings,
        bool IsSponsoredUser
    );

    [JsonNamingPolicy(JsonKnownNamingPolicy.CamelCase)]
    public sealed record ProfileSetting(
        string Id,
        string? Value
    );

    public static IEnumerable<ProfileSetting> GetProfileFields(Account account, IEnumerable<string> fields, HttpRequest request)
    {
        var profile = GetProfile(account, request);

        return fields
            .Where(profile.ContainsKey)
            .Select(field => new ProfileSetting(field, profile[field]));
    }

    private static Dictionary<string, string?> GetProfile(Account account, HttpRequest request)
        => new(StringComparer.Ordinal)
        {
            ["AppDisplayName"] = account.Username,
            ["AppDisplayPicRaw"] = $"{(request.IsHttps ? "https://" : "http://")}{request.Host.Value}/{account.ProfilePictureUrl ?? Account.DefaultPictureUrl}",
            ["GameDisplayName"] = account.Username,
            ["GameDisplayPicRaw"] = $"{(request.IsHttps ? "https://" : "http://")}{request.Host.Value}/{account.ProfilePictureUrl ?? Account.DefaultPictureUrl}",
            ["Gamertag"] = account.Username,
            ["Gamerscore"] = "100",
            ["FirstName"] = account.FirstName ?? account.Username,
            ["LastName"] = account.LastName ?? account.Username,
            ["SpeechAccessibility"] = "",
        };
}
