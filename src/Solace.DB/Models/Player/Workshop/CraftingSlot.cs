using System.Diagnostics.CodeAnalysis;
using Solace.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed class CraftingSlotEF : ICloneable<CraftingSlotEF>
{
    public ActiveJobR? ActiveJob { get; set; }
    public bool Locked { get; set; }

    public CraftingSlotEF DeepCopy()
        => new CraftingSlotEF()
        {
            ActiveJob = ActiveJob?.DeepCopy(),
            Locked = Locked,
        };

    public sealed class Comparer : IEqualityComparer<CraftingSlotEF>
    {
        public static Comparer Instance { get; } = new Comparer();

        private Comparer()
        {
        }

        public bool Equals(CraftingSlotEF? x, CraftingSlotEF? y)
            => x == y || (x != null && y != null && (x.ActiveJob?.Equals(y.ActiveJob) ?? false) && x.Locked == y.Locked);

        public int GetHashCode([DisallowNull] CraftingSlotEF obj)
            => HashCode.Combine(obj.ActiveJob, obj.Locked);
    }

    public sealed record InputRow(InputItem[] Items)
        : ICloneable<InputRow>
    {
        // efcore json needs this
        public InputRow()
            : this((InputItem[])default!)
        {
        }

        public bool Equals(InputRow? other)
            => other is not null && Items.SequenceEqual(other.Items);

        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var item in Items)
            {
                hash.Add(item);
            }

            return hash.ToHashCode();
        }

        public InputRow DeepCopy()
            => new InputRow([.. Items.Select(item => item.DeepCopy())]);
    }

    public sealed record ActiveJobR(
        string SessionId,
        string RecipeId,
        long StartTime,
        InputRow[] Input,
        int TotalRounds,
        int CollectedRounds,
        bool FinishedEarly
    ) : ICloneable<ActiveJobR>
    {
        // efcore json needs this
        private ActiveJobR()
            : this(default!, default!, default!, default!, default!, default!, default!)
        {
        }

        public bool Equals(ActiveJobR? other)
             => other is not null && SessionId == other.SessionId && RecipeId == other.RecipeId && StartTime == other.StartTime && Input.SequenceEqual(other.Input) && TotalRounds == other.TotalRounds && CollectedRounds == other.CollectedRounds && FinishedEarly == other.FinishedEarly;

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SessionId);
            hash.Add(RecipeId);
            hash.Add(StartTime);
            foreach (var item in Input)
            {
                hash.Add(item);
            }

            hash.Add(TotalRounds);
            hash.Add(CollectedRounds);
            hash.Add(FinishedEarly);

            return hash.ToHashCode();
        }

        public ActiveJobR DeepCopy()
            => new ActiveJobR(SessionId, RecipeId, StartTime, [.. Input.Select(item => item.DeepCopy())], TotalRounds, CollectedRounds, FinishedEarly);
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