using static Solace.ApiServer.Types.Catalog.ItemsCatalogEfficiencyCategory;

namespace Solace.ApiServer.Types.Catalog;

internal sealed record ItemsCatalogEfficiencyCategory(
    EfficiencyMapR EfficiencyMap
)
{
    internal sealed record EfficiencyMapR(
        float Hand,
        float Hoe,
        float Axe,
        float Shovel,
#pragma warning disable CA1707 // Identifiers should not contain underscores
        float Pickaxe_1,
        float Pickaxe_2,
        float Pickaxe_3,
        float Pickaxe_4,
        float Pickaxe_5,
#pragma warning restore CA1707 // Identifiers should not contain underscores
        float Sword,
        float Sheers
    );
}
