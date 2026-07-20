using SharpNBT;
using System.IO.Compression;
using Solace.Common.Utils;

namespace Solace.PreviewGenerator;

internal sealed class ServerDataZip
{
    public static ServerDataZip Read(Stream inputStream)
        => new ServerDataZip(inputStream);

    private readonly Dictionary<string, byte[]> _files = [];

    private ServerDataZip(Stream inputStream)
    {
        using var archive = new ZipArchive(inputStream);

        foreach (var entry in archive.Entries)
        {
            if (entry.IsDirectory)
            {
                continue;
            }

            using (Stream entryStream = entry.Open())
            using (var ms = new MemoryStream())
            {
                entryStream.CopyTo(ms);
                _files.Add(entry.FullName, ms.ToArray());
            }
        }
    }

    public CompoundTag GetChunkNBT(int x, int z)
    {
        var regionX = x >> 5;
        var regionZ = z >> 5;
        var chunkX = x & 31;
        var chunkZ = z & 31;
        var chunkIndex = (chunkZ << 5) | chunkX;

        using var ms = new MemoryStream(_files[$"region/r.{regionX}.{regionZ}.mca"]);
        using var reader = new BinaryReader(ms);

        ms.Seek(chunkIndex * 4, SeekOrigin.Begin);
        var offset = (int)(reader.ReadUInt32BE() >> 8);

        ms.Seek(offset * 4096, SeekOrigin.Begin);

        var length = (int)reader.ReadUInt32BE();
        var compressionType = reader.ReadByte();
        var compressed = new byte[length];
        ms.Read(compressed);
        byte[] uncompressed;
        switch (compressionType)
        {
            case 1:
                {
                    using var gZipStream = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress, false);
                    using var resultStream = new MemoryStream();
                    gZipStream.CopyTo(resultStream);
                    uncompressed = resultStream.ToArray();
                }

                break;
            case 2:
                {
                    using var deflateStream = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress, false);
                    using var resultStream = new MemoryStream();
                    deflateStream.CopyTo(resultStream);
                    uncompressed = resultStream.ToArray();
                }

                break;
            case 3:
                {
                    uncompressed = compressed;
                    break;
                }
            default:
                throw new IOException($"Invalid compression type {compressionType}");
        }

        using (var tagStream = new MemoryStream(uncompressed))
        using (var tagReader = new TagReader(tagStream, FormatOptions.Java, false))
        {
            CompoundTag tag = tagReader.ReadTag<CompoundTag>();

            return tag;
        }
    }
}
