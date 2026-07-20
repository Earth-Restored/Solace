using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Solace.Common;
using Solace.Common.Utils;
using static Solace.Buildplate.Model.WorldData.Logs;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Solace.Buildplate.Model;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed partial record class WorldData(
    byte[] ServerData,
    int Size,
    int Offset,
    bool Night
)
{
    public const string MetadataFileName = "buildplate_metadata.json";

    public static async Task<WorldData?> LoadFromZipAsync(Stream stream, ILogger logger, CancellationToken cancellationToken = default)
    {
        Dictionary<string, byte[]> worldFileContents = [];

        try
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        continue;
                    }

                    var entryPath = entry.FullName.AsSpan().Trim(['/', '\\']);

                    if (entryPath is not MetadataFileName)
                    {
                        // must be allocated here because of await
#pragma warning disable CA2014 // Do not use stackalloc in loops
                        Span<Range> parts = stackalloc Range[3];
#pragma warning restore CA2014 // Do not use stackalloc in loops
                        var partCount = entryPath.SplitAny(parts, ['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

                        if (partCount != 2)
                        {
                            continue;
                        }

                        if (entryPath[parts[0]] is not ("region" or "entities"))
                        {
                            continue;
                        }

                        if (entryPath[parts[1]] is not ("r.0.0.mca" or "r.0.-1.mca" or "r.-1.0.mca" or "r.-1.-1.mca"))
                        {
                            continue;
                        }
                    }

                    using (var entryStream = await entry.OpenAsync(cancellationToken))
                    using (var ms = new MemoryStream())
                    {
                        await entryStream.CopyToAsync(ms, cancellationToken);

                        worldFileContents[entry.FullName] = ms.ToArray();
                    }
                }
            }
        }
        catch (IOException ex)
        {
            LogWorldFileReadError(logger, ex);
            return null;
        }

        byte[] serverData;
        try
        {
            using (var zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var dirName in (IEnumerable<string>)["region", "entities"])
                    {
                        foreach (var fileName in (IEnumerable<string>)["r.0.0.mca", "r.0.-1.mca", "r.-1.0.mca", "r.-1.-1.mca"])
                        {
                            var filePath = $"{dirName}/{fileName}";

                            if (!worldFileContents.TryGetValue(filePath, out var data))
                            {
                                LogWorldFileFileMissing(logger, filePath);
                                return null;
                            }

                            var entry = zip.CreateEntry(filePath, CompressionLevel.SmallestSize);
                            using (var entryStream = await entry.OpenAsync(cancellationToken))
                            {
                                entryStream.Write(data);
                            }
                        }
                    }
                }

                serverData = zipStream.ToArray();
            }
        }
        catch (IOException exception)
        {
            LogServerDataPrepareError(logger, exception);
            return null;
        }

        var buildplateMetadataFileData = worldFileContents.GetValueOrDefault(MetadataFileName);
        var buildplateMetadataString = buildplateMetadataFileData is not null
            ? Encoding.UTF8.GetString(buildplateMetadataFileData)
            : null;

        return WorldData.Load(serverData, buildplateMetadataString, logger);

    }

    public static WorldData? Load(byte[] serverData, string? buildplateMetadataString, ILogger logger)
    {
        int size;
        int offset;
        bool night;

        try
        {
            if (buildplateMetadataString is null)
            {
                LogNoBuildplateMetadataFile(logger);
                size = 16;
                offset = 63;
                night = false;
            }
            else
            {
                var buildplateMetadataVersion = JsonSerializer.Deserialize(buildplateMetadataString, AppJsonContext.Default.BuildplateMetadataVersion);

                if (buildplateMetadataVersion is null)
                {
                    LogInvalidBuildplateMetadata(logger);
                    return null;
                }

                switch (buildplateMetadataVersion.Version)
                {
                    case 1:
                        {
                            var buildplateMetadata = JsonSerializer.Deserialize(buildplateMetadataString, AppJsonContext.Default.BuildplateMetadataV1);

                            if (buildplateMetadata is null)
                            {
                                LogInvalidBuildplateMetadata(logger);
                                return null;
                            }

                            size = buildplateMetadata.Size;
                            offset = buildplateMetadata.Offset;
                            night = buildplateMetadata.Night;
                        }

                        break;
                    default:
                        {
                            LogUnsupportedBuildplateMetadataVersion(logger, buildplateMetadataVersion.Version);
                            return null;
                        }
                }
            }
        }
        catch (Exception ex)
        {
            LogBuildplateMetadataReadError(logger, ex);
            return null;
        }

        if (size != 8 && size != 16 && size != 32)
        {
            LogInvalidBuildplateSite(logger, size);
            return null;
        }

        return new WorldData(serverData, size, offset, night);
    }

    public static void WriteMetadata(Stream stream, BuildplateMetadataV1 data)
    {
        var versionData = new BuildplateMetadataVersion(1);

        var versionNode = JsonSerializer.SerializeToNode(versionData, AppJsonContext.Default.BuildplateMetadataVersion) as JsonObject;
        var v1Node = JsonSerializer.SerializeToNode(data, AppJsonContext.Default.BuildplateMetadataV1) as JsonObject;

        if (versionNode is null || v1Node is null)
        {
            throw new InvalidOperationException("Failed to serialize metadata records to JsonNodes.");
        }

        var combinedObject = new JsonObject();

        foreach (var property in versionNode)
        {
            combinedObject.Add(property.Key, property.Value?.DeepClone());
        }

        foreach (var property in v1Node)
        {
            combinedObject.Add(property.Key, property.Value?.DeepClone());
        }

        using var writer = new Utf8JsonWriter(stream);
        combinedObject.WriteTo(writer);
    }

    // LoggerMessage doesn't work in records???
    internal static partial class Logs
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "Could not read world file")]
        internal static partial void LogWorldFileReadError(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Error, Message = "World file is missing file '{FilePath}'")]
        internal static partial void LogWorldFileFileMissing(ILogger logger, string FilePath);

        [LoggerMessage(Level = LogLevel.Error, Message = "Could not prepare server data")]
        internal static partial void LogServerDataPrepareError(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Warning, Message = $"World file does not contain {MetadataFileName}, using default values")]
        internal static partial void LogNoBuildplateMetadataFile(ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Invalid buildplate metadata")]
        internal static partial void LogInvalidBuildplateMetadata(ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Unsupported buildplate metadata version {Version}")]
        internal static partial void LogUnsupportedBuildplateMetadataVersion(ILogger logger, int Version);

        [LoggerMessage(Level = LogLevel.Error, Message = "Could not read buildplate metadata file")]
        internal static partial void LogBuildplateMetadataReadError(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Error, Message = "Invalid buildplate size {Size}, must be on of: 8, 16, 32")]
        internal static partial void LogInvalidBuildplateSite(ILogger logger, int Size);
    }
}