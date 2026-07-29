using Solace.Db.Earth.Models.Global;
using Solace.Db.Earth.Models.Player;
using Solace.Db.Earth.Models.Player.Workshop;

namespace Solace.Db.Earth.Models;

public sealed class ProfileEF : IEntityWithId<Guid>
{
    public const string DefaultPictureUrl = "images/default_pfp.png";

    public required Guid Id { get; set; }

    public required long? WebPortalAccountId { get; set; }

    public required DateTimeOffset CreatedDate { get; set; }

    public required string? Username { get; set; }

    public required string? ProfilePictureUrl { get; set; }

    // [MaxLength(16 * 1024)]
    public byte[]? SkinImageData { get; set; } // .png

    public bool IsSkinSlim { get; set; }

    public int Health { get; set; } = 20;

    public int Experience { get; set; }

    public int Level { get; set; } = 1;

    public Rubies Rubies { get; set; } = new Rubies();

    public ProfileVersions? ProfileVersions { get; set; }

    public ICollection<ActivityLogEntryEF> ActivityLogs { get; set; } = [];

    public BoostsEF? Boosts { get; set; }

    public ICollection<PlayerBuildplateEF> Buildplates { get; set; } = [];

    public HotbarEF? Hotbar { get; set; }

    public ICollection<StackableItemEF> StackableItems { get; set; } = [];

    public ICollection<NonStackableItemInstanceEF> NonStackableItems { get; set; } = [];

    public ICollection<ItemJournalEntryEF> JournalEntries { get; set; } = [];

    public ICollection<RedeemedTappableEF> RedeemedTappables { get; set; } = [];

    public ICollection<TokenEF> Tokens { get; set; } = [];

    public CraftingSlotsEF? CraftingSlots { get; set; }

    public SmeltingSlotsEF? SmeltingSlots { get; set; }

    public ICollection<SharedBuildplateEF> SharedBuildplates { get; set; } = [];
}