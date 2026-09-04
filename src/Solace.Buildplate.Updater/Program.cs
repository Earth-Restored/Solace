using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Solace.Common;
using Solace.EventBus.Client;

namespace Solace.Buildplate.Updater;

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

        var builder = WebApplication.CreateSlimBuilder(args);

        if (!builder.Configuration.GetValue<bool>("AcceptMinecraftEula", false))
        {
            Console.Write("Error: you must accept the minecraft eula, change AcceptMinecraftEula to true");
            return;
        }

        builder.AddServiceDefaults();
        builder.WebHost.UseKestrelHttpsConfiguration();

        builder.Services.AddSingleton<StartupDependencies>();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<StartupDependencies>().EventBus);
        builder.Services.AddSingleton<BuildplateUpdater>();
        builder.Services.AddSingleton<EventBusBuildplateUpdater>();

        using var app = builder.Build();

        using var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        GlobalLoggerFactory.Initialize(loggerFactory);

        var programLogger = loggerFactory.CreateLogger(nameof(Program));

        app.MapDefaultEndpoints();

        var eventBusConnectionString = builder.Configuration["services:event-bus:grpc:0"];
        Debug.Assert(eventBusConnectionString is not null);

        LogConnectingToEventBus(programLogger);
        EventBusClient eventBusClient;
        try
        {
            eventBusClient = await EventBusClient.ConnectAsync(eventBusConnectionString, programLogger);
        }
        catch (Exception exception)
        {
            LogConnectToEventBusError(programLogger, exception);
            loggerFactory.Dispose();
            return;
        }

        LogConnectedToEventBus(programLogger);

        // init stuff that requires logger but needs to be injected
        var startupDeps = app.Services.GetRequiredService<StartupDependencies>();
        startupDeps.EventBus = eventBusClient;

        await app.RunAsync();
    }

    internal sealed class StartupDependencies
    {
        public EventBusClient EventBus { get; set; } = null!;
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to event bus")]
    private static partial void LogConnectingToEventBus(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Could not connect to event bus")]
    private static partial void LogConnectToEventBusError(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to event bus")]
    private static partial void LogConnectedToEventBus(ILogger logger);
}