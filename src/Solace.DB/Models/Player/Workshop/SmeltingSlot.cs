using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed class SmeltingSlot : IEquatable<SmeltingSlot>, ICloneable<SmeltingSlot>
{
    public ActiveJobR? ActiveJob { get; set; }

    public BurningR? Burning { get; set; }

    public bool Locked { get; set; }

    public SmeltingSlot()
    {
        ActiveJob = null;
        Burning = null;
        Locked = false;
    }

    public bool Equals(SmeltingSlot? other)
        => other is not null && ActiveJob == other.ActiveJob && Burning == other.Burning && Locked == other.Locked;

    public override bool Equals(object? obj)
        => Equals(obj as SmeltingSlot);

    public override int GetHashCode()
        => HashCode.Combine(ActiveJob, Burning, Locked);

    public SmeltingSlot DeepCopy()
        => new SmeltingSlot()
        {
            ActiveJob = ActiveJob?.DeepCopy(),
            Burning = Burning?.DeepCopy(),
            Locked = Locked,
        };

    public sealed class Comparer : IEqualityComparer<SmeltingSlot>
    {
        public static Comparer Instance { get; } = new Comparer();

        private Comparer()
        {
        }

        public bool Equals(SmeltingSlot? x, SmeltingSlot? y)
            => x == y || (x != null && y != null && (x.ActiveJob?.Equals(y.ActiveJob) ?? false) && (x.Burning?.Equals(y.Burning) ?? false) && x.Locked == y.Locked);

        public int GetHashCode([DisallowNull] SmeltingSlot obj)
            => HashCode.Combine(obj.ActiveJob, obj.Burning, obj.Locked);
    }

    public sealed record ActiveJobR(
        string SessionId,
        string RecipeId,
        long StartTime,
        InputItem Input,
        Fuel? AddedFuel,
        int TotalRounds,
        int CollectedRounds,
        bool FinishedEarly
    ) : ICloneable<ActiveJobR>
    {
        // efcore json needs this
        private ActiveJobR()
            : this(default!, default!, default!, default!, default!, default!, default!, default!)
        {
        }

        public ActiveJobR DeepCopy()
            => new ActiveJobR(SessionId, RecipeId, StartTime, Input.DeepCopy(), AddedFuel?.DeepCopy(), TotalRounds, CollectedRounds, FinishedEarly);
    }

    public sealed record Fuel(
        InputItem Item,
        int BurnDuration,
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

    public sealed class Legacy : IEquatable<Legacy>
    {
        public ActiveJobR? ActiveJob { get; set; }

        public BurningR? Burning { get; set; }

        public bool Locked { get; set; }

        public Legacy()
        {
            ActiveJob = null;
            Burning = null;
            Locked = false;
        }

        public bool Equals(Legacy? other)
            => other is not null && ActiveJob == other.ActiveJob && Burning == other.Burning && Locked == other.Locked;

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
            => HashCode.Combine(ActiveJob, Burning, Locked);

        public sealed record ActiveJobR(
            string SessionId,
            string RecipeId,
            long StartTime,
            InputItem.Legacy Input,
            Fuel? AddedFuel,
            int TotalRounds,
            int CollectedRounds,
            bool FinishedEarly
        )
        {
            // efcore json needs this
            private ActiveJobR()
                : this(default!, default!, default!, default!, default!, default!, default!, default!)
            {
            }
        }

        public sealed record Fuel(
            InputItem.Legacy Item,
            int BurnDuration,
            int HeatPerSecond
        )
        {
            private Fuel()
                : this(default!, default!, default!)
            {
            }
        }

        public sealed record BurningR(
            Fuel Fuel,
            int RemainingHeat
        )
        {
            private BurningR()
                : this(default!, default!)
            {
            }
        }
    }
}
