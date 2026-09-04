using System.Collections.Immutable;
using Microsoft.Extensions.Logging.Abstractions;
using Solace.Common.Asp.Auth;

namespace Solace.Common.Asp.Tests;

public sealed class JwtUtilsTests
{
    public sealed record TestTokenData(string UserId, string Role) : ITokenData<TestTokenData>;

    private static readonly ImmutableArray<byte> Secret = [.. Enumerable.Repeat((byte)0x42, 32)];
    private static readonly ImmutableArray<byte> WrongSecret = [.. Enumerable.Repeat((byte)0x99, 32)];

    [Test]
    public async Task SignAndVerify_ValidToken_ReturnsDeserializedData()
    {
        var data = new TestTokenData("user123", "admin");
        var validity = ValidityDatePair.Create(60);

        var tokenString = JwtUtils.Sign(data, Secret, validity);
        await Assert.That(tokenString).IsNotNull();

        var verifiedToken = JwtUtils.Verify<TestTokenData>(tokenString, Secret, NullLogger.Instance);

        await Assert.That(verifiedToken).IsNotNull();
        await Assert.That(verifiedToken!.Data.UserId).IsEqualTo("user123");
        await Assert.That(verifiedToken.Data.Role).IsEqualTo("admin");
        await Assert.That(verifiedToken.Expired).IsFalse();
    }

    [Test]
    public async Task Verify_WrongSecret_ReturnsNull()
    {
        var data = new TestTokenData("user123", "admin");
        var validity = ValidityDatePair.Create(60);

        var tokenString = JwtUtils.Sign(data, Secret, validity);
        var verifiedToken = JwtUtils.Verify<TestTokenData>(tokenString, WrongSecret, NullLogger.Instance);

        await Assert.That(verifiedToken).IsNull();
    }

    [Test]
    public async Task Verify_ExpiredToken_ReturnsNullWhenAllowExpiredFalse()
    {
        var data = new TestTokenData("user123", "admin");
        var past = DateTimeOffset.UtcNow.AddHours(-2);
        var validity = new ValidityDatePair(past, past.AddMinutes(30));

        var tokenString = JwtUtils.Sign(data, Secret, validity);
        var verifiedToken = JwtUtils.Verify<TestTokenData>(tokenString, Secret, NullLogger.Instance, allowExpired: false);

        await Assert.That(verifiedToken).IsNull();
    }

    [Test]
    public async Task Verify_ExpiredToken_ReturnsTokenWhenAllowExpiredTrue()
    {
        var data = new TestTokenData("user123", "admin");
        var past = DateTimeOffset.UtcNow.AddHours(-2);
        var validity = new ValidityDatePair(past, past.AddMinutes(30));

        var tokenString = JwtUtils.Sign(data, Secret, validity);
        var verifiedToken = JwtUtils.Verify<TestTokenData>(tokenString, Secret, NullLogger.Instance, allowExpired: true);

        await Assert.That(verifiedToken).IsNotNull();
        await Assert.That(verifiedToken!.Data.UserId).IsEqualTo("user123");
        await Assert.That(verifiedToken.Expired).IsTrue();
    }

    [Test]
    public async Task Verify_InvalidJwtSignature_ReturnsNull()
    {
        var malformedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpYXQiOjE2MDAwMDAwMDB9.invalid_signature";
        var verifiedToken = JwtUtils.Verify<TestTokenData>(malformedToken, Secret, NullLogger.Instance);
        await Assert.That(verifiedToken).IsNull();
    }
}
