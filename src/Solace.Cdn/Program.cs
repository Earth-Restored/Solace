using System.Runtime.CompilerServices;
#if USE_SHARED_LIBS
using System.Runtime.Loader;
#endif
using Microsoft.AspNetCore.Http.HttpResults;

internal static class Program
{
    private static void Main(string[] args)
    {
#if USE_SHARED_LIBS
        AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
        {
            string sharedDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "shared_libs"));
            string assemblyPath = Path.Combine(sharedDir, $"{assemblyName.Name}.dll");

            if (File.Exists(assemblyPath))
            {
                return context.LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        };
#endif

        App.Run(args);
    }
}

internal sealed partial class App
{
    private static string staticDataPath = null!;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Run(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        staticDataPath = builder.Configuration["StaticDataPath"]!;

        if (!File.Exists(Path.Combine(staticDataPath, "resourcepacks", "vanilla.zip")))
        {
            Console.Error.WriteLine("Resource pack file does not exist");
            return 1;
        }

        builder.AddServiceDefaults();
        builder.WebHost.UseKestrelHttpsConfiguration();

        using var app = builder.Build();

        // app.UseHttpsRedirection();

        app.MapMethods("/availableresourcepack/resourcepacks/dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35", ["GET", "HEAD"], Handler);

        app.Run();

        return 0;
    }

    private static Results<BadRequest, PhysicalFileHttpResult> Handler(HttpContext context, ILogger<App> logger)
    {
        string resourcePackFilePath = Path.Combine(staticDataPath, "resourcepacks", "vanilla.zip"); //resource packs are distributed as renamed zip files containing an MCpack

        if (!System.IO.File.Exists(resourcePackFilePath))
        {
            Logs.LogResourcepackNotFound(logger);
            return TypedResults.BadRequest(); //we cannot serve you.
        }

        return TypedResults.PhysicalFile(
            path: resourcePackFilePath,
            contentType: "application/octet-stream",
            fileDownloadName: "dba38e59-091a-4826-b76a-a08d7de5a9e2-1301b0c257a311678123b9e7325d0d6c61db3c35",
            enableRangeProcessing: true
        );
    }
}

internal static partial class Logs
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Resource pack file not found")]
    public static partial void LogResourcepackNotFound(ILogger logger);
}
