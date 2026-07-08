using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Solace.Common;
using Solace.ObjectStore.Server.Services;
using System.Diagnostics;
using System.Runtime.Loader;

namespace Solace.ObjectStore.Server;

internal static class Program
{
    private static Task<int> Main(string[] args)
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

        return App.RunAsync(args);
    }
}

internal static partial class App
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) =>
            {
                Console.Error.WriteLine($"Unhandled exception: {e.ExceptionObject}");

                try
                {
                    var logger = GlobalLoggerFactory.CreateLogger(nameof(Program));
                    LogUnhandledException(logger, e.ExceptionObject as Exception);
                }
                catch
                {
                    Console.Error.WriteLine($"Unhandled exception before logger initialization");
                }

                Console.Out.Flush();
                Console.Error.Flush();

                Environment.Exit(2);
            };
        }

        var builder = WebApplication.CreateSlimBuilder(args);

        builder.AddServiceDefaults();

        builder.Services.AddGrpc();

        string dataDirectory = Path.GetFullPath(builder.Configuration.GetValue<string>("DataDirectory", "data/object_store"));

        builder.Services.AddSingleton<DataStore>(new DataStore(new DirectoryInfo(dataDirectory)));

        using var app = builder.Build();

        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var logger = loggerFactory.CreateLogger(nameof(Program));
        LogDataStoragePath(logger, dataDirectory);

        app.MapGrpcService<ObjectStoreServiceImpl>();

        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

        await app.RunAsync();

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using {Path} for data storage")]
    private static partial void LogDataStoragePath(ILogger logger, string Path);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Fatal error during server startup")]
    private static partial void LogFatalErrorDuringServerStartup(ILogger logger, Exception exception);
}
