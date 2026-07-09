using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Solace.Common;
using Solace.Common.Utils;

namespace Solace.DB.Models.Player;

public sealed class BoostsEF : IEntityWithId<Guid>, IVersionedEntity, IMergeable<BoostsEF>
{
    public Guid Id { get; set; }

    public int Version { get; set; } = 1;

    public Account Account { get; set; } = null!;

    public ActiveBoost?[] ActiveBoosts { get; set; } = new ActiveBoost[5];

    public ActiveBoost? Get(Guid instanceId)
        => ActiveBoosts.FirstOrDefault(activeBoost => activeBoost is not null && activeBoost.InstanceId == instanceId);

    public IEnumerable<ActiveBoost> Prune(DateTimeOffset currentTime)
    {
        for (int index = 0; index < ActiveBoosts.Length; index++)
        {
            ActiveBoost? activeBoost = ActiveBoosts[index];
            if (activeBoost is not null && activeBoost.StartTimeDT + activeBoost.DurationTS < currentTime)
            {
                ActiveBoosts[index] = null;
                yield return activeBoost;
            }
        }
    }

    public async Task MergeWith(BoostsEF other, ValueMerger merger)
    {
        merger.CurrentUserId = Id.ToString();
        merger.CurrentUsername = Account?.Username;

        for (var i = 0; i < other.ActiveBoosts.Length; i++)
        {
            if (ActiveBoosts[i] is null)
            {
                ActiveBoosts[i] = other.ActiveBoosts[i];
            }
            else if (other.ActiveBoosts[i] is not null)
            {
                ActiveBoosts[i] = await merger.AutoMerge(ActiveBoosts[i]!, other.ActiveBoosts[i]!, $"Boost slot {i + 1}", null);
            }
        }
    }

    public sealed record ActiveBoost(
        Guid InstanceId,
        Guid ItemId,
        long StartTime,
        long Duration
    ) : ICloneable<ActiveBoost>
    {
        [JsonIgnore, NotMapped] public DateTimeOffset StartTimeDT => DateTimeOffset.FromUnixTimeMilliseconds(StartTime);

        [JsonIgnore, NotMapped] public TimeSpan DurationTS => TimeSpan.FromMilliseconds(Duration);

        public ActiveBoost DeepCopy()
            => new ActiveBoost(this);

        public sealed class Comparer : IEqualityComparer<ActiveBoost>
        {
            public static Comparer Instance { get; } = new Comparer();

            private Comparer()
            {
            }

            public bool Equals(ActiveBoost? x, ActiveBoost? y)
                => x == y || (x?.Equals(y) ?? false);

            public int GetHashCode([DisallowNull] ActiveBoost obj)
                => obj.GetHashCode();
        }
    }

    public sealed class Legacy : IEquatable<Legacy>
    {
        public ActiveBoost?[] ActiveBoosts { get; init; }

        public Legacy()
        {
            ActiveBoosts = new ActiveBoost[5];
        }

        public bool Equals(Legacy? other)
            => other is not null && ActiveBoosts.SequenceEqual(other.ActiveBoosts);

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in ActiveBoosts)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }

        public sealed record ActiveBoost(
            Guid InstanceId,
            Guid ItemId,
            long StartTime,
            long Duration
        );
    }
}
