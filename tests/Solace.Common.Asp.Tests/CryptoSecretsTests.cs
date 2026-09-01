using System.Collections.Immutable;
using Solace.Common.Asp.Auth;

namespace Solace.Common.Asp.Tests;

public sealed class CryptoSecretsTests
{
    private static ImmutableArray<byte> CreateSecret(byte fill = 1)
        => [.. Enumerable.Repeat(fill, 32)];

    [Test]
    public async Task Constructor_WithImmutableArrays_InitializesProperties()
    {
        var secret1 = CreateSecret(1);
        var secret2 = CreateSecret(2);
        var secret3 = CreateSecret(3);
        var secret4 = CreateSecret(4);
        var secret5 = CreateSecret(5);
        var secret6 = CreateSecret(6);
        var secret7 = CreateSecret(7);
        var secret8 = CreateSecret(8);
        var secret9 = CreateSecret(9);

        var secrets = new CryptoSecrets(secret1, secret2, secret3, secret4, secret5, secret6, secret7, secret8, secret9);

        await Assert.That(secrets.LoginUserTokenSecret).IsEqualTo(secret1);
        await Assert.That(secrets.LoginDeviceTokenSecret).IsEqualTo(secret2);
        await Assert.That(secrets.LoginXboxTokenSecret).IsEqualTo(secret3);
        await Assert.That(secrets.LoginUserTokenSessionKey).IsEqualTo(secret4);
        await Assert.That(secrets.LoginUserTokenSessionKeyBase64).IsEqualTo(Convert.ToBase64String([.. secret4]));
        await Assert.That(secrets.LiveAuthTokenSecret).IsEqualTo(secret5);
        await Assert.That(secrets.LiveXapiTokenSecret).IsEqualTo(secret6);
        await Assert.That(secrets.LivePlayfabTokenSecret).IsEqualTo(secret7);
        await Assert.That(secrets.PlayfabEntityTokenSecret).IsEqualTo(secret8);
        await Assert.That(secrets.PlayfabSessionTicketSecret).IsEqualTo(secret9);
    }

    [Test]
    public async Task Constructor_WithEmptySecret_ThrowsArgumentException()
    {
        var validSecret = CreateSecret(1);
        var emptySecret = ImmutableArray<byte>.Empty;

        Action action = () => _ = new CryptoSecrets(
            emptySecret, validSecret, validSecret, validSecret,
            validSecret, validSecret, validSecret, validSecret, validSecret);

        await Assert.That(action).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WithDictionary_ParsesBase64Secrets()
    {
        var dummyBytes = Convert.ToBase64String([1, 2, 3, 4,]);
        var dict = CryptoSecrets.AllNames.ToDictionary(name => name, _ => dummyBytes, StringComparer.Ordinal);

        var secrets = new CryptoSecrets(dict);

        await Assert.That(secrets.LoginUserTokenSecret.SequenceEqual(new byte[] { 1, 2, 3, 4, })).IsTrue();
        await Assert.That(secrets.LoginUserTokenSessionKeyBase64).IsEqualTo(dummyBytes);
    }

    [Test]
    public async Task Constructor_WithDictionaryMissingKey_ThrowsArgumentException()
    {
        var dummyBytes = Convert.ToBase64String([1, 2, 3, 4,]);
        var dict = CryptoSecrets.AllNames.Take(5).ToDictionary(name => name, _ => dummyBytes, StringComparer.Ordinal);

        Action action = () => _ = new CryptoSecrets(dict);

        await Assert.That(action).Throws<ArgumentException>();
    }

    [Test]
    public async Task AllNames_ContainsNineSecrets()
    {
        await Assert.That(CryptoSecrets.AllNames.Length).IsEqualTo(9);
        await Assert.That(CryptoSecrets.AllNames.Contains(CryptoSecrets.LoginUserTokenName)).IsTrue();
        await Assert.That(CryptoSecrets.AllNames.Contains(CryptoSecrets.PlayfabSessionTicketName)).IsTrue();
    }
}
