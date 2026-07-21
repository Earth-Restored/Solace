using System.Text.Json.Serialization;
using Solace.DB.Earth.Models.Common;
using Solace.DB.Earth.Models.Player;
using Solace.DB.Earth.Models.Player.Workshop;

namespace Solace.DB.Earth;

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