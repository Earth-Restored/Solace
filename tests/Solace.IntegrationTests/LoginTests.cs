using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Testing.Platform.Services;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Db.Earth;
using Solace.WebPortal.Common.Features.Roles;
using Solace.WebPortal.Data;
using TUnit.Assertions.Conditions;
using TUnit.Core.Interfaces;

namespace Solace.IntegrationTests;

public sealed class LoginTests : IAsyncInitializer, IAsyncDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private const string AccountEmail = "test@solace.com";
    private const string AccountPassword = "aA1234$";

    private DistributedApplication _app = null!;
    private HttpClient _authServerClient = null!;
    private HttpClient _webPortalClient = null!;

    public async Task InitializeAsync()
    {
        _app = await AppHostExtensions.RunAsync(
            ["postgres", "event-bus", "auth-server", "object-store", "web-portal"],
            [
                // use default (localhost + port)
                "Shared:PublicEndpoints:WebPortal=",
                "Shared:PublicEndpoints:Locator=",
                "Shared:PublicEndpoints:AuthServer=",
                "Shared:PublicEndpoints:ApiServer=",
                "Shared:PublicEndpoints:Cdn=",
            ]);

        _authServerClient = _app.CreateHttpClient("auth-server", "http");
        _webPortalClient = _app.CreateHttpClient("web-portal", "http");

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("auth-server")
            .WaitAsync(DefaultTimeout);

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("web-portal")
            .WaitAsync(DefaultTimeout);

        var webPortalConnectionString = await _app.GetConnectionStringAsync("WebPortalDb");
        Debug.Assert(webPortalConnectionString is not null);

        await using var webPortalDb = ApplicationDbContext.CreateFromConnection(webPortalConnectionString);

        var user = new ApplicationUser()
        {
            UserName = AccountEmail,
            NormalizedUserName = AccountEmail.ToUpperOrdinal(),
            Email = AccountEmail,
            NormalizedEmail = AccountEmail.ToUpperOrdinal(),
            EmailConfirmed = true,
        };
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        user.PasswordHash = passwordHasher.HashPassword(user, AccountPassword);

        webPortalDb.Users.Add(user);

        await webPortalDb.SaveChangesAsync();

        var ownerRole = await webPortalDb.Roles
            .AsNoTracking()
            .FirstAsync(role => role.Name == RoleConstants.Owner);

        webPortalDb.UserRoles.Add(new IdentityUserRole<long>()
        {
            UserId = user.Id,
            RoleId = ownerRole.Id,
        });

        await webPortalDb.SaveChangesAsync();
    }

    [Test]
    public async Task InlineConnect(CancellationToken cancellationToken)
    {
        using var respones = await _authServerClient.GetAsync("/login.live.com/ppsecure/InlineConnect.srf", cancellationToken);

        await Assert.That(respones.StatusCode).IsEqualTo(HttpStatusCode.OK);

        await Assert.That(await respones.Content.ReadAsStringAsync(cancellationToken)).Contains("""
            href="/login"
            """);
    }

    [Test]
    public async Task Oidc(CancellationToken cancellationToken)
    {
        var cookieContainer = new CookieContainer();
        using var handler = new HttpClientHandler
        {
            CookieContainer = cookieContainer,
            AllowAutoRedirect = false,
        };
        using var client = new HttpClient(handler);

        var authServerUrl = _authServerClient.BaseAddress!;

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
            loginPageUri = new Uri(_webPortalClient.BaseAddress!, loginPageUri);
        }

        using var response3 = await client.GetAsync(loginPageUri, cancellationToken);
        await Assert.That(response3.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var loginHtml = await response3.Content.ReadAsStringAsync(cancellationToken);
        var loginToken = ExtractInputValue(loginHtml, "__RequestVerificationToken");
        await Assert.That(loginToken).IsNotEmpty();

        var loginPostContent = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("_handler", "login"),
            new KeyValuePair<string, string>("Input.Email", AccountEmail),
            new KeyValuePair<string, string>("Input.Password", AccountPassword),
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
            authorizeUri2 = new Uri(_webPortalClient.BaseAddress!, authorizeUri2);
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
        const string newUsername = "test_player";
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

    [Test]
    public async Task Rst2(CancellationToken cancellationToken)
    {
        var earthConnectionString = await _app.GetConnectionStringAsync("EarthDb", cancellationToken);
        Debug.Assert(earthConnectionString is not null);

        await using var earthDb = EarthDbContext.CreateFromConnection(earthConnectionString);

        var cryptoSecrets = await earthDb.GetOrInitializeSecretsAsync();

        var profile = await earthDb.GetOrCreateAccount(Guid.CreateVersion7(), null);
        profile.Username = "rst2";
        await earthDb.SaveChangesAsync(cancellationToken);

        var liveValidity = ValidityDatePair.Create(TimeSpan.FromMinutes(10));
        var liveToken = new AuthServer.Features.Live.Login.UserToken(
            profile.Id,
            profile.Username
        );
        var liveTokenString = JwtUtils.Sign(liveToken, cryptoSecrets.LoginUserTokenSecret, liveValidity);

        using var deviceCredsResponse = await _authServerClient.PostAsync("login.live.com/ppsecure/deviceaddcredential.srf", new StringContent("""
            <?xml version="1.0" encoding="UTF-8"?>
            <DeviceAddRequest>
                <ClientInfo name="MSAAndroidApp" version="1.0"/>
                <Authentication>
                    <Membername>lnklsdnfodspfkldsnf</Membername>
                    <Password>lkndsfdsfpomopsdjkf</Password>
                </Authentication>
            </DeviceAddRequest>
            """, new MediaTypeHeaderValue("application/x-www-form-urlencoded")), cancellationToken);

        await Assert.That(deviceCredsResponse).IsOk();

        int puid;
        {
            var xml = XElement.Parse(await deviceCredsResponse.Content.ReadAsStringAsync(cancellationToken));

            var successAttribute = xml.Attribute("Success")?.Value;
            var successElement = xml.Element("success")?.Value;
            var puidValue = xml.Element("puid")?.Value;

            await Assert.That(successAttribute).IsEqualTo("true");
            await Assert.That(successElement).IsEqualTo("true");

            await Assert.That(puidValue).IsNotNull();
            puid = int.Parse(puidValue!);
        }

        using var rst2DeviceResponse = await _authServerClient.PostAsync("/login.live.com/RST2.srf", new StringContent($$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope
                xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                xmlns:ps="http://schemas.microsoft.com/Passport/SoapServices/PPCRL"
                xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"
                xmlns:saml="urn:oasis:names:tc:SAML:1.0:assertion"
                xmlns:wsp="http://schemas.xmlsoap.org/ws/2004/09/policy"
                xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"
                xmlns:wsa="http://www.w3.org/2005/08/addressing"
                xmlns:wssc="http://schemas.xmlsoap.org/ws/2005/02/sc"
                xmlns:wst="http://schemas.xmlsoap.org/ws/2005/02/trust">
                <s:Header>
                    <wsa:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue</wsa:Action>
                    <wsa:To s:mustUnderstand="1">http://localhost/RST2.srf</wsa:To>
                    <wsa:MessageID>1751706048704</wsa:MessageID>
                    <ps:AuthInfo
                        xmlns:ps="http://schemas.microsoft.com/Passport/SoapServices/PPCRL" Id="PPAuthInfo">
                        <ps:BinaryVersion>11</ps:BinaryVersion>
                        <ps:DeviceType>Android</ps:DeviceType>
                        <ps:HostingApp>{F501FD64-9070-46AB-993C-6F7B71D8D883}</ps:HostingApp>
                    </ps:AuthInfo>
                    <wsse:Security>
                        <wsse:UsernameToken wsu:Id="devicesoftware">
                            <wsse:Username>lnklsdnfodspfkldsnf</wsse:Username>
                            <wsse:Password>lkndsfdsfpomopsdjkf</wsse:Password>
                        </wsse:UsernameToken>
                        <wsu:Timestamp wsu:Id="Timestamp"
                            xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
                            <wsu:Created>{{liveValidity.IssuedStr}}</wsu:Created>
                            <wsu:Expires>{{liveValidity.ExpiresStr}}</wsu:Expires>
                        </wsu:Timestamp>
                    </wsse:Security>
                </s:Header>
                <s:Body>
                    <wst:RequestSecurityToken
                        xmlns:wst="http://schemas.xmlsoap.org/ws/2005/02/trust" Id="RST0">
                        <wst:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</wst:RequestType>
                        <wsp:AppliesTo
                            xmlns:wsp="http://schemas.xmlsoap.org/ws/2004/09/policy">
                            <wsa:EndpointReference
                                xmlns:wsa="http://www.w3.org/2005/08/addressing">
                                <wsa:Address>http://Passport.NET/tb</wsa:Address>
                            </wsa:EndpointReference>
                        </wsp:AppliesTo>
                    </wst:RequestSecurityToken>
                </s:Body>
            </s:Envelope>
            """, Encoding.UTF8, "application/x-www-form-urlencoded"), cancellationToken);

        await Assert.That(rst2DeviceResponse).IsOk();

        string deviceToken;
        {
            var doc = XDocument.Parse(await rst2DeviceResponse.Content.ReadAsStringAsync(cancellationToken));

            XNamespace wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
            XNamespace wst = "http://schemas.xmlsoap.org/ws/2005/02/trust";
            XNamespace xenc = "http://www.w3.org/2001/04/xmlenc#";

            var createdValues = doc.Descendants(wsu + "Created")
                .Select(e => e.Value.Trim())
                .ToList();

            await Assert.That(createdValues).Count().IsEqualTo(2);
            await Assert.That(createdValues.Distinct()).Count().IsEqualTo(1);

            var expiresValues = doc.Descendants(wsu + "Expires")
                .Select(e => e.Value.Trim())
                .ToList();

            await Assert.That(expiresValues).Count().IsEqualTo(2);
            await Assert.That(expiresValues.Distinct()).Count().IsEqualTo(1);

            await Assert.That(DateTime.Parse(expiresValues[0])).IsAfter(DateTime.UtcNow);

            var cipherValue = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "CipherValue")
                ?.Value.Trim();

            await Assert.That(cipherValue).IsNotNullOrWhiteSpace();

            var binarySecret = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "BinarySecret")
                ?.Value.Trim();

            await Assert.That(binarySecret).IsNotNullOrWhiteSpace();

            deviceToken = cipherValue!;
        }

        var daXml = $"<EncryptedData Id=\"BinaryDAToken0\"><CipherData><CipherValue>{deviceToken}</CipherValue></CipherData></EncryptedData>";

        var deviceDATokenValue = $"ct={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}&hashalg=SHA256&bver=11&appid=%7BF501FD64-9070-46AB-993C-6F7B71D8D883%7D&da={Uri.EscapeDataString(daXml)}&nonce=gx6kRw%2FSeJTtF6yAOUkkoGpEMvIzqbXq0WnaUkhQjSE%3D&hash=NECWvAvk4nCiX4K2yjgNnyQE8F0yzOA5qhGKvpnDETE%3D";
        var deviceDATokenXmlEscaped = HttpUtility.HtmlEncode(deviceDATokenValue);

        using var rst2Response = await _authServerClient.PostAsync("/login.live.com/RST2.srf", new StringContent($$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <s:Envelope
            xmlns:s="http://www.w3.org/2003/05/soap-envelope"
            xmlns:ps="http://schemas.microsoft.com/Passport/SoapServices/PPCRL"
            xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"
            xmlns:saml="urn:oasis:names:tc:SAML:1.0:assertion"
            xmlns:wsp="http://schemas.xmlsoap.org/ws/2004/09/policy"
            xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"
            xmlns:wsa="http://www.w3.org/2005/08/addressing"
            xmlns:wssc="http://schemas.xmlsoap.org/ws/2005/02/sc"
            xmlns:wst="http://schemas.xmlsoap.org/ws/2005/02/trust">
            <s:Header>
                <wsa:Action s:mustUnderstand="1">http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue</wsa:Action>
                <wsa:To s:mustUnderstand="1">http://login.mce.com/RST2.srf</wsa:To>
                <wsa:MessageID>1751706048806</wsa:MessageID>
                <ps:AuthInfo
                    xmlns:ps="http://schemas.microsoft.com/Passport/SoapServices/PPCRL" Id="PPAuthInfo">
                    <ps:BinaryVersion>11</ps:BinaryVersion>
                    <ps:DeviceType>Android</ps:DeviceType>
                    <ps:HostingApp>{F501FD64-9070-46AB-993C-6F7B71D8D883}</ps:HostingApp>
                    <ps:InlineUX>Android</ps:InlineUX>
                    <ps:ConsentFlags>1</ps:ConsentFlags>
                    <ps:IsConnected>1</ps:IsConnected>
                    <ps:ClientAppURI>android-app://com.mojang.minecraftearth.H62DKCBHJP6WXXIV7RBFOGOL4NAK4E6Y</ps:ClientAppURI>
                    <ps:Telemetry>PackageMarket=com.google.android.packageinstaller</ps:Telemetry>
                </ps:AuthInfo>
                <wsse:Security>
                    <EncryptedData
                        xmlns="http://www.w3.org/2001/04/xmlenc#" Id="BinaryDAToken0">
                        <CipherData>
                            <CipherValue>{{liveTokenString}}</CipherValue>
                        </CipherData>
                    </EncryptedData>
                    <wsse:BinarySecurityToken ValueType="urn:liveid:sha1device" Id="DeviceDAToken">{{deviceDATokenXmlEscaped}}</wsse:BinarySecurityToken>
                    <wsu:Timestamp wsu:Id="Timestamp"
                        xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
                        <wsu:Created>{{liveValidity.IssuedStr}}</wsu:Created>
                        <wsu:Expires>{{liveValidity.ExpiresStr}}</wsu:Expires>
                    </wsu:Timestamp>
                </wsse:Security>
            </s:Header>
            <s:Body>
                <ps:RequestMultipleSecurityTokens
                    xmlns:ps="http://schemas.microsoft.com/Passport/SoapServices/PPCRL" Id="RSTS">
                    <wst:RequestSecurityToken
                        xmlns:wst="http://schemas.xmlsoap.org/ws/2005/02/trust" Id="RST0">
                        <wst:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</wst:RequestType>
                        <wsp:AppliesTo
                            xmlns:wsp="http://schemas.xmlsoap.org/ws/2004/09/policy">
                            <wsa:EndpointReference
                                xmlns:wsa="http://www.w3.org/2005/08/addressing">
                                <wsa:Address>http://Passport.NET/tb</wsa:Address>
                            </wsa:EndpointReference>
                        </wsp:AppliesTo>
                    </wst:RequestSecurityToken>
                    <wst:RequestSecurityToken
                        xmlns:wst="http://schemas.xmlsoap.org/ws/2005/02/trust" Id="RST1">
                        <wst:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</wst:RequestType>
                        <wsp:AppliesTo
                            xmlns:wsp="http://schemas.xmlsoap.org/ws/2004/09/policy">
                            <wsa:EndpointReference
                                xmlns:wsa="http://www.w3.org/2005/08/addressing">
                                <wsa:Address>cobrandid=90023&amp;scope=service%3A%3Auser.auth.xboxlive.com%3A%3Ambi_ssl</wsa:Address>
                            </wsa:EndpointReference>
                        </wsp:AppliesTo>
                        <wsp:PolicyReference
                        xmlns:wsp="http://schemas.xmlsoap.org/ws/2004/09/policy" URI="TOKEN_BROKER"/>
                    </wst:RequestSecurityToken>
                </ps:RequestMultipleSecurityTokens>
            </s:Body>
        </s:Envelope>
        """, Encoding.UTF8, "application/x-www-form-urlencoded"), cancellationToken);

        await Assert.That(rst2Response).IsOk();

        {
            var doc = XDocument.Parse(await rst2Response.Content.ReadAsStringAsync(cancellationToken));

            XNamespace xenc = "http://www.w3.org/2001/04/xmlenc#";
            XNamespace wssc = "http://schemas.xmlsoap.org/ws/2005/02/sc";

            var encryptedData = doc.Descendants(xenc + "EncryptedData")
                .FirstOrDefault(e => e.Attribute("Id")?.Value == "RSTR");
            await Assert.That(encryptedData).IsNotNull();

            var cipherValue = encryptedData!
                .Descendants(xenc + "CipherValue")
                .Select(e => e.Value.Trim())
                .FirstOrDefault();
            await Assert.That(cipherValue).IsNotNullOrWhiteSpace();

            var nonce = doc.Descendants(wssc + "Nonce")
                .Select(e => e.Value.Trim())
                .FirstOrDefault();
            await Assert.That(nonce).IsNotNullOrWhiteSpace();
        }
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

    public async ValueTask DisposeAsync()
    {
        _webPortalClient.Dispose();
        _authServerClient.Dispose();

        await _app.DisposeAsync();
    }

    private async Task DumpResourceLogsAsync(string resourceName)
    {
        var loggerService = _app.Services.GetRequiredService<ResourceLoggerService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            var logStream = loggerService.WatchAsync(resourceName);
            await foreach (var logBatch in logStream.WithCancellation(cts.Token))
            {
                foreach (var line in logBatch)
                {
                    Console.WriteLine($"[{resourceName}] {line.Content}");
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
