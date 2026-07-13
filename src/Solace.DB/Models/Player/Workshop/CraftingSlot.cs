using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solace.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed class CraftingSlotEF : ICloneable<CraftingSlotEF>
{
    public ActiveCraftingJob? ActiveJob { get; set; }
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

    public sealed record ActiveCraftingJob(
        string SessionId,
        Guid RecipeId,
        long StartTime,
        InputRow[] Input,
        int TotalRounds,
        int CollectedRounds,
        bool FinishedEarly
    ) : ICloneable<ActiveCraftingJob>
    {
        // efcore json needs this
        private ActiveCraftingJob()
            : this(default!, default!, default!, default!, default!, default!, default!)
        {
        }

        [JsonIgnore, NotMapped] public DateTimeOffset StartTimeDT => DateTimeOffset.FromUnixTimeMilliseconds(StartTime);

        public bool Equals(ActiveCraftingJob? other)
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

        public ActiveCraftingJob DeepCopy()
            => new ActiveCraftingJob(SessionId, RecipeId, StartTime, [.. Input.Select(item => item.DeepCopy())], TotalRounds, CollectedRounds, FinishedEarly);
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
            Guid RecipeId,
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

#region Converter
public sealed class CraftingSlotValueConverter : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<CraftingSlotEF[], string>
{
    public CraftingSlotValueConverter() : base(
        v => JsonSerializer.Serialize(v, DbJsonContext.Default.CraftingSlotEFArray),
        v => JsonSerializer.Deserialize(v, DbJsonContext.Default.CraftingSlotEFArray) ?? new CraftingSlotEF[3] { new CraftingSlotEF(), new CraftingSlotEF(), new CraftingSlotEF() })
    {
    }
}

public sealed class CraftingSlotArrayValueComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<CraftingSlotEF[]>
{
    public CraftingSlotArrayValueComparer() : base(
        (a, b) => CompareArrays(a, b),
        a => GetArrayHashCode(a),
        a => SnapshotArray(a))
    {
    }

    public static bool CompareArrays(CraftingSlotEF[]? a, CraftingSlotEF[]? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (!CraftingSlotEF.Comparer.Instance.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static int GetArrayHashCode(CraftingSlotEF[] a)
    {
        var hash = new HashCode();
        foreach (var item in a)
        {
            hash.Add(item is not null ? CraftingSlotEF.Comparer.Instance.GetHashCode(item) : 0);
        }

        return hash.ToHashCode();
    }

    public static CraftingSlotEF[] SnapshotArray(CraftingSlotEF[] a)
    {
        var clone = new CraftingSlotEF[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            clone[i] = a[i].DeepCopy();
        }

        return clone;
    }
}
#endregion
