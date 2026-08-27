using System.Collections.Immutable;
using System.Runtime.InteropServices;
using BitcoderCZ.Utils;
using Serilog;

namespace Solace.ApiServer;

internal sealed class CryptoSecrets
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

    private readonly ImmutableArray<byte> _loginUserTokenSecret;
    private readonly ImmutableArray<byte> _loginDeviceTokenSecret;
    private readonly ImmutableArray<byte> _loginXboxTokenSecret;
    private readonly ImmutableArray<byte> _loginUserTokenSessionKey;
    private readonly string _loginUserTokenSessionKeyBase64;

    private readonly ImmutableArray<byte> _liveAuthTokenSecret;
    private readonly ImmutableArray<byte> _liveXapiTokenSecret;
    private readonly ImmutableArray<byte> _livePlayfabTokenSecret;

    private readonly ImmutableArray<byte> _playfabEntityTokenSecret;
    private readonly ImmutableArray<byte> _playfabSessionTicketSecret;

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

        _loginUserTokenSecret = loginUserTokenSecret;
        _loginDeviceTokenSecret = loginDeviceTokenSecret;
        _loginXboxTokenSecret = loginXboxTokenSecret;
        _loginUserTokenSessionKey = loginUserTokenSessionKey;
        _loginUserTokenSessionKeyBase64 = Convert.ToBase64String(ImmutableCollectionsMarshal.AsArray(loginUserTokenSessionKey)!);

        _liveAuthTokenSecret = liveAuthTokenSecret;
        _liveXapiTokenSecret = liveXapiTokenSecret;
        _livePlayfabTokenSecret = livePlayfabTokenSecret;

        _playfabEntityTokenSecret = playfabEntityTokenSecret;
        _playfabSessionTicketSecret = playfabSessionTicketSecret;
    }

    public CryptoSecrets(IReadOnlyDictionary<string, string> secrets)
    {
        _loginUserTokenSecret = GetSecret(LoginUserTokenName);
        _loginDeviceTokenSecret = GetSecret(LoginDeviceTokenName);
        _loginXboxTokenSecret = GetSecret(LoginXboxTokenName);
        _loginUserTokenSessionKey = GetSecret(LoginUserTokenSessionKeyName);
        _loginUserTokenSessionKeyBase64 = secrets[LoginUserTokenSessionKeyName];

        _liveAuthTokenSecret = GetSecret(LiveAuthTokenName);
        _liveXapiTokenSecret = GetSecret(LiveXapiTokenName);
        _livePlayfabTokenSecret = GetSecret(LivePlayfabTokenName);

        _playfabEntityTokenSecret = GetSecret(PlayfabEntityTokenName);
        _playfabSessionTicketSecret = GetSecret(PlayfabSessionTicketName);

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

    public ImmutableArray<byte> LoginUserTokenSecret => _loginUserTokenSecret;

    public ImmutableArray<byte> LoginDeviceTokenSecret => _loginDeviceTokenSecret;

    public ImmutableArray<byte> LoginXboxTokenSecret => _loginXboxTokenSecret;

    public ImmutableArray<byte> LoginUserTokenSessionKey => _loginUserTokenSessionKey;
    public string LoginUserTokenSessionKeyBase64 => _loginUserTokenSessionKeyBase64;

    public ImmutableArray<byte> LiveAuthTokenSecret => _liveAuthTokenSecret;

    public ImmutableArray<byte> LiveXapiTokenSecret => _liveXapiTokenSecret;

    public ImmutableArray<byte> LivePlayfabTokenSecret => _livePlayfabTokenSecret;

    public ImmutableArray<byte> PlayfabEntityTokenSecret => _playfabEntityTokenSecret;

    public ImmutableArray<byte> PlayfabSessionTicketSecret => _playfabSessionTicketSecret;
}