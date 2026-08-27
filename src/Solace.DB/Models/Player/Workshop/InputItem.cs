using Solace.Common;
using Solace.DB.Models.Common;

namespace Solace.DB.Models.Player.Workshop;

public sealed record InputItem(
     string Id,
     int Count,
     NonStackableItemInstance[] Instances
) : ICloneable<InputItem>
{
     // efcore json needs this
     private InputItem()
          : this(default!, default!, default!)
     {
     }

     public InputItem DeepCopy()
          => new InputItem(Id, Count, [.. Instances.Select(item => item.DeepCopy())]);

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

    public sealed record Legacy(
          string Id,
          int Count,
          NonStackableItemInstance.Legacy[] Instances
     );
}
