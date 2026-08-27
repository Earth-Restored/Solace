using Solace.Common.Utils;

namespace Solace.DB.Models.Global;

public sealed class Secret : IEntityWithId<string>, IMergeable<Secret>
{
    public required string Id { get; set; }

    public required string Value { get; set; }

    public async Task MergeWith(Secret other, ValueMerger merger)
    {
        merger.CurrentUserId = null;
        merger.CurrentUsername = null;

        if (Value != other.Value)
        {
            Value = other.Value;
        }
    }
}