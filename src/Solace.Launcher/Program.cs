using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Solace.Launcher;

if (!Debugger.IsAttached)
{
    AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
    {
        Console.Error.WriteLine($"Unhandled exception: {e.ExceptionObject}");

        Console.Out.Flush();
        Console.Error.Flush();

        Environment.Exit(1);
    };
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<Starter>();
builder.Services.AddSingleton<UIManager>();

using var host = builder.Build();

var uiManager = host.Services.GetRequiredService<UIManager>();

await uiManager.RunAsync();