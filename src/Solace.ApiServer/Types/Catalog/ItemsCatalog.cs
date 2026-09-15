namespace Solace.ApiServer.Types.Catalog;

internal sealed record ItemsCatalog(
    ItemsCatalogItem[] Items,
    Dictionary<string, ItemsCatalogEfficiencyCategory> EfficiencyCategories
);
