using System.Collections.Immutable;
using System.Runtime.InteropServices;
using BitcoderCZ.Utils;

namespace Solace.Common.Asp.Auth;

public sealed class CryptoSecrets
{
    public const string LoginUserTokenName = "LoginUserTokenSecret";
    public const string LoginDeviceTokenName = "LoginDeviceTokenSecret";
    public const string LoginXboxTokenName = "LoginXboxTokenSecret";
    public const string LoginUserTokenSessionKeyName = "LoginUserTokenSessionKey";

    public const string LiveAuthTokenName = "LiveAuthTokenSecret";
    public const string LiveXapiTokenName = "LiveXapiTokenSecret";
    public const string LivePlayfabTokenName = "LivePlayfabTokenSecret";

    public const string PlayfabEntityTokenName = "PlayfabEntityTokenSecret";
    public const string PlayfabSessionTicketName = "PlayfabSessionTicketSecret";

    public static readonly ImmutableArray<string> AllNames =
    [
        LoginUserTokenName,
        LoginDeviceTokenName,
        LoginXboxTokenName,
        LoginUserTokenSessionKeyName,
        LiveAuthTokenName,
        LiveXapiTokenName,
        LivePlayfabTokenName,
        PlayfabEntityTokenName,
        PlayfabSessionTicketName,
    ];

    public CryptoSecrets(ImmutableArray<byte> loginUserTokenSecret, ImmutableArray<byte> loginDeviceTokenSecret, ImmutableArray<byte> loginXboxTokenSecret, ImmutableArray<byte> loginUserTokenSessionKey, ImmutableArray<byte> liveAuthTokenSecret, ImmutableArray<byte> liveXapiTokenSecret, ImmutableArray<byte> livePlayfabTokenSecret, ImmutableArray<byte> playfabEntityTokenSecret, ImmutableArray<byte> playfabSessionTicketSecret)
    {
        ThrowHelper.ThrowIfNullOrEmpty(loginUserTokenSecret);
        ThrowHelper.ThrowIfNullOrEmpty(loginDeviceTokenSecret);
        ThrowHelper.ThrowIfNullOrEmpty(loginXboxTokenSecret);
        ThrowHelper.ThrowIfNullOrEmpty(loginUserTokenSessionKey);

        ThrowHelper.ThrowIfNullOrEmpty(liveAuthTokenSecret);
        ThrowHelper.ThrowIfNullOrEmpty(liveXapiTokenSecret);
        ThrowHelper.ThrowIfNullOrEmpty(livePlayfabTokenSecret);

        ThrowHelper.ThrowIfNullOrEmpty(playfabEntityTokenSecret);
        ThrowHelper.ThrowIfNullOrEmpty(playfabSessionTicketSecret);

        LoginUserTokenSecret = loginUserTokenSecret;
        LoginDeviceTokenSecret = loginDeviceTokenSecret;
        LoginXboxTokenSecret = loginXboxTokenSecret;
        LoginUserTokenSessionKey = loginUserTokenSessionKey;
        LoginUserTokenSessionKeyBase64 = Convert.ToBase64String(ImmutableCollectionsMarshal.AsArray(loginUserTokenSessionKey)!);

        LiveAuthTokenSecret = liveAuthTokenSecret;
        LiveXapiTokenSecret = liveXapiTokenSecret;
        LivePlayfabTokenSecret = livePlayfabTokenSecret;

        PlayfabEntityTokenSecret = playfabEntityTokenSecret;
        PlayfabSessionTicketSecret = playfabSessionTicketSecret;
    }

    public CryptoSecrets(IReadOnlyDictionary<string, string> secrets)
    {
        LoginUserTokenSecret = GetSecret(LoginUserTokenName);
        LoginDeviceTokenSecret = GetSecret(LoginDeviceTokenName);
        LoginXboxTokenSecret = GetSecret(LoginXboxTokenName);
        LoginUserTokenSessionKey = GetSecret(LoginUserTokenSessionKeyName);
        LoginUserTokenSessionKeyBase64 = secrets[LoginUserTokenSessionKeyName];

        LiveAuthTokenSecret = GetSecret(LiveAuthTokenName);
        LiveXapiTokenSecret = GetSecret(LiveXapiTokenName);
        LivePlayfabTokenSecret = GetSecret(LivePlayfabTokenName);

        PlayfabEntityTokenSecret = GetSecret(PlayfabEntityTokenName);
        PlayfabSessionTicketSecret = GetSecret(PlayfabSessionTicketName);

        ImmutableArray<byte> GetSecret(string name)
        {
            ThrowHelper.ThrowIfNull(name, $"{nameof(secrets)}[{name}]");

            if (!secrets.TryGetValue(name, out var valueBase64))
            {
                ThrowHelper.ThrowArgumentException($"{nameof(secrets)} does not contain secret '{name}'.", nameof(secrets));
            }

            if (string.IsNullOrWhiteSpace(valueBase64))
            {
                ThrowHelper.ThrowArgumentException($"{nameof(secrets)}[{name}] cannot be empty.", $"{nameof(secrets)}[{name}]");
            }

            return ImmutableCollectionsMarshal.AsImmutableArray(Convert.FromBase64String(valueBase64));
        }
    }

    public ImmutableArray<byte> LoginUserTokenSecret { get; }

    public ImmutableArray<byte> LoginDeviceTokenSecret { get; }

    public ImmutableArray<byte> LoginXboxTokenSecret { get; }

    public ImmutableArray<byte> LoginUserTokenSessionKey { get; }
    public string LoginUserTokenSessionKeyBase64 { get; }

    public ImmutableArray<byte> LiveAuthTokenSecret { get; }

    public ImmutableArray<byte> LiveXapiTokenSecret { get; }

    public ImmutableArray<byte> LivePlayfabTokenSecret { get; }

    public ImmutableArray<byte> PlayfabEntityTokenSecret { get; }

    public ImmutableArray<byte> PlayfabSessionTicketSecret { get; }
}