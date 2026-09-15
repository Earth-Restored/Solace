using Spectre.Console.Cli;

namespace Solace.Db.Migrator;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var app = new CommandApp<MigrateCommand>();
        return await app.RunAsync(args);
    }
}
