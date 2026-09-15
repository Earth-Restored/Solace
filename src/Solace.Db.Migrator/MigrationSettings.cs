using System.ComponentModel;
using Spectre.Console.Cli;

namespace Solace.Db.Migrator;

public sealed class MigrationSettings : CommandSettings
{
    [CommandOption("--skip-intro")]
    [Description("Skip the introductory panel and confirmation pause.")]
    public bool SkipIntro { get; init; }

    [CommandOption("-p|--old-path <PATH>")]
    [Description("Path to old installation folder.")]
    public string? OldPath { get; init; }

    [CommandOption("--host <HOST>")]
    [Description("PostgreSQL Host.")]
    public string? PostgresHost { get; init; }

    [CommandOption("--port <PORT>")]
    [Description("PostgreSQL Port.")]
    public int? PostgresPort { get; init; }

    [CommandOption("-u|--user <USER>")]
    [Description("PostgreSQL User.")]
    public string? PostgresUser { get; init; }

    [CommandOption("--password <PASSWORD>")]
    [Description("PostgreSQL Password.")]
    public string? PostgresPassword { get; init; }

    [CommandOption("--endpoint <ENDPOINT>")]
    [Description("Object Store Endpoint.")]
    public string? ObjectStoreEndpoint { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompt and start migration immediately.")]
    public bool AutoConfirm { get; init; }
}
