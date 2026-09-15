namespace Solace.ApiServer.Types.Catalog;

internal sealed record RecipesCatalog(
    RecipesCatalog.CraftingRecipe[] Crafting,
    RecipesCatalog.SmeltingRecipe[] Smelting
)
{
    internal sealed record CraftingRecipe(
        Guid Id,
        string Category,
        string Duration,
        CraftingRecipeIngredient[] Ingredients,
        CraftingRecipeOutput Output,
        CraftingRecipeReturnItem[] ReturnItems,
        bool Deprecated
    );

    internal sealed record CraftingRecipeIngredient(
        Guid[] Items,
        int Quantity
    );

    internal sealed record CraftingRecipeOutput(
        Guid ItemId,
        int Quantity
    );

    internal sealed record CraftingRecipeReturnItem(
        Guid Id,
        int Amount
    );

    internal sealed record SmeltingRecipe(
        Guid Id,
        int HeatRequired,
        Guid InputItemId,
        SmeltingRecipeOutput Output,
        SmeltingRecipeReturnItem[] ReturnItems,
        bool Deprecated
    );

    internal sealed record SmeltingRecipeOutput(
        Guid ItemId,
        int Quantity
    );

    internal sealed record SmeltingRecipeReturnItem(
        Guid Id,
        int Amount
    );
}
