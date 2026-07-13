using System.Text.Json.Serialization;
using Solace.DB.Models.Common;
using Solace.DB.Models.Global;
using Solace.DB.Models.Player;
using Solace.DB.Models.Player.Workshop;

namespace Solace.DB;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(BoostsEF.ActiveBoost?[]))]
[JsonSerializable(typeof(CraftingSlotEF[]))]
[JsonSerializable(typeof(Dictionary<Guid, Dictionary<Guid, NonStackableItemInstance>>))]
[JsonSerializable(typeof(Dictionary<Guid, JournalEF.ItemJournalEntry>))]
[JsonSerializable(typeof(Dictionary<Guid, int>))]
[JsonSerializable(typeof(Dictionary<string, TokensEF.Token>))]
[JsonSerializable(typeof(HotbarEF.Item?[]))]
[JsonSerializable(typeof(List<ActivityLogEF.Entry>))]
[JsonSerializable(typeof(SharedBuildplateEF.HotbarItem?[]))]
[JsonSerializable(typeof(SmeltingSlot[]))]
internal sealed partial class DbJsonContext : JsonSerializerContext
{
}