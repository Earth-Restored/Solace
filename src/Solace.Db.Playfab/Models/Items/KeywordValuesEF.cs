using Solace.Common;

namespace Solace.Db.Playfab.Models.Items;

public sealed class KeywordValuesEF : IEquatable<KeywordValuesEF>, ICloneable<KeywordValuesEF>
{
    public List<string> Values { get; set; } = [];

    public KeywordValuesEF DeepCopy()
        => new()
        {
            Values = [.. Values],
        };

    public bool Equals(KeywordValuesEF? other)
        => other is not null && Values.SequenceEqual(other.Values, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Values)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public override bool Equals(object? obj)
        => Equals(obj as KeywordValuesEF);
}
