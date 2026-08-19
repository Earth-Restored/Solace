using Solace.Common;

namespace Solace.Db.Earth.Models.Common;

public sealed record Rewards(
    int Rubies,
    int ExperiencePoints,
    int? Level,
    Dictionary<Guid, int> Items,
    Guid[] Buildplates,
    string[] Challenges
) : ICloneable<Rewards>
{
    public Rewards() : this(0, 0, null, [], [], [])
    {
    }

    public Rewards DeepCopy()
        => new(Rubies, ExperiencePoints, Level, [with(Items)], [.. Buildplates], [.. Challenges]);

    public bool Equals(Rewards? other)
        => other is not null && Rubies == other.Rubies && ExperiencePoints == other.ExperiencePoints && Level == other.Level && Items.OrderBy(item => item.Key).Select(item => (item.Key, item.Value)).SequenceEqual(other.Items.OrderBy(item => item.Key).Select(item => (item.Key, item.Value))) && Buildplates.SequenceEqual(other.Buildplates) && Challenges.SequenceEqual(other.Challenges);

    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(Rubies);
        hash.Add(ExperiencePoints);
        hash.Add(Level);

        foreach (var item in Items.OrderBy(item => item.Key))
        {
            hash.Add(item.Key);
            hash.Add(item.Value);
        }

        foreach (var item in Buildplates)
        {
            hash.Add(item);
        }

        foreach (var item in Challenges)
        {
            hash.Add(item, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
