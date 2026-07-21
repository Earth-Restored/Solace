using Solace.DB.Earth.Models.Global;
using Solace.DB.Earth.Models.Player;
using Solace.DB.Earth.Models.Player.Workshop;

namespace Solace.DB.Earth.Models;

public sealed class Account : IEntityWithId<Guid>
{
    public const string DefaultPictureUrl = "images/default_pfp.png";

    public required Guid Id { get; set; }

    public required DateTimeOffset CreatedDate { get; set; }

    public required string? Username { get; set; }

    public required string? ProfilePictureUrl { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    // [MaxLength(16)]
    public required byte[] PasswordSalt { get; set; }

    // [MaxLength(64)]
    public required byte[] PasswordHash { get; set; }

    // [MaxLength(16 * 1024)]
    public byte[]? SkinImageData { get; set; } // .png

    public bool IsSkinSlim { get; set; }

    public AccountVersions? AccountVersions { get; set; }

    public ProfileEF? Profile { get; set; }

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