using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Solace.Db;

public sealed class ListImmutableValueComparer<T> : ValueComparer<List<T>>
    where T : class, IEquatable<T>
{
    public ListImmutableValueComparer()
        : base(
            (a1, a2) => a1 == a2 || (a1 != null && a2 != null && a1.SequenceEqual(a2)),
            a => a != null ? a.Aggregate(0, (h, v) => HashCode.Combine(h, v)) : 0,
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            a => a != null ? a.ToList() : new List<T>())
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    {
    }
}
