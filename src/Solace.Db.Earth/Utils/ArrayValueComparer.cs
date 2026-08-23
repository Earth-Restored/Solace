using Microsoft.EntityFrameworkCore.ChangeTracking;
using Solace.Common;

namespace Solace.Db.Earth.Utils;

internal sealed class ArrayValueComparer<T> : ValueComparer<T[]>
    where T : class, IEquatable<T>, ICloneable<T>
{
    public ArrayValueComparer()
        : base(
            (a1, a2) => a1 == a2 || (a1 != null && a2 != null && a1.SequenceEqual(a2)),
            a => a != null ? a.Aggregate(0, (h, v) => HashCode.Combine(h, v)) : 0,
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            a => a != null ? a.Select(item => item == null ? null : item.DeepCopy()).ToArray() : Array.Empty<T>())
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    {
    }
}