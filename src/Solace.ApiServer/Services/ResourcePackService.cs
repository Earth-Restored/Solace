using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.ResourcePacks;
using Solace.EventBus.Client;
using Solace.StaticData;

namespace Solace.ApiServer.Services;

internal sealed partial class ResourcePackService : IDisposable
{
    public const string GenoaPackId = "dba38e59-091a-4826-b76a-a08d7de5a9e2";

    internal sealed record ActiveResourcePackInfo(
        Guid Id,
        int BuildNumber,
        string VersionString,
        string RelativePath
    );

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly IDbContextFactory<EarthDbContext> _earthDbFactory;
    private readonly StaticDataProvider _staticData;
    private readonly ILogger<ResourcePackService> _logger;

    private volatile ActiveResourcePackInfo? _cachedPack;
    private readonly SemaphoreSlim _synthesisLock = new(1, 1);
    private Subscriber? _subscriber;
    private EventBusClient? _eventBusClient;

    public ResourcePackService(IDbContextFactory<EarthDbContext> earthDbFactory, StaticDataProvider staticData, ILogger<ResourcePackService> logger)
    {
        _earthDbFactory = earthDbFactory;
        _staticData = staticData;
        _logger = logger;
    }

    public async Task InitializeAsync(EventBusClient eventBusClient)
    {
        _eventBusClient = eventBusClient;

        _subscriber = await eventBusClient.AddSubscriberAsync(
            "resourcepack",
            HandleEvent,
            exception =>
            {
                if (exception is not null)
                {
                    LogSubscriberError(_logger, exception);
                }

                return Task.CompletedTask;
            });

        // ensure active rs is cached on startup
        try
        {
            await using var earthDb = await _earthDbFactory.CreateDbContextAsync();
            await RefreshActivePackAsync(earthDb, force: false);
        }
        catch (Exception exception)
        {
            LogStartupSynthesisError(_logger, exception);
        }
    }

    private async Task HandleEvent(SubscriberEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Type is "updated")
        {
            LogResourcePackUpdatedEventReceived(_logger);
            await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);
            await RefreshActivePackAsync(earthDb, force: false, cancellationToken);
        }
        else if (@event.Type is "synthesize")
        {
            LogSynthesizeRequestedEventReceived(_logger);
            await using var earthDb = await _earthDbFactory.CreateDbContextAsync(cancellationToken);
            await RefreshActivePackAsync(earthDb, force: true, cancellationToken);
        }
    }

    public ActiveResourcePackInfo? GetActiveResourcePack()
        => _cachedPack;

    public async Task RefreshActivePackAsync(EarthDbContext earthDb, bool force, CancellationToken cancellationToken = default)
    {
        await _synthesisLock.WaitAsync(cancellationToken);

        try
        {
            var pack = await EnsureSynthesizedAsync(earthDb, force, cancellationToken);

            if (pack is not null)
            {
                _cachedPack = new ActiveResourcePackInfo(
                    pack.Id,
                    pack.BuildNumber,
                    pack.VersionString,
                    $"availableresourcepack/resourcepacks/{pack.Id}"
                );
            }
            else if (_staticData.Resourcepacks.GenoaResourcepackName is not null)
            {
                _cachedPack = new ActiveResourcePackInfo(
                    Guid.TryParse(_staticData.Resourcepacks.GenoaResourcepackName, out var baseGuid) ? baseGuid : Guid.Empty,
                    4,
                    "2020.1214.04",
                    $"availableresourcepack/resourcepacks/{_staticData.Resourcepacks.GenoaResourcepackName}"
                );
            }
            else
            {
                _cachedPack = null;
            }
        }
        finally
        {
            _synthesisLock.Release();
        }
    }

    public async Task<SynthesizedResourcePackEF?> EnsureSynthesizedAsync(EarthDbContext earthDb, bool force, CancellationToken cancellationToken)
    {
        var basePackPath = _staticData.Resourcepacks.GenoaResourcepackPath;
        if (string.IsNullOrWhiteSpace(basePackPath) || !File.Exists(basePackPath))
        {
            LogBaseResourcePackMissing(_logger, basePackPath ?? "null");
            return null;
        }

        var activePack = await earthDb.SynthesizedResourcePacks
            .AsNoTracking()
            .OrderByDescending(p => p.BuildNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var cachedFilePath = activePack is not null
            ? _staticData.Resourcepacks.GetCachedResourcepackPath(activePack.Id)
            : null;

        var basePackInfo = new FileInfo(basePackPath);
        var basePackHash = ComputeBasePackHash(basePackInfo);

        if (!force && activePack is not null && activePack.ContentHash == basePackHash && File.Exists(cachedFilePath))
        {
            return activePack;
        }

        var assets = await earthDb.ResourcePackAssets
            .AsNoTracking()
            .OrderBy(a => a.Path)
            .ToListAsync(cancellationToken);

        var translations = await earthDb.ResourcePackTranslations
            .AsNoTracking()
            .OrderBy(t => t.Language)
            .ThenBy(t => t.Key)
            .ToListAsync(cancellationToken);

        return await SynthesizeInternalAsync(earthDb, basePackPath, assets, translations, basePackHash, cancellationToken);
    }

    private async Task<SynthesizedResourcePackEF?> SynthesizeInternalAsync(EarthDbContext earthDb, string basePackPath, List<ResourcePackAssetEF> assets, List<ResourcePackTranslationEF> translations, string contentHash, CancellationToken cancellationToken)
    {
        var maxBuild = await earthDb.SynthesizedResourcePacks
            .MaxAsync(p => (int?)p.BuildNumber, cancellationToken) ?? 4;

        var newBuildNumber = int.Max(maxBuild + 1, 5);
        var versionString = $"2020.1214.{newBuildNumber:D2}";
        var newGuid = Guid.NewGuid();

        var cacheDir = _staticData.Resourcepacks.GenoaCacheDirectory;
        Directory.CreateDirectory(cacheDir);

        var finalOuterPath = _staticData.Resourcepacks.GetCachedResourcepackPath(newGuid);
        var tempOuterPath = Path.Combine(cacheDir, $"{newGuid}.tmp");
        var tempBaseInnerPath = Path.Combine(cacheDir, $"{newGuid}.base.tmp");

        LogStartingSynthesis(_logger, newGuid, newBuildNumber);

        try
        {
            using (var baseOuterZip = ZipFile.OpenRead(basePackPath))
            {
                var innerEntry = baseOuterZip.GetEntry("genoa.mcpack")
                    ?? throw new InvalidOperationException("Base resource pack does not contain genoa.mcpack");

                await using (var tempBaseFile = new FileStream(tempBaseInnerPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    await using var innerEntryStream = innerEntry.Open();
                    await innerEntryStream.CopyToAsync(tempBaseFile, cancellationToken);
                }

                using var baseInnerZip = ZipFile.OpenRead(tempBaseInnerPath);

                await using (var outerZipStream = new FileStream(tempOuterPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                using (var outerZip = new ZipArchive(outerZipStream, ZipArchiveMode.Create))
                {
                    var outerEntry = outerZip.CreateEntry("genoa.mcpack", CompressionLevel.NoCompression);
                    await using (var innerPackStream = outerEntry.Open())
                    using (var newInnerZip = new ZipArchive(innerPackStream, ZipArchiveMode.Create, leaveOpen: false))
                    {
                        var customTranslationsByLang = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                        foreach (var translation in translations)
                        {
                            if (!customTranslationsByLang.TryGetValue(translation.Language, out var map))
                            {
                                map = [with(StringComparer.OrdinalIgnoreCase)];
                                customTranslationsByLang[translation.Language] = map;
                            }

                            map[translation.Key] = translation.Value;
                        }

                        var customAssetPaths = assets.ToDictionary(
                            a => a.Path.TrimStart('/').Replace('\\', '/'),
                            a => a.Data,
                            StringComparer.OrdinalIgnoreCase
                        );

                        var writtenPaths = new List<string>(baseInnerZip.Entries.Count + customAssetPaths.Count + 6);
                        var writtenLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var writtenTextures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        // copy base entries except manifest.json, contents.json, texts/languages.json, textures/textures_list.json, overridden assets, and merged .lang files
                        foreach (var entry in baseInnerZip.Entries)
                        {
                            var normalizedName = entry.FullName.Replace('\\', '/');

                            if (string.Equals(normalizedName, "manifest.json", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(normalizedName, "contents.json", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (string.Equals(normalizedName, "texts/languages.json", StringComparison.OrdinalIgnoreCase))
                            {
                                using var langJsonStream = entry.Open();
                                using var doc = await JsonDocument.ParseAsync(langJsonStream, cancellationToken: cancellationToken);
                                if (doc.RootElement.ValueKind is JsonValueKind.Array)
                                {
                                    foreach (var item in doc.RootElement.EnumerateArray())
                                    {
                                        if (item.ValueKind is JsonValueKind.String && item.GetString() is { Length: > 0 } langCode)
                                        {
                                            writtenLanguages.Add(langCode);
                                        }
                                    }
                                }

                                continue;
                            }

                            if (string.Equals(normalizedName, "textures/textures_list.json", StringComparison.OrdinalIgnoreCase))
                            {
                                using var texJsonStream = entry.Open();
                                using var doc = await JsonDocument.ParseAsync(texJsonStream, cancellationToken: cancellationToken);
                                if (doc.RootElement.ValueKind is JsonValueKind.Array)
                                {
                                    foreach (var item in doc.RootElement.EnumerateArray())
                                    {
                                        if (item.ValueKind is JsonValueKind.String && item.GetString() is { Length: > 0 } texturePath)
                                        {
                                            writtenTextures.Add(texturePath);
                                        }
                                    }
                                }

                                continue;
                            }

                            if (customAssetPaths.ContainsKey(normalizedName))
                            {
                                continue;
                            }

                            if (normalizedName.StartsWith("texts/", StringComparison.OrdinalIgnoreCase) &&
                                normalizedName.EndsWith(".lang", StringComparison.OrdinalIgnoreCase))
                            {
                                var langName = Path.GetFileNameWithoutExtension(normalizedName);
                                writtenLanguages.Add(langName);

                                if (customTranslationsByLang.Remove(langName, out var customMap))
                                {
                                    var langEntry = newInnerZip.CreateEntry(normalizedName, CompressionLevel.Fastest);
                                    await using (var langStream = langEntry.Open())
                                    await using (var writer = new StreamWriter(langStream, Utf8NoBom))
                                    {
                                        await MergeLangFileAsync(entry, customMap, writer, cancellationToken);
                                    }

                                    writtenPaths.Add(normalizedName);
                                    continue;
                                }
                            }

                            var copiedEntry = newInnerZip.CreateEntry(normalizedName, CompressionLevel.Fastest);
                            await using var sourceStream = entry.Open();
                            await using var destStream = copiedEntry.Open();
                            await sourceStream.CopyToAsync(destStream, cancellationToken);
                            writtenPaths.Add(normalizedName);
                            TryTrackTexturePath(normalizedName, writtenTextures);
                        }

                        // write remaining custom .lang files
                        foreach (var (lang, customMap) in customTranslationsByLang)
                        {
                            var langPath = $"texts/{lang}.lang";
                            var langEntry = newInnerZip.CreateEntry(langPath, CompressionLevel.Fastest);
                            await using (var langStream = langEntry.Open())
                            await using (var writer = new StreamWriter(langStream, Utf8NoBom))
                            {
                                await WriteCustomLangFileAsync(writer, customMap);
                            }

                            writtenLanguages.Add(lang);
                            writtenPaths.Add(langPath);
                        }

                        if (writtenLanguages.Count > 0)
                        {
                            var languagesList = writtenLanguages.ToList();
                            languagesList.Sort(StringComparer.OrdinalIgnoreCase);

                            var languagesEntry = newInnerZip.CreateEntry("texts/languages.json", CompressionLevel.Fastest);
                            await using (var languagesStream = languagesEntry.Open())
                            await using (var jsonWriter = new Utf8JsonWriter(languagesStream, new JsonWriterOptions { Indented = true }))
                            {
                                jsonWriter.WriteStartArray();
                                foreach (var lang in languagesList)
                                {
                                    jsonWriter.WriteStringValue(lang);
                                }

                                jsonWriter.WriteEndArray();
                                await jsonWriter.FlushAsync(cancellationToken);
                            }

                            writtenPaths.Add("texts/languages.json");
                        }

                        // write custom assets
                        foreach (var (assetPath, assetData) in customAssetPaths)
                        {
                            var assetEntry = newInnerZip.CreateEntry(assetPath, CompressionLevel.Fastest);
                            await using var assetStream = assetEntry.Open();
                            await assetStream.WriteAsync(assetData, cancellationToken);
                            writtenPaths.Add(assetPath);
                            TryTrackTexturePath(assetPath, writtenTextures);
                        }

                        if (writtenTextures.Count > 0)
                        {
                            var texturesList = writtenTextures.ToList();
                            texturesList.Sort(StringComparer.OrdinalIgnoreCase);

                            var texturesListEntry = newInnerZip.CreateEntry("textures/textures_list.json", CompressionLevel.Fastest);
                            await using (var texturesListStream = texturesListEntry.Open())
                            await using (var jsonWriter = new Utf8JsonWriter(texturesListStream, new JsonWriterOptions { Indented = true }))
                            {
                                jsonWriter.WriteStartArray();
                                foreach (var texturePath in texturesList)
                                {
                                    jsonWriter.WriteStringValue(texturePath);
                                }

                                jsonWriter.WriteEndArray();
                                await jsonWriter.FlushAsync(cancellationToken);
                            }

                            writtenPaths.Add("textures/textures_list.json");
                        }

                        // update manifest.json
                        var baseManifestEntry = baseInnerZip.GetEntry("manifest.json");
                        string manifestContent;
                        if (baseManifestEntry is not null)
                        {
                            using var baseManifestEntryReader = new StreamReader(baseManifestEntry.Open(), Encoding.UTF8);
                            manifestContent = UpdateManifestJson(await baseManifestEntryReader.ReadToEndAsync(cancellationToken), newBuildNumber);
                        }
                        else
                        {
                            manifestContent = CreateDefaultManifestJson(newBuildNumber);
                        }

                        var manifestEntry = newInnerZip.CreateEntry("manifest.json", CompressionLevel.Fastest);
                        await using (var writer = new StreamWriter(manifestEntry.Open(), Utf8NoBom))
                        {
                            await writer.WriteAsync(manifestContent);
                        }

                        writtenPaths.Add("manifest.json");

                        // update contents.json
                        writtenPaths.Add("contents.json");
                        writtenPaths.Sort(StringComparer.OrdinalIgnoreCase);

                        var contentsEntry = newInnerZip.CreateEntry("contents.json", CompressionLevel.Fastest);
                        await using (var contentsStream = contentsEntry.Open())
                        {
                            await WriteContentsJsonAsync(contentsStream, writtenPaths, cancellationToken);
                        }
                    }
                }
            }

            File.Move(tempOuterPath, finalOuterPath, overwrite: true);
            var fileSize = new FileInfo(finalOuterPath).Length;

            var newPack = new SynthesizedResourcePackEF
            {
                Id = newGuid,
                BuildNumber = newBuildNumber,
                VersionString = versionString,
                ContentHash = contentHash,
            };

            List<SynthesizedResourcePackEF> oldPacks;
            await using (var transaction = await earthDb.Database.BeginTransactionAsync(cancellationToken))
            {
                oldPacks = await earthDb.SynthesizedResourcePacks.ToListAsync(cancellationToken);
                earthDb.SynthesizedResourcePacks.RemoveRange(oldPacks);

                earthDb.SynthesizedResourcePacks.Add(newPack);
                await earthDb.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }

            // clean up old cached files on disk
            foreach (var oldPack in oldPacks)
            {
                if (oldPack.Id != newGuid)
                {
                    var oldFilePath = _staticData.Resourcepacks.GetCachedResourcepackPath(oldPack.Id);

                    if (File.Exists(oldFilePath))
                    {
                        try
                        {
                            File.Delete(oldFilePath);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            _cachedPack = new ActiveResourcePackInfo(
                newGuid,
                newBuildNumber,
                versionString,
                $"availableresourcepack/resourcepacks/{newGuid}"
            );

            Debug.Assert(_eventBusClient is not null);
            await _eventBusClient.PublishAsync("resourcepack", "updated", newGuid.ToString(), cancellationToken);

            LogSynthesisCompleted(_logger, newGuid, newBuildNumber, fileSize);
            return newPack;
        }
        finally
        {
            if (File.Exists(tempBaseInnerPath))
            {
                try
                {
                    File.Delete(tempBaseInnerPath);
                }
                catch
                {
                }
            }

            if (File.Exists(tempOuterPath))
            {
                try
                {
                    File.Delete(tempOuterPath);
                }
                catch
                {
                }
            }
        }
    }

    private static void TryTrackTexturePath(string path, HashSet<string> textures)
    {
        if (!path.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".tga", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            textures.Add(path[..^4]);
        }
    }

    private static string ComputeBasePackHash(FileInfo basePackInfo)
    {
        // we don't need to actually hash the full file, base pack won't change much and change will likely change size and ofc last write
        var hasher = new XxHash3();

        Span<byte> numBuffer = stackalloc byte[sizeof(long) * 2];
        BinaryPrimitives.WriteInt64LittleEndian(numBuffer[..8], basePackInfo.Length);
        BinaryPrimitives.WriteInt64LittleEndian(numBuffer[8..], basePackInfo.LastWriteTimeUtc.Ticks);
        hasher.Append(numBuffer);

        return hasher.GetCurrentHashAsUInt64().ToString("x16");
    }

    private static async Task MergeLangFileAsync(ZipArchiveEntry entry, Dictionary<string, string> customMap, StreamWriter writer, CancellationToken cancellationToken)
    {
        var lookup = customMap.GetAlternateLookup<ReadOnlySpan<char>>();

        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                var span = line.AsSpan().TrimStart();
                if (span.StartsWith('#') || !span.Contains('='))
                {
                    await writer.WriteLineAsync(line);
                    continue;
                }

                var equalsIndex = span.IndexOf('=');
                var keySpan = span[..equalsIndex].Trim();
                if (lookup.Remove(keySpan, out var key, out var customVal))
                {
                    await writer.WriteAsync(key);
                    await writer.WriteAsync('=');
                    await writer.WriteLineAsync(customVal);
                }
                else
                {
                    await writer.WriteLineAsync(line);
                }
            }
        }

        if (customMap.Count > 0)
        {
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("## Synthesized Custom Strings");

            foreach (var (key, value) in customMap)
            {
                await writer.WriteAsync(key);
                await writer.WriteAsync('=');
                await writer.WriteLineAsync(value);
            }
        }
    }

    private static async Task WriteCustomLangFileAsync(StreamWriter writer, Dictionary<string, string> customMap)
    {
        await writer.WriteLineAsync("## Synthesized Custom Strings");

        foreach (var (key, value) in customMap)
        {
            await writer.WriteAsync(key);
            await writer.WriteAsync('=');
            await writer.WriteLineAsync(value);
        }
    }

    private static string UpdateManifestJson(string json, int buildNumber)
    {
        try
        {
            var node = JsonNode.Parse(json);

            if (node is JsonObject objNode && objNode["header"] is JsonObject header)
            {
                header["uuid"] = GenoaPackId;
                header["version"] = new JsonArray(2020, 1214, buildNumber);

                if (objNode["modules"] is JsonArray modules && modules.Count > 0 && modules[0] is JsonObject module0)
                {
                    module0["version"] = new JsonArray(0, 0, buildNumber);
                }

                return objNode.ToJsonString(IndentedJsonOptions);
            }
        }
        catch
        {
        }

        return CreateDefaultManifestJson(buildNumber);
    }

    private static string CreateDefaultManifestJson(int buildNumber)
    {
        var manifest = new
        {
            format_version = 1,
            header = new
            {
                description = "resourcePack.genoa.description",
                name = "resourcePack.genoa.name",
                uuid = GenoaPackId,
                version = new[] { 2020, 1214, buildNumber },
                min_engine_version = new[] { 1, 2, 6 },
            },
            modules = new[]
            {
                new
                {
                    description = "resourcePack.genoa.description",
                    type = "resources",
                    uuid = Guid.NewGuid().ToString(),
                    version = new[] { 0, 0, buildNumber },
                },
            },
        };

        return JsonSerializer.Serialize(manifest, IndentedJsonOptions);
    }

    private static async Task WriteContentsJsonAsync(Stream stream, List<string> paths, CancellationToken cancellationToken)
    {
        await using var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
        });

        jsonWriter.WriteStartObject();
        jsonWriter.WriteStartArray("content");

        foreach (var path in paths)
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString("path", path);
            jsonWriter.WriteEndObject();
        }

        jsonWriter.WriteEndArray();
        jsonWriter.WriteEndObject();

        await jsonWriter.FlushAsync(cancellationToken);
    }

    public void Dispose()
        => _synthesisLock.Dispose();

    [LoggerMessage(Level = LogLevel.Error, Message = "ResourcePack subscriber error")]
    private static partial void LogSubscriberError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to ensure synthesized resource pack at startup")]
    private static partial void LogStartupSynthesisError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "ResourcePack updated event received, invalidation cached info")]
    private static partial void LogResourcePackUpdatedEventReceived(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "ResourcePack synthesize requested event received, starting synthesis")]
    private static partial void LogSynthesizeRequestedEventReceived(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Base resource pack not found at {Path}, cannot synthesize")]
    private static partial void LogBaseResourcePackMissing(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting resource pack synthesis for Id: {Guid}, Build: {Build}")]
    private static partial void LogStartingSynthesis(ILogger logger, Guid guid, int build);

    [LoggerMessage(Level = LogLevel.Information, Message = "Resource pack synthesis completed, Id: {Guid}, Build: {Build}, Size: {Size} bytes")]
    private static partial void LogSynthesisCompleted(ILogger logger, Guid guid, int build, long size);
}
