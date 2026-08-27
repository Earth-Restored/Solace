using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Solace.Db.Migrator.Old.Earth.Models.Player;

public sealed class Rubies : IEquatable<Rubies>
{
    public Rubies()
    {
    }

    public Rubies(int purchased, int earned)
    {
        Purchased = purchased;
        Earned = earned;
    }

    public int Purchased { get; set; }

    public int Earned { get; set; }

    [JsonIgnore, NotMapped]
    public int Total => Purchased + Earned;

    public bool Equals(Rubies? other)
        => other is not null && Purchased == other.Purchased && Earned == other.Earned;

    public override bool Equals(object? obj)
        => Equals(obj as Rubies);

    public override int GetHashCode()
        => HashCode.Combine(Purchased, Earned);
}
