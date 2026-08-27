using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Solace.Common;

public static class IdTranslator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid ToGuid(ReadOnlySpan<char> idString)
    {
        if (idString.IsEmpty)
        {
            return Guid.Empty;
        }

        if (Guid.TryParse(idString, out var guid))
        {
            return guid;
        }

        if (idString.Length <= 32 && IsValidHex(idString))
        {
            return ToGuidDirect(idString);
        }

        return ToGuidViaHash(idString);
    }

    // todo: optimmize - remove TryFormat
    public static string ToHex(Guid id)
    {
        if (id == Guid.Empty)
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[32];
        bool formatted = id.TryFormat(buffer, out int charsWritten, "N");
        Debug.Assert(formatted && charsWritten == 32);

        ReadOnlySpan<char> trimmed = buffer.TrimStart('0');
        
        return trimmed.IsEmpty ? string.Empty : trimmed.ToString();
    }

    private static Guid ToGuidDirect(ReadOnlySpan<char> hexId)
    {
        Span<char> padded = stackalloc char[32];
        var paddingLength = 32 - hexId.Length;

        padded[..paddingLength].Fill('0');
        hexId.CopyTo(padded[paddingLength..]);

        var parseSuccessful = Guid.TryParse(padded, out var shortGuid);
        Debug.Assert(parseSuccessful);

        return shortGuid;
    }

    private static Guid ToGuidViaHash(ReadOnlySpan<char> longId)
    {
        var maxByteCount = Encoding.UTF8.GetByteCount(longId);

        // Hybrid approach: use stackalloc for small strings, ArrayPool for large ones to prevent Stack Overflow
        byte[]? rented = null;
        Span<byte> utf8Bytes = maxByteCount <= 1024
            ? stackalloc byte[1024]
            : (rented = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            var writtenUtf8 = Encoding.UTF8.GetBytes(longId, utf8Bytes);
            Span<byte> hashBytes = stackalloc byte[16];

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms - does not need to be secure, just consistent
            MD5.HashData(utf8Bytes[..writtenUtf8], hashBytes);
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

            return new Guid(hashBytes);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static bool IsValidHex(ReadOnlySpan<char> chars)
    {
        foreach (char c in chars)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'A' and <= 'F') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}