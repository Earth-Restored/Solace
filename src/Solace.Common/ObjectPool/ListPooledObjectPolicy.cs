using Microsoft.Extensions.ObjectPool;

namespace Solace.Common.ObjectPool;

/// <summary>
/// A policy for pooling <see cref="List{T}"/> instances.
/// </summary>
public sealed class ListPooledObjectPolicy<T> : PooledObjectPolicy<List<T>>
{
    /// <summary>
    /// Gets or sets the initial capacity of pooled <see cref="List{T}"/> instances.
    /// </summary>
    /// <value>Defaults to <c>16</c>.</value>
    public int InitialCapacity { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum value for <see cref="List{T}.Capacity"/> that is allowed to be
    /// retained, when <see cref="Return(List{T})"/> is invoked.
    /// </summary>
    /// <value>Defaults to <c>2048</c>.</value>
    public int MaximumRetainedCapacity { get; set; } = 2 * 1024;

    /// <inheritdoc />
    public override List<T> Create()
        => new List<T>(InitialCapacity);

    /// <inheritdoc />
    public override bool Return(List<T> obj)
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