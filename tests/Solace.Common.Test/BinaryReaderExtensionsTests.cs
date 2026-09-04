using Solace.Common.Utils;

namespace Solace.Common.Test;

public sealed class BinaryReaderExtensionsTests
{
    [Test]
    public async Task ReadUInt32BE_ValidStream_ReadsBigEndianValue()
    {
        byte[] data = [0x12, 0x34, 0x56, 0x78];
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        var value = reader.ReadUInt32BE();

        await Assert.That(value).IsEqualTo(0x12345678U);
    }

    [Test]
    public async Task ReadUInt32BE_InsufficientBytes_ThrowsEndOfStreamException()
    {
        byte[] data = [0x12, 0x34];
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        Action action = () => reader.ReadUInt32BE();

        await Assert.That(action).Throws<EndOfStreamException>();
    }
}
