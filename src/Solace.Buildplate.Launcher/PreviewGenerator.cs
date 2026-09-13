using System.Text;
using Microsoft.Extensions.Logging;
using Solace.Buildplate.PreviewGenerator.Registry;
using System.Text.Json;

namespace Solace.Buildplate.Launcher;

internal static partial class PreviewGenerator
{
    public static string? GeneratePreview(byte[] serverData, bool isNight, string staticDataPath, ILogger logger)
    {
        BedrockBlocks.Initialize(staticDataPath, logger);
        JavaBlocks.Initialize(staticDataPath, logger);

        string previewString;
        try
        {
            using (var ms = new MemoryStream(serverData))
            {
                previewString = Solace.Buildplate.PreviewGenerator.Generator.Generate(ms, logger);
            }
        }
        catch (Exception exception)
        {
            LogErrorWhileGeneratingBuildplatePreview(logger, exception);
            return null;
        }

        // todo: use JsonObject?
        Buildplate.PreviewGenerator.PreviewModel previewObject;
        try
        {
            previewObject = JsonSerializer.Deserialize(previewString, AppJsonContext.Default.PreviewModel)!;
        }
        catch (Exception exception)
        {
            LogErrorWhileJsonEncoding(logger, exception);
            return null;
        }

        previewObject = previewObject with { IsNight = isNight, };

        var previewJson = JsonSerializer.Serialize(previewObject, AppJsonContext.Default.PreviewModel);

        var previewBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(previewJson));

        LogPreviewGenerated(logger);
        return previewBase64;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while generating buildplate preview")]
    private static partial void LogErrorWhileGeneratingBuildplatePreview(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error while processing buildplate preview generator response")]
    private static partial void LogErrorWhileJsonEncoding(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Preview generated")]
    private static partial void LogPreviewGenerated(ILogger logger);
}
