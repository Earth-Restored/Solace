using Solace.WebPortal.Common.Features.Players;

namespace Solace.WebPortal.Client.Features.Players;

public sealed class AccountViewModel
{
    public Guid Id { get; set; }

    public string? Username { get; set; }

    public int Health { get; set; }

    public int MaxHealth { get; set; }

    public int Level { get; set; }

    public int TotalExperience { get; set; }

    public float? LevelProgressPercentage { get; set; } // 0 to 1; null - max level already

    public int EarnedRubies { get; set; }

    public int PurchasedRubies { get; set; }

    public int TotalRubies => EarnedRubies + PurchasedRubies;

    public static AccountViewModel FromDto(PlayerDto player)
         => new()
         {
             Id = player.Id,
             Username = player.Username,
             Health = player.Health,
             MaxHealth = player.MaxHealth,
             Level = player.Level,
             TotalExperience = player.TotalExperience,
             LevelProgressPercentage = player.LevelProgressPercentage,
             PurchasedRubies = player.PurchasedRubies,
             EarnedRubies = player.EarnedRubies,
         };
}