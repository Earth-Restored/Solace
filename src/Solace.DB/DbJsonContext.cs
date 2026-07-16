using System.Text.Json.Serialization;
using Solace.DB.Models.Common;
using Solace.DB.Models.Player;
using Solace.DB.Models.Player.Workshop;

namespace Solace.DB;

[JsonSourceGenerationOptions]
[JsonSerializable(typeof(BoostsEF.ActiveBoost?[]))]
[JsonSerializable(typeof(CraftingSlotEF[]))]
[JsonSerializable(typeof(Dictionary<Guid, int>))]
[JsonSerializable(typeof(HotbarEF.Item?[]))]
[JsonSerializable(typeof(Rewards))]
[JsonSerializable(typeof(SmeltingSlotEF[]))]
internal sealed partial class DbJsonContext : JsonSerializerContext
{
}