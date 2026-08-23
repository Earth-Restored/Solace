using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.Db.Earth.Models.Player;

public sealed class BoostsEF : IEntityWithId<Guid>
{
    public required Guid Id { get; set; }

    public ProfileEF Profile { get; set; } = null!;

    public ActiveBoost?[] ActiveBoosts { get; set; } = new ActiveBoost[5];

    public ActiveBoost? Get(Guid instanceId)
        => ActiveBoosts.FirstOrDefault(activeBoost => activeBoost is not null && activeBoost.InstanceId == instanceId);

    public IEnumerable<ActiveBoost> Prune(DateTimeOffset currentTime)
    {
        for (var index = 0; index < ActiveBoosts.Length; index++)
        {
            var activeBoost = ActiveBoosts[index];
            if (activeBoost is not null && activeBoost.StartTime + activeBoost.Duration < currentTime)
            {
                ActiveBoosts[index] = null;
                yield return activeBoost;
            }
        }
    }

    public sealed record ActiveBoost(
        Guid InstanceId,
        Guid ItemId,
        DateTimeOffset StartTime,
        TimeSpan Duration
    ) : ICloneable<ActiveBoost>
    {
        public ActiveBoost DeepCopy()
            => new(this);
    }
}
