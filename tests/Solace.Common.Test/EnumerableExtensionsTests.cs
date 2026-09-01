using Solace.Common.Utils;

namespace Solace.Common.Test;

public sealed class EnumerableExtensionsTests
{
    [Test]
    public async Task WhereNotNull_FiltersNullNullableStructs()
    {
        int?[] input = [1, null, 3, null, 5];

        var result = input.WhereNotNull();

        await Assert.That(result).IsEquivalentTo([1, 3, 5]);
    }
}
