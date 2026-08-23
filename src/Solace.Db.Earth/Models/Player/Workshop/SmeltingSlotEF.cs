using Solace.Common;

namespace Solace.Db.Earth.Models.Player.Workshop;

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
        => new()
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
            => new(SessionId, RecipeId, StartTime, Input.DeepCopy(), AddedFuel?.DeepCopy(), TotalRounds, CollectedRounds, FinishedEarly);
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
            => new(Item.DeepCopy(), BurnDuration, HeatPerSecond);
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
            => new(Fuel.DeepCopy(), RemainingHeat);
    }
}
