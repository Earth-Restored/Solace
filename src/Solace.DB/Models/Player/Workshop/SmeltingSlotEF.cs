using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed class SmeltingSlotEF : IEquatable<SmeltingSlotEF>, ICloneable<SmeltingSlotEF>
{
    public ActiveSmeltingJob? ActiveJob { get; set; }

    public BurningR? Burning { get; set; }

    public bool Locked { get; set; }

    public SmeltingSlotEF()
    {
        ActiveJob = null;
        Burning = null;
        Locked = false;
    }

    public SmeltingSlotEF DeepCopy()
        => new SmeltingSlotEF()
        {
            ActiveJob = ActiveJob?.DeepCopy(),
            Burning = Burning?.DeepCopy(),
            Locked = Locked,
        };

    public bool Equals(SmeltingSlotEF? other)
        => other is not null && ActiveJob == other.ActiveJob && Burning == other.Burning && Locked == other.Locked;

    public override bool Equals(object? obj)
        => Equals(obj as SmeltingSlotEF);

    public override int GetHashCode()
        => HashCode.Combine(ActiveJob, Burning, Locked);

    public sealed class Comparer : IEqualityComparer<SmeltingSlotEF>
    {
        public static Comparer Instance { get; } = new Comparer();

        private Comparer()
        {
        }

        public bool Equals(SmeltingSlotEF? x, SmeltingSlotEF? y)
            => x?.Equals(y) ?? ReferenceEquals(x, y) || (x != null && y != null && (x.ActiveJob?.Equals(y.ActiveJob) ?? false) && (x.Burning?.Equals(y.Burning) ?? false) && x.Locked == y.Locked);

        public int GetHashCode([DisallowNull] SmeltingSlotEF obj)
            => HashCode.Combine(obj.ActiveJob, obj.Burning, obj.Locked);
    }

    public sealed record ActiveSmeltingJob(
        string SessionId,
        Guid RecipeId,
        DateTimeOffset StartTime,
        InputItem Input,
        Fuel? AddedFuel,
        int TotalRounds,
        int CollectedRounds,
        bool FinishedEarly
    ) : ICloneable<ActiveSmeltingJob>
    {
        // efcore json needs this
        private ActiveSmeltingJob()
            : this(default!, default!, default!, default!, default!, default!, default!, default!)
        {
        }

        public ActiveSmeltingJob DeepCopy()
            => new ActiveSmeltingJob(SessionId, RecipeId, StartTime, Input.DeepCopy(), AddedFuel?.DeepCopy(), TotalRounds, CollectedRounds, FinishedEarly);
    }

    public sealed record Fuel(
        InputItem Item,
        TimeSpan BurnDuration, // seconds
        int HeatPerSecond
    ) : ICloneable<Fuel>
    {
        private Fuel()
            : this(default!, default!, default!)
        {
        }

        public Fuel DeepCopy()
            => new Fuel(Item.DeepCopy(), BurnDuration, HeatPerSecond);
    }

    public sealed record BurningR(
        Fuel Fuel,
        int RemainingHeat
    ) : ICloneable<BurningR>
    {
        private BurningR()
            : this(default!, default!)
        {
        }

        public BurningR DeepCopy()
            => new BurningR(Fuel.DeepCopy(), RemainingHeat);
    }
}
