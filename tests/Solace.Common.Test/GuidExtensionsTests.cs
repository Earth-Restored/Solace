using Solace.Common.Utils;

namespace Solace.Common.Test;

public sealed class GuidExtensionsTests
{
    [Test]
    public async Task RoundTrip_LowHigh_RestoresOriginalGuid()
    {
        var originalGuid = Guid.NewGuid();

        var (low, high) = originalGuid.ToLowHigh();
        var restoredGuid = GuidExtensions.FromLowHigh(low, high);

        await Assert.That(restoredGuid).IsEqualTo(originalGuid);
    }

    [Test]
    public async Task IsNullOrZero_WithNull_ReturnsTrue()
    {
        Guid? guid = null;
        await Assert.That(GuidExtensions.IsNullOrZero(guid)).IsTrue();
    }

    [Test]
    public async Task IsNullOrZero_WithEmpty_ReturnsTrue()
    {
        Guid? guid = Guid.Empty;
        await Assert.That(GuidExtensions.IsNullOrZero(guid)).IsTrue();
    }

    [Test]
    public async Task IsNullOrZero_WithValidGuid_ReturnsFalse()
    {
        Guid? guid = Guid.NewGuid();
        await Assert.That(GuidExtensions.IsNullOrZero(guid)).IsFalse();
    }
}
