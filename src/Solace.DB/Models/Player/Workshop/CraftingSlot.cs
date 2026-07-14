using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Solace.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed class CraftingSlotEF
{
    public ActiveCraftingJob? ActiveJob { get; set; }
    public bool Locked { get; set; }

    public sealed record InputRow(InputItem[] Items)
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
    }

    public sealed record ActiveCraftingJob(
        string SessionId,
        Guid RecipeId,
        DateTimeOffset StartTime,
        InputRow[] Input,
        int TotalRounds,
        int CollectedRounds,
        bool FinishedEarly
    )
    {
        // efcore json needs this
        private ActiveCraftingJob()
            : this(default!, default!, default!, default!, default!, default!, default!)
        {
        }

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
    }
}
