using Solace.Common.Utils;

namespace Solace.Common.Test;

public sealed class RandomExtensionsTests
{
    [Test]
    public async Task NextSingle_ValidRange_ReturnsValueWithinRange()
    {
        var random = new Random(42);
        var min = 5.0f;
        var max = 10.0f;

        for (var i = 0; i < 100; i++)
        {
            var value = random.NextSingle(min, max);
            await Assert.That(value).IsGreaterThanOrEqualTo(min);
            await Assert.That(value).IsLessThanOrEqualTo(max);
        }
    }

    [Test]
    public async Task NextSingle_MinGreaterThanMax_ThrowsArgumentOutOfRangeException()
    {
        var random = new Random();
        Action action = () => random.NextSingle(10.0f, 5.0f);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
    }
}
