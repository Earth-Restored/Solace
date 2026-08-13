using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Solace.Db;

public sealed class StringDictionaryValueComparer<TValue> : ValueComparer<Dictionary<string, TValue>>
    where TValue : class, IEquatable<TValue>
{
    public StringDictionaryValueComparer(Func<TValue, TValue> deepCopy)
        : base(
            (a1, a2) => a1 == a2 || (a1 != null && a2 != null && a1.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new ValueTuple<string, TValue>(item.Key, item.Value)).SequenceEqual(a2.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => new ValueTuple<string, TValue>(item.Key, item.Value)))),
            a => a != null ? a.OrderBy(item => item.Key, StringComparer.Ordinal).Aggregate(0, (h, v) => HashCode.Combine(h, v.Key, v.Value)) : 0,
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            a => a != null ? a.Select(item => item.Value == null ? item : new KeyValuePair<string, TValue>(item.Key, deepCopy(item.Value))).ToDictionary() : new Dictionary<string, TValue>(StringComparer.Ordinal))
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    {
    }
}
