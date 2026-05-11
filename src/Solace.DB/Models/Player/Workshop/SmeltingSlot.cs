namespace Solace.DB.Models.Player.Workshop;

public sealed class SmeltingSlot : IEquatable<SmeltingSlot>
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

    public sealed record ActiveJobR(
        string SessionId,
        string RecipeId,
        long StartTime,
        InputItem Input,
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
        InputItem Item,
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
