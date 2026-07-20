using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BitcoderCZ.Maths.Vectors;
using Cyotek.Data.Nbt;

namespace Solace.BuildplateRenderer.Utils;

// https://minecraft.wiki/w/Anvil_file_format
internal static partial class RegionUtils
{
    public const int RegionSize = 32;
    public const int ChunkToLocalMask = RegionSize - 1;

    public const int TimestampOffset = 0x1000;
    public const int HeaderLength = 0x1000 + 0x1000;
    public const int ChunkSize = 0x1000;

    public const byte CompressionTypeGzip = 1;
    public const byte CompressionTypeZlib = 2;
    public const byte CompressionTypeNone = 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 ChunkToRegion(int2 chunkPosition)
        => new int2(chunkPosition.X >> 5, chunkPosition.Y >> 5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 ChunkToLocal(int2 chunkPosition)
        => new int2(chunkPosition.X & ChunkToLocalMask, chunkPosition.Y & ChunkToLocalMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 LocalToChunk(int2 localPosition, int2 regionPosition)
        => localPosition + (regionPosition * RegionSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LocalToIndex(int2 localPosition)
        => (localPosition.Y << 5) | localPosition.X;

    public static int2 PathToPos(ReadOnlySpan<char> path)
    {
        Debug.Assert(RegionFileRegex().IsMatch(path), $"{nameof(path)} should corespond to a region file.");

        Debug.Assert(path.StartsWith("region/"), $"{nameof(path)} should start with 'region/'");
        path = path[7..];

        Debug.Assert(!path.Contains('/'), $"{nameof(path)} shouldn't contain '/' at this point.");
        Debug.Assert(path.StartsWith("r."), $"{nameof(path)} should start with 'r.' at this point.");
        path = path[2..];

        var dotIndex = path.IndexOf('.');
        Debug.Assert(dotIndex != -1, $"{nameof(path)} should contain '.' at this point.");
        var regionX = int.Parse(path[..dotIndex]);
        path = path[(dotIndex + 1)..];

        dotIndex = path.IndexOf('.');
        Debug.Assert(dotIndex != -1, $"{nameof(path)} should contain '.' at this point.");
        var regionZ = int.Parse(path[..dotIndex]);

        return new int2(regionX, regionZ);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint CalculatePaddedLength(uint chunkDataLength)
    {
        chunkDataLength += 5; // header
        return chunkDataLength % ChunkSize == 0 ? chunkDataLength : chunkDataLength + (ChunkSize - (chunkDataLength % ChunkSize));
    }

    public static bool ContainsChunk(ReadOnlySpan<byte> regionData, int2 localPosition)
    {
        ValidateLocalCoords(localPosition);

        var chunkIndex = LocalToIndex(localPosition);

        var offset = BinaryPrimitives.ReadInt32BigEndian(regionData[(chunkIndex * 4)..]) >> 8;

        return offset >= 2;
    }

    public static IEnumerable<int2> GetChunkPositions(ReadOnlyMemory<byte> regionData)
    {
        for (var z = 0; z < RegionSize; z++)
        {
            for (var x = 0; x < RegionSize; x++)
            {
                var pos = new int2(x, z);
                if (ContainsChunk(regionData.Span, pos))
                {
                    yield return pos;
                }
            }
        }
    }

    public static ReadOnlyMemory<byte> ReadRawChunkData(ReadOnlyMemory<byte> regionData, int2 localPosition, out byte compressionType)
    {
        ValidateLocalCoords(localPosition);

        var dataSpan = regionData.Span;

        Debug.Assert(ContainsChunk(dataSpan, localPosition), $"{nameof(regionData)} should contain a chunk at {localPosition}.");

        var chunkIndex = LocalToIndex(localPosition);

        var offset = (BinaryPrimitives.ReadInt32BigEndian(dataSpan[(chunkIndex * 4)..]) >> 8) * ChunkSize;

        var length = BinaryPrimitives.ReadInt32BigEndian(dataSpan[offset..]) - 1;
        compressionType = dataSpan[offset + 4];

        return regionData.Slice(offset + 5, length);
    }

    /// <exception cref="InvalidDataException">Thrown if the compression type is invalid.</exception>
    public static MemoryStream ReadChunkData(ReadOnlyMemory<byte> regionData, int2 localPosition)
    {
        ValidateLocalCoords(localPosition);

        ReadOnlyMemory<byte> chunkData = ReadRawChunkData(regionData, localPosition, out var compressionType);

        MemoryStream uncompressed;

        switch (compressionType)
        {
            case CompressionTypeGzip:
                {
                    uncompressed = new MemoryStream(chunkData.Length * 2);

                    using var gZipStream = new GZipStream(new ReadOnlySpanStream(chunkData), CompressionMode.Decompress, false);
                    gZipStream.CopyTo(uncompressed);
                }

                break;
            case CompressionTypeZlib:
                {
                    uncompressed = new MemoryStream(chunkData.Length * 2);

                    using var deflateStream = new ZLibStream(new ReadOnlySpanStream(chunkData), CompressionMode.Decompress, false);
                    deflateStream.CopyTo(uncompressed);
                }

                break;
            case CompressionTypeNone:
                {
                    var buffer = new byte[chunkData.Length];
                    chunkData.CopyTo(buffer.AsMemory());
                    uncompressed = new MemoryStream(buffer);
                    break;
                }

            default:
                throw new InvalidDataException($"Invalid/unknown compression type '{compressionType}'.");
        }

        uncompressed.Position = 0;

        return uncompressed;
    }

    /// <exception cref="InvalidDataException">Thrown if the compression type is invalid.</exception>
    public static NbtDocument ReadChunkNTB(ReadOnlyMemory<byte> regionData, int2 localPosition)
    {
        ValidateLocalCoords(localPosition);

        using (MemoryStream ms = ReadChunkData(regionData, localPosition))
        {
            var document = new NbtDocument();
            document.Load(ms);
            return document;
        }
    }

    [Conditional("DEBUG")]
    private static void ValidateLocalCoords(int2 localPosition)
    {
        Debug.Assert(localPosition.X is >= 0 and < RegionSize, $"{nameof(localPosition)}.X must be in bounds.");
        Debug.Assert(localPosition.Y is >= 0 and < RegionSize, $"{nameof(localPosition)}.Y must be in bounds.");
    }

    [GeneratedRegex(@"^region/r\.-?\d+\.-?\d+\.mca$", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex RegionFileRegex();
}