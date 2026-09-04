using System.Net;
using System.Text.RegularExpressions;

namespace Solace.IntegrationTests;

public sealed partial class LoginTests
{
    [Test]
    public async Task Login_Oidc(CancellationToken cancellationToken)
    {
        var cookieContainer = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false,
        };
        using var client = new HttpClient(handler);

        var authServerUrl = _fixture.AuthServerClient.BaseAddress!;

        var loginUri = new Uri(authServerUrl, "/login");
        using var response1 = await client.GetAsync(loginUri, cancellationToken);
        await Assert.That((int)response1.StatusCode).IsGreaterThanOrEqualTo(300);
        await Assert.That((int)response1.StatusCode).IsLessThan(400);
        await Assert.That(response1.Headers.Location).IsNotNull();

        var authorizeUri = response1.Headers.Location!;
        if (!authorizeUri.IsAbsoluteUri)
        {
            authorizeUri = new Uri(authServerUrl, authorizeUri);
        }

        using var response2 = await client.GetAsync(authorizeUri, cancellationToken);
        await Assert.That((int)response2.StatusCode).IsGreaterThanOrEqualTo(300);
        await Assert.That((int)response2.StatusCode).IsLessThan(400);
        await Assert.That(response2.Headers.Location).IsNotNull();

        var loginPageUri = response2.Headers.Location!;
        if (!loginPageUri.IsAbsoluteUri)
        {
            loginPageUri = new Uri(_fixture.WebPortalClient.BaseAddress!, loginPageUri);
        }

        using var response3 = await client.GetAsync(loginPageUri, cancellationToken);
        await Assert.That(response3.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginHtml = await response3.Content.ReadAsStringAsync(cancellationToken);
        var loginToken = ExtractInputValue(loginHtml, "__RequestVerificationToken");
        await Assert.That(loginToken).IsNotEmpty();

        var loginPostContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "login"),
            new KeyValuePair<string, string>("Input.Email", LoginTestsFixture.AccountEmail),
            new KeyValuePair<string, string>("Input.Password", LoginTestsFixture.AccountPassword),
            new KeyValuePair<string, string>("Input.RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", loginToken),
        ]);

        using var response4 = await client.PostAsync(loginPageUri, loginPostContent, cancellationToken);
        await Assert.That((int)response4.StatusCode).IsGreaterThanOrEqualTo(300);
        await Assert.That((int)response4.StatusCode).IsLessThan(400);
        await Assert.That(response4.Headers.Location).IsNotNull();

        var authorizeUri2 = response4.Headers.Location!;
        if (!authorizeUri2.IsAbsoluteUri)
        {
            authorizeUri2 = new Uri(_fixture.WebPortalClient.BaseAddress!, authorizeUri2);
        }

        using var response5 = await client.GetAsync(authorizeUri2, cancellationToken);

        Uri callbackUri;
        HttpResponseMessage response6;
        if (response5.Headers.Location is not null)
        {
            callbackUri = response5.Headers.Location;
            if (!callbackUri.IsAbsoluteUri)
            {
                callbackUri = new Uri(authServerUrl, callbackUri);
            }

            response6 = await client.GetAsync(callbackUri, cancellationToken);
        }
        else
        {
            var authorizeHtml = await response5.Content.ReadAsStringAsync(cancellationToken);
            var callbackAction = ExtractFormAction(authorizeHtml);
            await Assert.That(callbackAction).IsNotEmpty();

            callbackUri = new Uri(new Uri(authorizeUri2.GetLeftPart(UriPartial.Authority)), callbackAction);
            response6 = await client.PostAsync(callbackUri, new FormUrlEncodedContent(ExtractFormInputs(authorizeHtml)), cancellationToken);
        }

        using (response6)
        {
            await Assert.That((int)response6.StatusCode).IsGreaterThanOrEqualTo(300);
            await Assert.That((int)response6.StatusCode).IsLessThan(400);
            await Assert.That(response6.Headers.Location).IsNotNull();
        }

        var inlineConnectUri = response6.Headers.Location!;
        if (!inlineConnectUri.IsAbsoluteUri)
        {
            inlineConnectUri = new Uri(authServerUrl, inlineConnectUri);
        }

        using var response7 = await client.GetAsync(inlineConnectUri, cancellationToken);
        await Assert.That(response7.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var inlineConnectHtml = await response7.Content.ReadAsStringAsync(cancellationToken);
        await Assert.That(inlineConnectHtml).Contains("Select Profile");

        var createProfileToken = ExtractInputValue(inlineConnectHtml, "__RequestVerificationToken");
        await Assert.That(createProfileToken).IsNotEmpty();

        // Create profile
        const string newUsername = "oidc_profile";
        var createProfileContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "create_profile_form"),
            new KeyValuePair<string, string>("__RequestVerificationToken", createProfileToken),
            new KeyValuePair<string, string>("NewUsername", newUsername),
        ]);

        using var createResponse = await client.PostAsync(inlineConnectUri, createProfileContent, cancellationToken);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var createHtml = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        await Assert.That(createHtml).Contains(newUsername);
        await ValidateProfileResponse(createHtml);

        // Select existing profile
        using var refreshResponse = await client.GetAsync(inlineConnectUri, cancellationToken);
        await Assert.That(refreshResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var refreshHtml = await refreshResponse.Content.ReadAsStringAsync(cancellationToken);
        await Assert.That(refreshHtml).Contains("Select Profile");
        await Assert.That(refreshHtml).Contains(newUsername);

        var selectHandlerMatch = Regex.Match(
            refreshHtml,
            @"select_profile_[0-9a-fA-F\-]+",
            RegexOptions.None,
            matchTimeout: TimeSpan.FromSeconds(1));
        await Assert.That(selectHandlerMatch.Success).IsTrue();

        var selectHandler = selectHandlerMatch.Value;
        var selectToken = ExtractInputValue(refreshHtml, "__RequestVerificationToken");
        await Assert.That(selectToken).IsNotEmpty();

        var selectProfileContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", selectHandler),
            new KeyValuePair<string, string>("__RequestVerificationToken", selectToken),
        ]);

        using var selectResponse = await client.PostAsync(inlineConnectUri, selectProfileContent, cancellationToken);
        await Assert.That(selectResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var selectHtml = await selectResponse.Content.ReadAsStringAsync(cancellationToken);
        await Assert.That(selectHtml).Contains(newUsername);
        await ValidateProfileResponse(selectHtml);

        static async Task ValidateProfileResponse(string html)
        {
            await Assert.That(html).Contains("Logging in...");
            await Assert.That(html).Contains("external.Property('DAToken',");
            await Assert.That(html).Contains("external.Property('DAStartTime',");
            await Assert.That(html).Contains("external.Property('DAExpires',");
            await Assert.That(html).Contains("external.Property('DASessionKey',");
            await Assert.That(html).Contains("external.Property('FirstName',");
            await Assert.That(html).Contains("external.Property('LastName',");
            await Assert.That(html).Contains("external.Property('SigninName',");
            await Assert.That(html).Contains("external.Property('Username',");
            await Assert.That(html).Contains("external.Property('CID',");
            await Assert.That(html).Contains("external.Property('PUID',");
        }
    }
}
