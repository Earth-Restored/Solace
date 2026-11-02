namespace Solace.Db.Earth.Models;

public sealed class SeedingHistory
{
    public string Key { get; set; } = "SeasonsData";

    public required DateTimeOffset SeededAt { get; set; }

    public required Version Version { get; set; }
}
