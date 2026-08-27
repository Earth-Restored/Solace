using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Solace.Common.Constants;

public static partial class AccountConstants
{
    // Str - for some reason string interpolation with string constants works, but it does not work with int, so need to have string variants for usage in attributes
    public const int UsernameLengthMin = 3;
    public const string UsernameLengthMinStr = "3";
    public const int UsernameLengthMax = 16; // keep in sync with Solace.ApiServer.Controllers.Live.LoginController.GenerateUserId()
    public const string UsernameLengthMaxStr = "16"; // keep in sync with Solace.ApiServer.Controllers.Live.LoginController.GenerateUserId()

    public const int PasswordLengthMin = 4;
    public const string PasswordLengthMinStr = "4";
    public const int PasswordLengthMax = 64;
    public const string PasswordLengthMaxStr = "64";

    public const int NameLengthMin = 2;
    public const int NameLengthMax = 100;

    public const string UsernameAllowedCharacters = "lowercase letters, numbers, underscore and colon";

    [GeneratedRegex("^[a-z0-9_:]+$", RegexOptions.CultureInvariant)]
    public static partial Regex GetUsernameRegex();

    public static byte[] HashPassword(string password, byte[] salt)
    {
        Debug.Assert(password.Length <= 128);

        byte[] passwordUTF8 = Encoding.UTF8.GetBytes(password);

        return Org.BouncyCastle.Crypto.Generators.SCrypt.Generate(passwordUTF8, salt, 16384, 8, 1, 64);
    }
}