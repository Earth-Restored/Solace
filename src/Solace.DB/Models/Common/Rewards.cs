using Solace.Common;

namespace Solace.DB.Models.Common;

// todo: implement gethashcode and equals
public sealed record Rewards(
    int Rubies,
    int ExperiencePoints,
    int? Level,
    Dictionary<string, int> Items,
    string[] Buildplates,
    string[] Challenges
) : ICloneable<Rewards>
{
    public Rewards DeepCopy()
        => new Rewards(Rubies, ExperiencePoints, Level, new Dictionary<string, int>(Items), [.. Buildplates], [.. Challenges]);
}
