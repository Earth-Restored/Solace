using Solace.Common;
using Solace.DB.Models.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed record InputItem(
     Guid Id,
     int Count,
     NonStackableItemInstanceEF[] Instances
)
{
     // efcore json needs this
     private InputItem()
          : this(default!, default!, default!)
     {
     }

     public bool Equals(InputItem? other)
          => other is not null && Id == other.Id && Count == other.Count && Instances.SequenceEqual(other.Instances);

     public override int GetHashCode()
     {
          var hash = new HashCode();
          hash.Add(Id);
          hash.Add(Count);
          foreach (var item in Instances)
          {
               hash.Add(item);
          }

          return hash.ToHashCode();
     }
}
