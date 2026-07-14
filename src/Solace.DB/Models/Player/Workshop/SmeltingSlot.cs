using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Solace.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed class SmeltingSlot : IEquatable<SmeltingSlot>
{
    public ActiveSmeltingJob? ActiveJob { get; set; }

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

    public sealed record ActiveSmeltingJob(
        string SessionId,
        Guid RecipeId,
        DateTimeOffset StartTime,
        InputItem Input,
        Fuel? AddedFuel,
        int TotalRounds,
        int CollectedRounds,
        bool FinishedEarly
    )
    {
        // efcore json needs this
        private ActiveSmeltingJob()
            : this(default!, default!, default!, default!, default!, default!, default!, default!)
        {
        }
    }

    public sealed record Fuel(
        InputItem Item,
        TimeSpan BurnDuration, // seconds
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
