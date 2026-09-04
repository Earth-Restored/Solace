namespace Solace.Common.Test;

public sealed class MiniFigIdTranslatorTests
{
    [Test]
    public async Task ToGuid_ValidHexFullLength_ReturnsExpectedGuid()
    {
        var input = "0123456789abcdef0123456789abcdef";
        var expected = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

        var result = MiniFigIdTranslator.ToGuid(input);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ToGuid_ValidHexShort_PadsWithZeros()
    {
        var input = "1";
        var expected = Guid.Parse("00000000-0000-0000-0000-000000000001");

        var result = MiniFigIdTranslator.ToGuid(input);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ToGuid_EmptyString_ThrowsArgumentOutOfRangeException()
    {
        Action action = () => MiniFigIdTranslator.ToGuid("");

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ToGuid_LengthGreaterThan32_ThrowsArgumentOutOfRangeException()
    {
        var input = new string('a', 33);
        Action action = () => MiniFigIdTranslator.ToGuid(input);

        await Assert.That(action).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ToGuid_InvalidHexCharacters_ThrowsArgumentException()
    {
        var input = "0123456789g000000000000000000000";
        Action action = () => MiniFigIdTranslator.ToGuid(input);

        await Assert.That(action).Throws<ArgumentException>();
    }
}
