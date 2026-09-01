using Solace.Common.Asp.Auth;

namespace Solace.Common.Asp.Tests;

public sealed class ValidityDatePairTests
{
    [Test]
    public async Task Constructor_RoundsToNearestSecond()
    {
        var issued = new DateTimeOffset(2026, 9, 1, 12, 0, 0, 500, TimeSpan.Zero);
        var expires = new DateTimeOffset(2026, 9, 1, 13, 0, 0, 800, TimeSpan.Zero);

        var pair = new ValidityDatePair(issued, expires);

        var expectedIssued = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var expectedExpires = new DateTimeOffset(2026, 9, 1, 13, 0, 0, TimeSpan.Zero);

        await Assert.That(pair.Issued).IsEqualTo(expectedIssued);
        await Assert.That(pair.Expires).IsEqualTo(expectedExpires);
        await Assert.That(pair.IssuedStr).IsEqualTo("2026-09-01T12:00:00Z");
        await Assert.That(pair.ExpiresStr).IsEqualTo("2026-09-01T13:00:00Z");
    }

    [Test]
    public async Task Constructor_IssuedAfterExpires_ThrowsArgumentOutOfRangeException()
    {
        var issued = DateTimeOffset.UtcNow.AddHours(1);
        var expires = DateTimeOffset.UtcNow;

        Action action = () => _ = new ValidityDatePair(issued, expires);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Create_WithMinutes_CreatesPairWithDuration()
    {
        var pair = ValidityDatePair.Create(30);

        var diff = pair.Expires - pair.Issued;
        await Assert.That(diff.TotalMinutes).IsEqualTo(30);
    }

    [Test]
    public async Task Create_WithTimeSpan_CreatesPairWithDuration()
    {
        var pair = ValidityDatePair.Create(TimeSpan.FromMinutes(30));

        var diff = pair.Expires - pair.Issued;
        await Assert.That(diff.TotalMinutes).IsEqualTo(30);
    }
}
