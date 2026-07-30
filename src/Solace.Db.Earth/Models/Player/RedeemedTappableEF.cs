namespace Solace.Db.Earth.Models.Player;

public sealed class RedeemedTappableEF
{
    public required Guid ProfileId { get; set; }

    public required Guid TappableId { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }

    public ProfileEF Profile { get; set; } = null!;
}