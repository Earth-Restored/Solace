using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Solace.Db;

public sealed class StringStringDictionaryValueComparer : ValueComparer<Dictionary<string, string>>
{
    public StringStringDictionaryValueComparer()
        : base(
            (a1, a2) => a1 == a2 || (a1 != null && a2 != null && a1.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new ValueTuple<string, string>(item.Key, item.Value)).SequenceEqual(a2.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new ValueTuple<string, string>(item.Key, item.Value)))),
            a => a != null ? a.OrderBy(item => item.Key, StringComparer.Ordinal).Aggregate(0, (h, v) => HashCode.Combine(h, v.Key, v.Value)) : 0,
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            a => a != null ? a.ToDictionary() : new Dictionary<string, string>(StringComparer.Ordinal))
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    {
    }
}
