namespace Solace.WebPortal.Common.Features.Data;

public sealed record DataArchiveManifest(
    string FormatVersion,
    string ApplicationVersion,
    DateTimeOffset ExportedAt,
    IReadOnlyList<string> ObjectIds,
    IReadOnlyDictionary<string, string> Databases
);
