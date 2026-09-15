namespace Solace.ApiServer.Types.Catalog;

internal sealed record JournalCatalogItem(
    string ReferenceId,
    string ParentCollection,
    int OverallOrder,
    int CollectionOrder,
    string? DefaultSound,
    bool Deprecated,
    string ToolsVersion
);
