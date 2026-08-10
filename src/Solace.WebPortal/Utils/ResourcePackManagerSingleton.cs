using System.Diagnostics.CodeAnalysis;
using BitcoderCZ.Minecraft.MeshGenerator;
using Solace.Common;

namespace Solace.WebPortal.Utils;

internal static partial class ResourcePackManagerSingleton
{
    private static volatile ResourcePackManager? resourcePackManager;
    private static readonly SemaphoreSlim resourcePackLock = new(1, 1);

    public static async Task<ResourcePackManager> GetResourcePackManagerAsync(string staticDataPath)
    {
        await EnsureResourcePackLoadedAsync(staticDataPath);

        return resourcePackManager;
    }

#pragma warning disable CS8774 // Member must have a non-null value when exiting.
    [MemberNotNull(nameof(resourcePackManager))]
    private static async Task EnsureResourcePackLoadedAsync(string staticDataPath)
    {
        if (resourcePackManager is not null)
        {
            return;
        }

        await resourcePackLock.WaitAsync();

        try
        {
#pragma warning disable CA1508 // Avoid dead conditional code - static volatile
            if (resourcePackManager is null)
#pragma warning restore CA1508 // Avoid dead conditional code
            {
                var dir = new DirectoryInfo(Path.Combine(staticDataPath, "resourcepacks", "java"));
                if (dir.Exists)
                {
                    resourcePackManager = await ResourcePackManager.LoadAllAsync(dir);
                    if (resourcePackManager.Count < 2)
                    {
                        var logger = GlobalLoggerFactory.CreateLogger(nameof(ResourcePackManagerSingleton));

                        LogNotLoadedAllResourcepacks(logger, resourcePackManager.Count);
                        resourcePackManager = null;
                    }
                }
                else
                {
                    var logger = GlobalLoggerFactory.CreateLogger(nameof(ResourcePackManagerSingleton));

                    LogResourcePackDirectoryNotFound(logger);
                }
            }
        }
        finally
        {
            resourcePackLock.Release();
        }
    }
#pragma warning restore CS8774 // Member must have a non-null value when exiting.

    [LoggerMessage(Level = LogLevel.Warning, Message = "Only loaded {LoadedPackCount} resourcepacks, make sure staticdata/resourcepacks/java contains minecraft/ and fountain/")]
    private static partial void LogNotLoadedAllResourcepacks(ILogger logger, int LoadedPackCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resource pack directory not found. Previews will fail")]
    private static partial void LogResourcePackDirectoryNotFound(ILogger logger);
}