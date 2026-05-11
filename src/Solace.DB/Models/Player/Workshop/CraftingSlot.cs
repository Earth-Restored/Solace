namespace Solace.DB.Models.Player.Workshop;

public sealed class CraftingSlotEF
{
    public ActiveJobR? ActiveJob { get; set; }
    public bool Locked { get; set; }

    public sealed record InputRow(InputItem[] Items)
    {
        // efcore json needs this
        public InputRow()
            : this((InputItem[])default!)
        {
        }
    }

    public sealed record ActiveJobR(
        string SessionId,
        string RecipeId,
        long StartTime,
        InputRow[] Input,
        int TotalRounds,
        int CollectedRounds,
        bool FinishedEarly
    )
    {
        // efcore json needs this
        private ActiveJobR()
            : this(default!, default!, default!, default!, default!, default!, default!)
        {
        }
    }

    public sealed class Legacy : IEquatable<Legacy>
    {
        public ActiveJobR? ActiveJob { get; set; }
        public bool Locked { get; set; }

        public Legacy()
        {
            ActiveJob = null;
            Locked = false;
        }

        public bool Equals(Legacy? other)
            => other is not null && ActiveJob == other.ActiveJob && Locked == other.Locked;

        public override bool Equals(object? obj)
            => Equals(obj as Legacy);

        public override int GetHashCode()
            => HashCode.Combine(ActiveJob, Locked);

        public sealed record ActiveJobR(
            string SessionId,
            string RecipeId,
            long StartTime,
            InputItem.Legacy[][] Input,
            int TotalRounds,
            int CollectedRounds,
            bool FinishedEarly
        )
        {
            // efcore json needs this
            private ActiveJobR()
                : this(default!, default!, default!, default!, default!, default!, default!)
            {
            }
        }
    }
}