namespace Solace.DB.Models.Player;

public sealed class ProfileEF : IEntityWithId<Guid>
{
    public Guid Id { get; set; }

    public Account Account { get; set; } = null!;

    public int Health { get; set; } = 20;

    public int Experience { get; set; }

    public int Level { get; set; } = 1;

    public Rubies Rubies { get; set; } = new Rubies();
}