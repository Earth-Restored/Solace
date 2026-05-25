using System.Buffers.Binary;

namespace Solace.Common.Utils;

public static class BinaryReaderExtensions
{
    extension(BinaryReader reader)
    {
        public uint ReadUInt32BE()
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            int read = reader.Read(buffer);
            if (read != sizeof(uint))
            {
                throw new EndOfStreamException($"{sizeof(uint)} bytes required from stream, but only {read} returned.");
            }

            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }
    }
}
