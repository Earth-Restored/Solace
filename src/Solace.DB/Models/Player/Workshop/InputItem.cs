using Solace.DB.Models.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed record InputItem(
     string Id,
     int Count,
     NonStackableItemInstance[] Instances
)
{
     // efcore json needs this
     private InputItem()
          : this(default!, default!, default!)
     {
     }

     public sealed record Legacy(
          string Id,
          int Count,
          NonStackableItemInstance.Legacy[] Instances
     );
}
