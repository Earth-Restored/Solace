using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Web;
using System.Xml.Linq;
using Aspire.Hosting.Testing;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;
using Solace.Db.Earth;

namespace Solace.IntegrationTests;

public sealed partial class LoginTests
{
    [Test]
    public async Task Login_Rst2(CancellationToken cancellationToken)
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
            """, Encoding.UTF8, "application/x-www-form-urlencoded"), cancellationToken);

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
            puid = int.Parse(puidValue!, CultureInfo.InvariantCulture);
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

            // can't use WhenParsedInto - https://github.com/thomhurst/TUnit/blob/718d7b5faa27bf0fef838f104d63e85cc048703f/src/TUnit.Assertions/Assertions/Strings/ParseAssertions.cs#L300
            await Assert.That(expiresValues[0])
                .IsParsableInto<DateTime>().WithFormatProvider(CultureInfo.InvariantCulture);

            await Assert.That(DateTime.Parse(expiresValues[0], CultureInfo.InvariantCulture)).IsAfter(DateTime.UtcNow);

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
}
