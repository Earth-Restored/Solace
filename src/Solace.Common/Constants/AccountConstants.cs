using System.Text.RegularExpressions;

namespace Solace.Common.Constants;

public static partial class AccountConstants
{
    // Str - for some reason string interpolation with string constants works, but it does not work with int, so need to have string variants for usage in attributes
    public const int UsernameLengthMin = 3;
    public const string UsernameLengthMinStr = "3";
    public const int UsernameLengthMax = 16; // keep in sync with Solace.ApiServer.Controllers.Live.LoginController.GenerateUserId()
    public const string UsernameLengthMaxStr = "16"; // keep in sync with Solace.ApiServer.Controllers.Live.LoginController.GenerateUserId()

    public const string UsernameAllowedCharacters = "lowercase letters, numbers, underscore and colon";

    [GeneratedRegex("^[a-z0-9_:]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    public static partial Regex GetUsernameRegex();
}