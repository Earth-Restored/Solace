using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Solace.AuthServer.Utils;
using Solace.Common.Asp;
using Solace.Common.Asp.Auth;

namespace Solace.AuthServer.Features.Live.Login;

public sealed partial class RST2
{
    private static readonly XmlReaderSettings xmlReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    private static readonly XmlSerializer xmlSerializer = new(typeof(SoapEnvelope));

    private static readonly Dictionary<string, string> namespaces = new()
    {
        { "S", "http://www.w3.org/2003/05/soap-envelope"},
        { "wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"},
        { "wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"},
        { "wsp", "http://schemas.xmlsoap.org/ws/2004/09/policy"},
        { "wst", "http://schemas.xmlsoap.org/ws/2005/02/trust"},
        { "wssc", "http://schemas.xmlsoap.org/ws/2005/02/sc"},
        { "wsa", "http://www.w3.org/2005/08/addressing"},
        { "ps", "http://schemas.microsoft.com/Passport/SoapServices/PPCRL"},
        { "psf", "http://schemas.microsoft.com/Passport/SoapServices/SOAPFault"},
        { "e", "http://www.w3.org/2001/04/xmlenc#"},
        { "ds", "http://www.w3.org/2000/09/xmldsig#"},
        { "ns1", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"},
    };

    // get 415 with Immediate.Apis, can't customize enough when using it
    public static void MapEndpoint(IEndpointRouteBuilder app)
        => app.MapPost("login.live.com/RST2.srf", HandleAsync)
        .DisableAntiforgery()
        .Accepts<string>("application/x-www-form-urlencoded");

    // todo: strongly typed response, same as request
    private static async Task<Results<ContentHttpResult, BadRequest>> HandleAsync(
        HttpContext httpContext,
        [FromServices] IOptions<AuthSettings> authSettingsOption,
        [FromServices] CryptoSecrets cryptoSecrets,
        ILogger<RST2> logger,
        CancellationToken cancellationToken)
    {
        var authSettings = authSettingsOption.Value;

        SoapEnvelope? request;

        string requestBody;
        using (var reader = new StreamReader(httpContext.Request.Body))
        {
            requestBody = await reader.ReadToEndAsync(cancellationToken);
        }

        try
        {
            // can't use StreamReader because XmlSerializer is fucking old and does not support async
            using (var stringReader = new StringReader(requestBody))
            {
                using (var xmlReader = XmlReader.Create(stringReader, xmlReaderSettings))
                {
                    request = (SoapEnvelope?)xmlSerializer.Deserialize(xmlReader);
                }
            }
        }
        catch (InvalidOperationException)
        {
            return TypedResults.BadRequest();
        }

        if (request is null or { Header: null } or { Body: null })
        {
            return TypedResults.BadRequest();
        }

        if (request.Body?.RequestSecurityToken is { } requestSecurityToken)
        {
            // device token request

            // todo: use UsernameToken

            if (requestSecurityToken.RequestType is not "http://schemas.xmlsoap.org/ws/2005/02/trust/Issue" ||
                requestSecurityToken.AppliesTo is not { EndpointReference: { Address: "http://Passport.NET/tb" } })
            {
                return TypedResults.BadRequest();
            }

            var headerValidity = ValidityDatePair.Create(authSettings.SoapHeaderValidityMinutes);

            var deviceTokenValidity = ValidityDatePair.Create(authSettings.DeviceTokenValidityMinutes);
            var deviceToken = new DeviceToken();
            var deviceTokenString = JwtUtils.Sign(deviceToken, cryptoSecrets.LoginDeviceTokenSecret, deviceTokenValidity);

            var response = new XmlDocument();

            var envelope = CreateElement(response, "S", "Envelope");
            envelope.SetAttribute("xmlns:wsse", LookupNamespace("wsse"));
            envelope.SetAttribute("xmlns:wsu", LookupNamespace("wsu"));
            envelope.SetAttribute("xmlns:wsp", LookupNamespace("wsp"));
            envelope.SetAttribute("xmlns:wst", LookupNamespace("wst"));
            envelope.SetAttribute("xmlns:wssc", LookupNamespace("wssc"));
            envelope.SetAttribute("xmlns:wsa", LookupNamespace("wsa"));
            envelope.SetAttribute("xmlns:ps", LookupNamespace("ps"));
            envelope.SetAttribute("xmlns:psf", LookupNamespace("psf"));
            envelope.SetAttribute("xmlns:e", LookupNamespace("e"));
            envelope.SetAttribute("xmlns:ds", LookupNamespace("ds"));

            var header = CreateElement(response, "S", "Header");
            {
                var security = CreateElement(response, "wsse", "Security");
                var timestamp = CreateElement(response, "wsu", "Timestamp");
                timestamp.SetAttribute("wsu:Id", "Timestamp");
                {
                    var created = CreateElement(response, "wsu", "Created");
                    created.InnerText = headerValidity.IssuedStr;
                    timestamp.AppendChild(created);
                    var expires = CreateElement(response, "wsu", "Expires");
                    expires.InnerText = headerValidity.ExpiresStr;
                    timestamp.AppendChild(expires);
                }

                security.AppendChild(timestamp);
                header.AppendChild(security);

                var pp = CreateElement(response, "psf", "pp");
                header.AppendChild(pp);
            }

            envelope.AppendChild(header);

            var body = CreateElement(response, "S", "Body");
            {
                var requestSecurityTokenResponse = CreateElement(response, "wst", "RequestSecurityTokenResponse");
                {
                    var tokenType = CreateElement(response, "wst", "TokenType");
                    tokenType.InnerText = "urn:passport:legacy";
                    requestSecurityTokenResponse.AppendChild(tokenType);

                    var appliesTo = CreateElement(response, "wsp", "AppliesTo");
                    {
                        var endpointReference = CreateElement(response, "wsa", "EndpointReference");
                        {
                            var address = CreateElement(response, "wsa", "Address");
                            address.InnerText = "http://Passport.NET/tb";
                            endpointReference.AppendChild(address);
                        }

                        appliesTo.AppendChild(endpointReference);
                    }

                    requestSecurityTokenResponse.AppendChild(appliesTo);

                    var lifetime = CreateElement(response, "wst", "Lifetime");
                    {
                        var created = CreateElement(response, "wsu", "Created");
                        created.InnerText = deviceTokenValidity.IssuedStr;
                        lifetime.AppendChild(created);

                        var expires = CreateElement(response, "wsu", "Expires");
                        expires.InnerText = deviceTokenValidity.ExpiresStr;
                        lifetime.AppendChild(expires);
                    }

                    requestSecurityTokenResponse.AppendChild(lifetime);

                    var requestedSecurityToken = CreateElement(response, "wst", "RequestedSecurityToken");
                    {
                        var encryptedData = response.CreateElement("EncryptedData");
                        encryptedData.SetAttribute("Id", "BinaryDAToken0");
                        {
                            var cipherData = response.CreateElement("CipherData");
                            {
                                var cipherValue = response.CreateElement("CipherValue");
                                cipherValue.InnerText = deviceTokenString;
                                cipherData.AppendChild(cipherValue);
                            }

                            encryptedData.AppendChild(cipherData);
                        }

                        requestedSecurityToken.AppendChild(encryptedData);
                    }

                    requestSecurityTokenResponse.AppendChild(requestedSecurityToken);

                    var requestedProofToken = CreateElement(response, "wst", "RequestedProofToken");
                    {
                        var binarySecret = CreateElement(response, "wst", "BinarySecret");
                        binarySecret.InnerText = "0000";
                        requestedProofToken.AppendChild(binarySecret);
                    }

                    requestSecurityTokenResponse.AppendChild(requestedProofToken);
                }

                body.AppendChild(requestSecurityTokenResponse);
            }

            envelope.AppendChild(body);

            response.AppendChild(envelope);

            return TypedResults.Content("""
                <?xml version="1.0" encoding="UTF-8"?>

                """ + response.OuterXml, contentType: "application/soap+xml");
        }
        else if (request.Body?.RequestMultipleSecurityTokens is { } requestMultipleSecurityTokens)
        {
            // user token request (user token + device token -> next user token + next session key + xbox token)

            Debug.Assert(request.Header.Security?.EncryptedData?.Id is "BinaryDAToken0");
            var userTokenString = request.Header.Security?.EncryptedData.CipherData?.CipherValue;
            Debug.Assert(request.Header.Security?.BinarySecurityToken?.Id is "DeviceDAToken");
            var deviceDATokenString = request.Header.Security?.BinarySecurityToken.Value;

            string? deviceDATokenXMLStringEncoded = null;
            if (!string.IsNullOrEmpty(deviceDATokenString))
            {
                var match = GetDeviceDATokenStringRegex().Match(deviceDATokenString);
                if (match.Success && match.Groups.Count > 1)
                {
                    deviceDATokenXMLStringEncoded = match.Groups[1].Value;
                }
            }

            var deviceDATokenXMLString = HttpUtility.UrlDecode(deviceDATokenXMLStringEncoded);

            var deviceTokenString = string.Empty;
            if (deviceDATokenXMLString is not null)
            {
                var deviceTokenXml = new XmlDocument();
                deviceTokenXml.LoadXml(deviceDATokenXMLString);
                deviceTokenString = deviceTokenXml.SelectSingleNode("/EncryptedData/CipherData/CipherValue")?.InnerText ?? string.Empty;
            }

            if (requestMultipleSecurityTokens.SecurityTokenRequests is null)
            {
                return TypedResults.BadRequest();
            }

            if (requestMultipleSecurityTokens.SecurityTokenRequests.Count is not 2)
            {
                return TypedResults.BadRequest();
            }

            if (requestMultipleSecurityTokens.SecurityTokenRequests[0].RequestType is not "http://schemas.xmlsoap.org/ws/2005/02/trust/Issue" ||
                requestMultipleSecurityTokens.SecurityTokenRequests[0].AppliesTo?.EndpointReference?.Address is not "http://Passport.NET/tb" ||
                requestMultipleSecurityTokens.SecurityTokenRequests[1].RequestType is not "http://schemas.xmlsoap.org/ws/2005/02/trust/Issue" ||
                requestMultipleSecurityTokens.SecurityTokenRequests[1].AppliesTo?.EndpointReference?.Address is not "cobrandid=90023&scope=service%3A%3Auser.auth.xboxlive.com%3A%3Ambi_ssl" ||
                userTokenString is null)
            {
                return TypedResults.BadRequest();
            }

            var userToken = JwtUtils.Verify<UserToken>(userTokenString, cryptoSecrets.LoginUserTokenSecret, logger, allowExpired: true);
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            var deviceToken = JwtUtils.Verify<DeviceToken>(deviceTokenString, cryptoSecrets.LoginDeviceTokenSecret, logger, allowExpired: true);
#pragma warning restore IDE0059 // Unnecessary assignment of a value

            if (userToken is null or { Expired: true, })
            {
                var headerValidity = ValidityDatePair.Create(authSettings.SoapHeaderValidityMinutes);
                var nonce = GenerateNonce();

                var scheme = httpContext.Request.IsHttps ? "https" : "http";
                var host = httpContext.Request.Host.Value!;
                var path = httpContext.Request.Path.Value ?? "";

                if (path.EndsWith("RST2.srf", StringComparison.OrdinalIgnoreCase))
                {
                    path = path[..^"RST2.srf".Length];
                }

                if (!path.EndsWith('/'))
                {
                    path += "/";
                }

                var reauthenticateURL = userToken is not null
                    ? $"{scheme}://{host}{path}ppsecure/reauthenticateStart?username={HttpUtility.UrlEncode(userToken.Data.Username)}&userToken={HttpUtility.UrlEncode(userTokenString)}"
                    : $"{scheme}://{host}{path}ppsecure/InlineConnect.srf";

                var reauthenticateURLDocument = new XmlDocument();
                var ppEle = CreateElement(reauthenticateURLDocument, "psf", "pp");
                var inlineauthurlEle = CreateElement(reauthenticateURLDocument, "psf", "inlineauthurl");
                inlineauthurlEle.InnerText = reauthenticateURL;
                ppEle.AppendChild(inlineauthurlEle);
                reauthenticateURLDocument.AppendChild(ppEle);

                var reauthenticateURLDocumentCipherText = DoAESEncryption(
                    ImmutableCollectionsMarshal.AsArray(cryptoSecrets.LoginUserTokenSessionKey)!,
                    nonce,
                    reauthenticateURLDocument.OuterXml
                );

                var response = new XmlDocument();
                var envelope = CreateElement(response, "S", "Envelope");
                {
                    var header = CreateElement(response, "S", "Header");
                    {
                        var security = CreateElement(response, "wsse", "Security");
                        {
                            var timestamp = CreateElement(response, "wsu", "Timestamp");
                            {
                                var created = CreateElement(response, "wsu", "Created");
                                created.InnerText = headerValidity.IssuedStr;
                                timestamp.AppendChild(created);

                                var expires = CreateElement(response, "wsu", "Expires");
                                expires.InnerText = headerValidity.ExpiresStr;
                                timestamp.AppendChild(expires);
                            }

                            security.AppendChild(timestamp);

                            var derivedKeyToken = response.CreateElement("wssc", "DerivedKeyToken", "http://schemas.xmlsoap.org/ws/2005/02/sc");
                            derivedKeyToken.SetAttribute("xmlns:wssc", "http://schemas.xmlsoap.org/ws/2005/02/sc");
                            derivedKeyToken.SetAttribute("xmlns:ns1", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");

                            var idAttr = response.CreateAttribute("ns1", "Id", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
                            idAttr.Value = "EncKey";
                            derivedKeyToken.Attributes.Append(idAttr);
                            derivedKeyToken.SetAttribute("Algorithm", "urn:liveid:SP800-108CTR-HMAC-SHA256");
                            {
                                var nonceEle = response.CreateElement("wssc", "Nonce", "http://schemas.xmlsoap.org/ws/2005/02/sc");
                                nonceEle.InnerText = nonce;
                                derivedKeyToken.AppendChild(nonceEle);
                            }

                            security.AppendChild(derivedKeyToken);
                        }

                        header.AppendChild(security);

                        var encryptedPP = CreateElement(response, "psf", "EncryptedPP");
                        {
                            var encryptedData = CreateElement(response, "e", "EncryptedData");
                            encryptedData.SetAttribute("Id", "EncPsf");
                            encryptedData.SetAttribute("Type", "http://www.w3.org/2001/04/xmlenc#Element");

                            var encryptionMethod = CreateElement(response, "e", "EncryptionMethod");
                            encryptionMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#aes256-cbc");
                            encryptedData.AppendChild(encryptionMethod);

                            var keyInfo = CreateElement(response, "ds", "KeyInfo");
                            {
                                var str = CreateElement(response, "wsse", "SecurityTokenReference");
                                var reference = CreateElement(response, "wsse", "Reference");
                                reference.SetAttribute("URI", "#EncKey");
                                str.AppendChild(reference);
                                keyInfo.AppendChild(str);
                            }

                            encryptedData.AppendChild(keyInfo);

                            var cipherData = CreateElement(response, "e", "CipherData");
                            {
                                var cipherValue = CreateElement(response, "e", "CipherValue");
                                cipherValue.InnerText = reauthenticateURLDocumentCipherText;
                                cipherData.AppendChild(cipherValue);
                            }

                            encryptedData.AppendChild(cipherData);
                        }

                        header.AppendChild(encryptedPP);
                    }

                    envelope.AppendChild(header);

                    var body = CreateElement(response, "S", "Body");
                    {
                        var fault = CreateElement(response, "S", "Fault");
                        {
                            var detail = CreateElement(response, "S", "Detail");
                            {
                                var error = CreateElement(response, "psf", "error");
                                {
                                    var value = CreateElement(response, "psf", "value");
                                    value.InnerText = "0";
                                    error.AppendChild(value);

                                    var internalerror = CreateElement(response, "psf", "internalerror");
                                    {
                                        var code = CreateElement(response, "psf", "code");
                                        code.InnerText = "0";
                                        internalerror.AppendChild(code);
                                    }

                                    error.AppendChild(internalerror);
                                }

                                detail.AppendChild(error);
                            }

                            fault.AppendChild(detail);
                        }

                        body.AppendChild(fault);
                    }

                    envelope.AppendChild(body);
                }

                response.AppendChild(envelope);

                return TypedResults.Content(response.OuterXml);
            }
            else
            {
                var headerValidity = ValidityDatePair.Create(authSettings.SoapHeaderValidityMinutes);
                var nonce = GenerateNonce();

                var nextUserTokenValidity = ValidityDatePair.Create(authSettings.UserTokenValidityMinutes);
                var nextUserToken = userToken.Data;
                var nextUserTokenString = JwtUtils.Sign(nextUserToken, cryptoSecrets.LoginUserTokenSecret, nextUserTokenValidity);

                var xboxTokenValidity = ValidityDatePair.Create(authSettings.XboxTokenValidityMinutes);
                var xboxToken = new Common.XboxTicketToken(userToken.Data.UserId, userToken.Data.Username);
                var xboxTokenString = JwtUtils.Sign(xboxToken, cryptoSecrets.LoginXboxTokenSecret, xboxTokenValidity);

                var nextSessionKey = cryptoSecrets.LoginUserTokenSessionKeyBase64; // todo: random?

                var tokenDocument = new XmlDocument();

                var requestSecurityTokenResponseCollection = CreateElement(tokenDocument, "wst", "RequestSecurityTokenResponseCollection");
                {
                    var encryptedData = tokenDocument.CreateElement("EncryptedData");
                    encryptedData.SetAttribute("xmlns", "http://www.w3.org/2001/04/xmlenc#");
                    encryptedData.SetAttribute("Id", "BinaryDAToken0");
                    {
                        var cipherData = tokenDocument.CreateElement("CipherData");
                        {
                            var cipherValue = tokenDocument.CreateElement("CipherValue");
                            cipherValue.InnerText = nextUserTokenString;
                            cipherData.AppendChild(cipherValue);
                        }

                        encryptedData.AppendChild(cipherData);
                    }

                    var binarySecret = CreateElement(tokenDocument, "wst", "BinarySecret");
                    binarySecret.InnerText = nextSessionKey;

                    AddTokenResponse("urn:passport:legacy", "http://Passport.NET/tb",
                        nextUserTokenValidity.IssuedStr, nextUserTokenValidity.ExpiresStr,
                        encryptedData, binarySecret);

                    var binarySecurityToken = CreateElement(tokenDocument, "wsse", "BinarySecurityToken");
                    binarySecurityToken.SetAttribute("Id", "Compact1");
                    binarySecurityToken.InnerText = xboxTokenString;

                    AddTokenResponse("urn:passport:compact", "cobrandid=90023&scope=service%3A%3Auser.auth.xboxlive.com%3A%3Ambi_ssl", xboxTokenValidity.IssuedStr, xboxTokenValidity.ExpiresStr, binarySecurityToken, null);

                    void AddTokenResponse(string tokenType, string address, string issued, string expires, XmlElement securityToken, XmlElement? proofToken)
                    {
                        var requestSecurityTokenResponse = CreateElement(tokenDocument, "wst", "RequestSecurityTokenResponse");
                        {
                            var tokenTypeEle = CreateElement(tokenDocument, "wst", "TokenType");
                            tokenTypeEle.InnerText = tokenType;
                            requestSecurityTokenResponse.AppendChild(tokenTypeEle);

                            var appliesTo = CreateElement(tokenDocument, "wsp", "AppliesTo");
                            {
                                var endpointReference = CreateElement(tokenDocument, "wsa", "EndpointReference");
                                {
                                    var addressEle = CreateElement(tokenDocument, "wsa", "Address");
                                    addressEle.InnerText = address;
                                    endpointReference.AppendChild(addressEle);
                                }

                                appliesTo.AppendChild(endpointReference);
                            }

                            requestSecurityTokenResponse.AppendChild(appliesTo);

                            var lifetime = CreateElement(tokenDocument, "wst", "Lifetime");
                            {
                                var createdEle = CreateElement(tokenDocument, "wsu", "Created");
                                createdEle.InnerText = issued;
                                lifetime.AppendChild(createdEle);

                                var expiresEle = CreateElement(tokenDocument, "wsu", "Expires");
                                expiresEle.InnerText = expires;
                                lifetime.AppendChild(expiresEle);
                            }

                            requestSecurityTokenResponse.AppendChild(lifetime);

                            var requestedSecurityToken = CreateElement(tokenDocument, "wst", "RequestedSecurityToken");
                            requestedSecurityToken.AppendChild(securityToken);

                            requestSecurityTokenResponse.AppendChild(requestedSecurityToken);

                            if (proofToken is not null)
                            {
                                var requestedProofToken = CreateElement(tokenDocument, "wst", "RequestedProofToken");
                                requestedProofToken.AppendChild(proofToken);

                                requestSecurityTokenResponse.AppendChild(requestedProofToken);
                            }
                        }

                        requestSecurityTokenResponseCollection.AppendChild(requestSecurityTokenResponse);
                    }
                }

                tokenDocument.AppendChild(requestSecurityTokenResponseCollection);
                var tokenDocumentString = tokenDocument.OuterXml;

                var tokenDocumentCipherText = DoAESEncryption(ImmutableCollectionsMarshal.AsArray(cryptoSecrets.LoginUserTokenSessionKey)!, nonce, tokenDocumentString);

                var response = new XmlDocument();
                var envelope = CreateElement(response, "S", "Envelope");
                {
                    var header = CreateElement(response, "S", "Header");
                    {
                        var security = CreateElement(response, "wsse", "Security");
                        {
                            var timestamp = CreateElement(response, "wsu", "Timestamp");
                            {
                                var created = CreateElement(response, "wsu", "Created");
                                created.InnerText = headerValidity.IssuedStr;
                                timestamp.AppendChild(created);

                                var expires = CreateElement(response, "wsu", "Expires");
                                expires.InnerText = headerValidity.ExpiresStr;
                                timestamp.AppendChild(expires);
                            }

                            security.AppendChild(timestamp);

                            var derivedKeyToken = response.CreateElement("wssc", "DerivedKeyToken", "http://schemas.xmlsoap.org/ws/2005/02/sc");
                            derivedKeyToken.SetAttribute("xmlns:wssc", "http://schemas.xmlsoap.org/ws/2005/02/sc");
                            derivedKeyToken.SetAttribute("xmlns:ns1", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
                            var idAttr = response.CreateAttribute("ns1", "Id", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
                            idAttr.Value = "EncKey";
                            derivedKeyToken.Attributes.Append(idAttr);
                            derivedKeyToken.SetAttribute("Algorithm", "urn:liveid:SP800-108CTR-HMAC-SHA256");
                            {
                                var nonceEle = response.CreateElement("wssc", "Nonce", "http://schemas.xmlsoap.org/ws/2005/02/sc");
                                nonceEle.InnerText = nonce;

                                derivedKeyToken.AppendChild(nonceEle);
                            }

                            security.AppendChild(derivedKeyToken);
                        }

                        header.AppendChild(security);
                    }

                    envelope.AppendChild(header);

                    var body = CreateElement(response, "S", "Body");
                    {
                        var encryptedData = response.CreateElement("EncryptedData");
                        encryptedData.SetAttribute("xmlns", "http://www.w3.org/2001/04/xmlenc#");
                        encryptedData.SetAttribute("Id", "RSTR");
                        encryptedData.SetAttribute("Type", "http://www.w3.org/2001/04/xmlenc#Element");
                        {
                            var encryptionMethod = response.CreateElement("EncryptionMethod");
                            encryptionMethod.SetAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#aes256-cbc");
                            encryptedData.AppendChild(encryptionMethod);

                            var keyInfo = response.CreateElement("KeyInfo");
                            keyInfo.SetAttribute("xmlns", "http://www.w3.org/2000/09/xmldsig#");
                            {
                                var securityTokenReference = CreateElement(response, "wsse", "SecurityTokenReference");
                                {
                                    var reference = CreateElement(response, "wsse", "Reference");
                                    reference.SetAttribute("URI", "#EncKey");
                                    securityTokenReference.AppendChild(reference);
                                }

                                keyInfo.AppendChild(securityTokenReference);
                            }

                            encryptedData.AppendChild(keyInfo);

                            var cipherData = response.CreateElement("CipherData");
                            {
                                var cipherValue = response.CreateElement("CipherValue");
                                cipherValue.InnerText = tokenDocumentCipherText;
                                cipherData.AppendChild(cipherValue);
                            }

                            encryptedData.AppendChild(cipherData);
                        }

                        body.AppendChild(encryptedData);
                    }

                    envelope.AppendChild(body);
                }

                response.AppendChild(envelope);

                return TypedResults.Content(response.OuterXml);
            }
        }
        else
        {
            return TypedResults.BadRequest();
        }
    }

    private static string LookupNamespace(string prefix)
        => namespaces[prefix];

    private static XmlElement CreateElement(XmlDocument doc, string prefix, string localName)
        => doc.CreateElement(prefix, localName, LookupNamespace(prefix));

    private static string GenerateNonce()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(32);

        var bufferSpan = buffer.AsSpan();
        RandomNumberGenerator.Fill(bufferSpan);
        var base64 = Convert.ToBase64String(bufferSpan);

        ArrayPool<byte>.Shared.Return(buffer);

        return base64;
    }

    private static string DoAESEncryption(byte[] sessionKey, string nonceBase64, string plainText)
    {
        var nonce = Convert.FromBase64String(nonceBase64);
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);

        byte[]? messageKey;
        using (var hmac = new HMACSHA256(sessionKey))
        {
            var w1 = hmac.TransformBlock([0, 0, 0, 1], 0, 4, null, 0);
            var labelBytes = Encoding.UTF8.GetBytes("WS-SecureConversationWS-SecureConversation");
            var w2 = hmac.TransformBlock(labelBytes, 0, labelBytes.Length, null, 0);
            var w3 = hmac.TransformBlock([0], 0, 1, null, 0);
            var w4 = hmac.TransformBlock(nonce, 0, nonce.Length, null, 0);
            var w5 = hmac.TransformFinalBlock([0, 0, 1, 0], 0, 4);

            messageKey = hmac.Hash;
        }

        Debug.Assert(messageKey is not null);

        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);

        // Encrypt with AES-256-CBC
        byte[] cipherText;
        using (var aes = Aes.Create())
        {
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = messageKey;
            aes.IV = iv;

#pragma warning disable CA5401 // Do not use CreateEncryptor with non-default IV
            using var encryptor = aes.CreateEncryptor(messageKey, iv);

            var cipherData = encryptor.TransformFinalBlock(plainTextBytes, 0, plainTextBytes.Length);
            cipherText = new byte[iv.Length + cipherData.Length];
            iv.AsSpan().CopyTo(cipherText.AsSpan());
            cipherData.AsSpan().CopyTo(cipherText.AsSpan(iv.Length..));
#pragma warning restore CA5401 // Do not use CreateEncryptor with non-default IV
        }

        return Convert.ToBase64String(cipherText);
    }

    [GeneratedRegex("&da=([^&]*)")]
    private static partial Regex GetDeviceDATokenStringRegex();

    [XmlRoot(ElementName = "Envelope", Namespace = "http://www.w3.org/2003/05/soap-envelope")]
    public sealed class SoapEnvelope
    {
        [XmlElement(ElementName = "Header", Namespace = "http://www.w3.org/2003/05/soap-envelope")]
        public SoapHeader? Header { get; set; }

        [XmlElement(ElementName = "Body", Namespace = "http://www.w3.org/2003/05/soap-envelope")]
        public SoapBody? Body { get; set; }
    }

    public sealed class SoapHeader
    {
        [XmlElement(ElementName = "Action", Namespace = "http://www.w3.org/2005/08/addressing")]
        public string? Action { get; set; }

        [XmlElement(ElementName = "To", Namespace = "http://www.w3.org/2005/08/addressing")]
        public string? To { get; set; }

        [XmlElement(ElementName = "MessageID", Namespace = "http://www.w3.org/2005/08/addressing")]
        public string? MessageID { get; set; }

        [XmlElement(ElementName = "AuthInfo", Namespace = "http://schemas.microsoft.com/Passport/SoapServices/PPCRL")]
        public AuthInfo? AuthInfo { get; set; }

        [XmlElement(ElementName = "Security", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd")]
        public SecurityHeader? Security { get; set; }
    }

    public sealed class AuthInfo
    {
        [XmlElement(ElementName = "BinaryVersion")]
        public int BinaryVersion { get; set; }

        [XmlElement(ElementName = "DeviceType")]
        public string? DeviceType { get; set; }

        [XmlElement(ElementName = "HostingApp")]
        public string? HostingApp { get; set; }

        [XmlElement(ElementName = "InlineUX")]
        public string? InlineUX { get; set; }

        [XmlElement(ElementName = "ConsentFlags")]
        public string? ConsentFlags { get; set; }

        [XmlElement(ElementName = "IsConnected")]
        public string? IsConnected { get; set; }

        [XmlElement(ElementName = "ClientAppURI")]
        public string? ClientAppURI { get; set; }
    }

    public sealed class SecurityHeader
    {
        [XmlElement(ElementName = "UsernameToken", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd")]
        public UsernameToken? UsernameToken { get; set; }

        [XmlElement(ElementName = "EncryptedData", Namespace = "http://www.w3.org/2001/04/xmlenc#")]
        public EncryptedData? EncryptedData { get; set; }

        [XmlElement(ElementName = "BinarySecurityToken", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd")]
        public BinarySecurityToken? BinarySecurityToken { get; set; }

        [XmlElement(ElementName = "Timestamp", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd")]
        public Timestamp? Timestamp { get; set; }

        // Catch-all for extra complex XML structures like Signatures or DerivedKeyTokens to prevent parsing failures
        [XmlAnyElement]
        public XmlElement[]? AdditionalElements { get; set; }
    }

    public sealed class UsernameToken
    {
        [XmlAttribute(AttributeName = "Id", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd")]
        public string? Id { get; set; }

        [XmlElement(ElementName = "Username", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd")]
        public string? Username { get; set; }

        [XmlElement(ElementName = "Password", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd")]
        public string? Password { get; set; }
    }

    public sealed class EncryptedData
    {
        [XmlAttribute(AttributeName = "Id")]
        public string? Id { get; set; }

        [XmlAttribute(AttributeName = "Type")]
        public string? Type { get; set; }

        [XmlElement(ElementName = "CipherData", Namespace = "http://www.w3.org/2001/04/xmlenc#")]
        public CipherData? CipherData { get; set; }
    }

    public sealed class CipherData
    {
        [XmlElement(ElementName = "CipherValue", Namespace = "http://www.w3.org/2001/04/xmlenc#")]
        public string? CipherValue { get; set; }
    }

    public sealed class BinarySecurityToken
    {
        [XmlAttribute(AttributeName = "ValueType")]
        public string? ValueType { get; set; }

        [XmlAttribute(AttributeName = "Id")]
        public string? Id { get; set; }

        [XmlText]
        public string? Value { get; set; }
    }

    public sealed class Timestamp
    {
        [XmlAttribute(AttributeName = "Id", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd")]
        public string? Id { get; set; }

        [XmlElement(ElementName = "Created", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd")]
        public string? Created { get; set; }

        [XmlElement(ElementName = "Expires", Namespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd")]
        public string? Expires { get; set; }
    }

    public sealed class SoapBody
    {
        [XmlElement(ElementName = "RequestSecurityToken", Namespace = "http://schemas.xmlsoap.org/ws/2005/02/trust")]
        public RequestSecurityToken? RequestSecurityToken { get; set; }

        [XmlElement(ElementName = "RequestMultipleSecurityTokens", Namespace = "http://schemas.microsoft.com/Passport/SoapServices/PPCRL")]
        public RequestMultipleSecurityTokens? RequestMultipleSecurityTokens { get; set; }
    }

    public sealed class RequestMultipleSecurityTokens
    {
        [XmlAttribute(AttributeName = "Id")]
        public string? Id { get; set; }

        [XmlElement(ElementName = "RequestSecurityToken", Namespace = "http://schemas.xmlsoap.org/ws/2005/02/trust")]
        public List<RequestSecurityToken>? SecurityTokenRequests { get; set; }
    }

    public sealed class RequestSecurityToken
    {
        [XmlAttribute(AttributeName = "Id")]
        public string? Id { get; set; }

        [XmlElement(ElementName = "RequestType", Namespace = "http://schemas.xmlsoap.org/ws/2005/02/trust")]
        public string? RequestType { get; set; }

        [XmlElement(ElementName = "AppliesTo", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/policy")]
        public AppliesTo? AppliesTo { get; set; }
    }

    public sealed class AppliesTo
    {
        [XmlElement(ElementName = "EndpointReference", Namespace = "http://www.w3.org/2005/08/addressing")]
        public EndpointReference? EndpointReference { get; set; }
    }

    public sealed class EndpointReference
    {
        [XmlElement(ElementName = "Address", Namespace = "http://www.w3.org/2005/08/addressing")]
        public string? Address { get; set; }
    }
}