using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace Solace.Common.Utils;

#pragma warning disable CA1708 // Identifiers should differ by more than case - https://github.com/dotnet/sdk/issues/51716
public static class GuidExtensions
#pragma warning restore CA1708 // Identifiers should differ by more than case
{
    extension(Guid)
    {
        public static Guid FromLowHigh(ulong low, ulong high)
        {
            Span<byte> bytes = stackalloc byte[16];
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, low);
            BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], high);

            return new Guid(bytes, false);
        }

        public static bool IsNullOrZero([NotNullWhen(false)] Guid? value)
            => value is null || value.Value == Guid.Empty;
    }

    extension(Guid guid)
    {
        public (ulong Low, ulong High) ToLowHigh()
        {
            Span<byte> bytes = stackalloc byte[16];
            _ = guid.TryWriteBytes(bytes, false, out _);

            return (BinaryPrimitives.ReadUInt64LittleEndian(bytes), BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]));
        }
    }
}