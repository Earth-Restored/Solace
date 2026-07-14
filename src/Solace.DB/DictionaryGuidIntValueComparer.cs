using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Solace.DB;

internal sealed class DictionaryGuidIntValueComparer : ValueComparer<Dictionary<Guid, int>>
{
    public DictionaryGuidIntValueComparer()
        : base(
            (d1, d2) => DictionariesEqual(d1, d2),
            d => ComputeHashCode(d),
            d => new Dictionary<Guid, int>(d.Select(item => new KeyValuePair<Guid, int>(item.Key, item.Value))))
    {
    }

    public static bool DictionariesEqual(Dictionary<Guid, int>? d1, Dictionary<Guid, int>? d2)
    {
        if (d1 == d2)
        {
            return true;
        }

        if (d1 == null || d2 == null)
        {
            return false;
        }

        if (d1.Count != d2.Count)
        {
            return false;
        }

        foreach (var kvp in d1)
        {
            if (!d2.TryGetValue(kvp.Key, out var value2))
            {
                return false;
            }

            if (kvp.Value != value2)
            {
                return false;
            }
        }

        return true;
    }

    public static int ComputeHashCode(Dictionary<Guid, int>? d)
    {
        if (d == null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var kvp in d.OrderBy(x => x.Key))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }

        return hash.ToHashCode();
    }
}
