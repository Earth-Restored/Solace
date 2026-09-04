using System.Text.RegularExpressions;

namespace Solace.IntegrationTests;

[ClassDataSource<LoginTestsFixture>(Shared = SharedType.PerClass)]
public sealed partial class LoginTests
{
    private readonly LoginTestsFixture _fixture;

    public LoginTests(LoginTestsFixture fixture)
    {
        _fixture = fixture;
    }

    private static string ExtractInputValue(string html, string inputName)
    {
        var match = Regex.Match(
            html,
            $@"<input[^>]*name=""{Regex.Escape(inputName)}""[^>]*value=""([^""]*)""",
            RegexOptions.IgnoreCase,
            matchTimeout: TimeSpan.FromSeconds(1));

        if (!match.Success)
        {
            match = Regex.Match(
                html,
                $@"<input[^>]*value=""([^""]*)""[^>]*name=""{Regex.Escape(inputName)}""",
                RegexOptions.IgnoreCase, matchTimeout:
                TimeSpan.FromSeconds(1));
        }

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string ExtractFormAction(string html)
    {
        var match = Regex.Match(
            html,
            "<form[^>]*action=\"(?<action>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            matchTimeout: TimeSpan.FromSeconds(1));

        return match.Success ? match.Groups["action"].Value : string.Empty;
    }

    private static IEnumerable<KeyValuePair<string, string>> ExtractFormInputs(string html)
    {
        foreach (Match match in Regex.Matches(
            html,
            "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            matchTimeout: TimeSpan.FromSeconds(1)))
        {
            yield return new KeyValuePair<string, string>(
                match.Groups["name"].Value,
                match.Groups["value"].Value
            );
        }
    }
}
