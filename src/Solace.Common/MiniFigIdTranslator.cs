using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Solace.Common;

public static class MiniFigIdTranslator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid ToGuid(ReadOnlySpan<char> idString)
    {
        if (idString.IsEmpty || idString.Length > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(idString));
        }

        if (!IsValidHex(idString))
        {
            throw new ArgumentException("Invalid id.", nameof(idString));
        }

        Span<char> padded = stackalloc char[32];
        var paddingLength = 32 - idString.Length;

        padded[..paddingLength].Fill('0');
        idString.CopyTo(padded[paddingLength..]);

        var parseSuccessful = Guid.TryParse(padded, out var shortGuid);
        Debug.Assert(parseSuccessful);

        return shortGuid;
    }

    private static bool IsValidHex(ReadOnlySpan<char> chars)
    {
        foreach (var c in chars)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}