using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Solace.Common;

namespace Solace.Buildplate.ServerSetup;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
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

        // ProcessStartInfo.KillOnParentExit currently only supported on these
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid())
        {
            Console.WriteLine("Unsupported OS, supported: windows, linux, android");
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);

        if (!builder.Configuration.GetValue<bool>("AcceptMinecraftEula", false))
        {
            Console.Write("Error: you must accept the minecraft eula, change AcceptMinecraftEula to true");
            return;
        }

        builder.AddServiceDefaults();

        builder.Services.AddSingleton<SetupService>();

        using var app = builder.Build();

        using var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        try
        {
            await app.Services.GetRequiredService<SetupService>().SetupAsync();
        }
        catch (IOException exception)
        {
            LogFatalErrorDuringServerSetup(programLogger, exception);
            loggerFactory.Dispose();
            return;
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Fatal error during server setup")]
    private static partial void LogFatalErrorDuringServerSetup(ILogger logger, Exception exception);
}
