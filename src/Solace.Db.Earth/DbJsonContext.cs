using System.Text.Json.Serialization;
using Solace.Db.Earth.Models.Common;
using Solace.Db.Earth.Models.Player;
using Solace.Db.Earth.Models.Player.Workshop;

namespace Solace.Db.Earth;

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