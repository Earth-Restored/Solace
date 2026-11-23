using System.Buffers.Binary;
using System.Globalization;

namespace Solace.ApiServer.Utils;

// Accounts are identified by a Guid, which the Android client accepts as an xuid.
// The iOS XAL client requires a numeric (uint64) xuid, so derive a stable one from the Guid.
internal static class XuidUtils
{
    public static ulong FromUserId(Guid userId)
    {
        Span<byte> bytes = stackalloc byte[16];
        userId.TryWriteBytes(bytes);

        // Keep it in the range of real xuids (2535...) so clients treat it like one
        return 0x0009_0000_0000_0000UL | (BinaryPrimitives.ReadUInt64LittleEndian(bytes) & 0x0000_FFFF_FFFF_FFFFUL);
    }

    public static string ToNumericString(Guid userId)
        => FromUserId(userId).ToString(CultureInfo.InvariantCulture);

    public static bool Matches(string xuid, Guid userId)
        => Guid.TryParse(xuid, out var guid)
            ? guid == userId
            : ulong.TryParse(xuid, NumberStyles.None, CultureInfo.InvariantCulture, out ulong numeric) && numeric == FromUserId(userId);
}
