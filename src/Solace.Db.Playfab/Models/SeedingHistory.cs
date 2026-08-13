namespace Solace.Db.Playfab.Models;

public sealed class SeedingHistory
{
    public string Key { get; set; } = "PlayfabData";

    public required DateTimeOffset? SeededAt { get; set; }

    public required Version Version { get; set; }
}
