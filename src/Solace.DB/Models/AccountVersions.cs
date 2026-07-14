namespace Solace.DB.Models;

public sealed class AccountVersions : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Account Account { get; set; } = null!;

    public int Profile { get; set; }

    public int Inventory { get; set; }

    public int Crafting { get; set; }

    public int Smelting { get; set; }

    public int Boosts { get; set; }

    public int Buildplates { get; set; }

    public int Journal { get; set; }

    public int Challenges { get; set; }

    public int Tokens { get; set; }
}