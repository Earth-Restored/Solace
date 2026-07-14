using System.Text.Json.Serialization;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player;

public sealed class RedeemedTappableEF
{
    public required Guid AccountId { get; set; }

    public required Guid TappableId { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }

    public Account Account { get; set; } = null!;
}