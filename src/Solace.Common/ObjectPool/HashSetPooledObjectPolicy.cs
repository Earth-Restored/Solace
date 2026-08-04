using Microsoft.Extensions.ObjectPool;

namespace Solace.Common.ObjectPool;

/// <summary>
/// A policy for pooling <see cref="HashSet{T}"/> instances.
/// </summary>
public sealed class HashSetPooledObjectPolicy<T> : PooledObjectPolicy<HashSet<T>>
{
    /// <summary>
    /// Gets or sets the initial capacity of pooled <see cref="HashSet{T}"/> instances.
    /// </summary>
    /// <value>Defaults to <c>16</c>.</value>
    public int InitialCapacity { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum value for <see cref="HashSet{T}.Capacity"/> that is allowed to be
    /// retained, when <see cref="Return(HashSet{T})"/> is invoked.
    /// </summary>
    /// <value>Defaults to <c>2048</c>.</value>
    public int MaximumRetainedCapacity { get; set; } = 2 * 1024;

    /// <inheritdoc />
    public override HashSet<T> Create()
        => [with(InitialCapacity)];

    /// <inheritdoc />
    public override bool Return(HashSet<T> obj)
    {
        if (obj.Capacity > MaximumRetainedCapacity)
        {
            // Too big. Discard this one.
            return false;
        }

        obj.Clear();
        return true;
    }
}