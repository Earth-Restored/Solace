namespace Solace.ApiServer.Types.Catalog;

internal sealed record JournalCatalog(
    Dictionary<string, JournalCatalogItem> Items
);
