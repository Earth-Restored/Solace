using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Solace.StaticData;

public sealed class Catalog
{
    public ItemsCatalogR ItemsCatalog { get; }
    public ItemEfficiencyCategoriesCatalogR ItemEfficiencyCategoriesCatalog { get; }
    public ItemJournalGroupsCatalogR ItemJournalGroupsCatalog { get; }
    public RecipesCatalogR RecipesCatalog { get; }
    public NFCBoostsCatalogR NfcBoostsCatalog { get; }

    internal Catalog(string dir)
    {
        try
        {
            ItemsCatalog = new ItemsCatalogR(Path.Combine(dir, "items.json"));
            ItemEfficiencyCategoriesCatalog = new ItemEfficiencyCategoriesCatalogR(Path.Combine(dir, "itemEfficiencyCategories.json"));
            ItemJournalGroupsCatalog = new ItemJournalGroupsCatalogR(Path.Combine(dir, "itemJournalGroups.json"));
            RecipesCatalog = new RecipesCatalogR(Path.Combine(dir, "recipes.json"));
            NfcBoostsCatalog = new NFCBoostsCatalogR(Path.Combine(dir, "nfc.json"));
        }
        catch (StaticDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new StaticDataException(null, exception);
        }
    }

    public sealed class ItemsCatalogR
    {
        public readonly ImmutableArray<Item> Items;

        private readonly Dictionary<Guid, Item> itemsById = [];

        internal ItemsCatalogR(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var items = JsonSerializer.Deserialize(stream, AppJsonContext.Default.ItemArray);

                Debug.Assert(items is not null);

                Items = ImmutableCollectionsMarshal.AsImmutableArray(items);
            }

            HashSet<Guid> ids = [];
            HashSet<string> names = [];
            foreach (var item in Items)
            {
                if (!ids.Add(item.Id))
                {
                    throw new StaticDataException($"Duplicate item ID {item.Id}");
                }

                if (!names.Add(item.Name + "." + item.Aux))
                {
                    throw new StaticDataException($"Duplicate item name/aux {item.Name} {item.Aux}");
                }
            }

            foreach (var item in Items)
            {
                itemsById[item.Id] = item;
            }
        }

        public Item? GetItem(Guid id)
            => itemsById.GetValueOrDefault(id);

        public bool TryGetItem(Guid id, [MaybeNullWhen(false)] out Item item)
            => itemsById.TryGetValue(id, out item);

        [JsonSerializable(typeof(Item), TypeInfoPropertyName = "ItemCatalogItem")]
        public sealed record Item(
            Guid Id,
            string Name,
            int Aux,
            bool Stackable,
            Item.TypeE Type,
            Item.CategoryE Category,
            Item.RarityE Rarity,
            Item.UseTypeE UseType,
            Item.UseTypeE AlternativeUseType,
            Item.BlockInfoR? BlockInfo,
            Item.ToolInfoR? ToolInfo,
            Item.ConsumeInfoR? ConsumeInfo,
            Item.FuelInfoR? FuelInfo,
            Item.ProjectileInfoR? ProjectileInfo,
            Item.MobInfoR? MobInfo,
            Item.BoostInfoR? BoostInfo,
            Item.JournalEntryR? JournalEntry,
            Item.ExperienceR Experience
        )
        {
            [JsonConverter(typeof(JsonStringEnumConverter<TypeE>))]
            public enum TypeE
            {
#pragma warning disable CA1707 // Identifiers should not contain underscores
                BLOCK,
                ITEM,
                TOOL,
                MOB,
                ENVIRONMENT_BLOCK,
                BOOST,
                ADVENTURE_SCROLL,
#pragma warning restore CA1707 // Identifiers should not contain underscores
            }

            [JsonConverter(typeof(JsonStringEnumConverter<CategoryE>))]
            public enum CategoryE
            {
#pragma warning disable CA1707 // Identifiers should not contain underscores
                CONSTRUCTION,
                EQUIPMENT,
                ITEMS,
                MOBS,
                NATURE,
                BOOST_ADVENTURE_XP,
                BOOST_CRAFTING,
                BOOST_DEFENSE,
                BOOST_EATING,
                BOOST_HEALTH,
                BOOST_HOARDING,
                BOOST_ITEM_XP,
                BOOST_MINING_SPEED,
                BOOST_RETENTION,
                BOOST_SMELTING,
                BOOST_STRENGTH,
                BOOST_TAPPABLE_RADIUS,
#pragma warning restore CA1707 // Identifiers should not contain underscores
            }

            [JsonConverter(typeof(JsonStringEnumConverter<RarityE>))]
            public enum RarityE
            {
                COMMON,
                UNCOMMON,
                RARE,
                EPIC,
                LEGENDARY,
                OOBE,
            }

            [JsonConverter(typeof(JsonStringEnumConverter<UseTypeE>))]
            public enum UseTypeE
            {
#pragma warning disable CA1707 // Identifiers should not contain underscores
                NONE,
                BUILD,
                BUILD_ATTACK,
                INTERACT,
                INTERACT_AND_BUILD,
                DESTROY,
                USE,
                CONSUME,
#pragma warning restore CA1707 // Identifiers should not contain underscores
            }

            public sealed record BlockInfoR(
                int BreakingHealth,
                string? EfficiencyCategory
            );

            public sealed record ToolInfoR(
                int BlockDamage,
                int MobDamage,
                int MaxWear,
                string? EfficiencyCategory
            );

            public sealed record ConsumeInfoR(
                int Heal,
                Guid? ReturnItemId
            );

            public sealed record FuelInfoR(
                int BurnTime,
                int HeatPerSecond,
                Guid? ReturnItemId
            );

            public sealed record ProjectileInfoR(
                int MobDamage
            );

            public sealed record MobInfoR(
                int Health
            );

            public sealed record BoostInfoR(
                string Name,
                int? Level,
                BoostInfoType Type,
                bool CanBeRemoved,
                long Duration,
                bool TriggeredOnDeath,
                BoostEffect[] Effects
            );

            public sealed record BoostEffect(
                BoostEffectType Type,
                int Value,
                Guid[] ApplicableItemIds,
                BoostEffectActivation Activation
            );

            [JsonConverter(typeof(JsonStringEnumConverter<BoostEffectType>))]
            public enum BoostEffectType
            {
#pragma warning disable CA1707 // Identifiers should not contain underscores
                ADVENTURE_XP,
                CRAFTING,
                DEFENSE,
                EATING,
                HEALING,
                HEALTH,
                ITEM_XP,
                MINING_SPEED,
                RETENTION_BACKPACK,
                RETENTION_HOTBAR,
                RETENTION_XP,
                SMELTING,
                STRENGTH,
                TAPPABLE_RADIUS,
#pragma warning restore CA1707 // Identifiers should not contain underscores
            }

            [JsonConverter(typeof(JsonStringEnumConverter<BoostEffectActivation>))]
            public enum BoostEffectActivation
            {
                INSTANT,
                TIMED,
                TRIGGERED,
            }

            [JsonConverter(typeof(JsonStringEnumConverter<BoostInfoType>))]
            public enum BoostInfoType
            {
#pragma warning disable CA1707 // Identifiers should not contain underscores
                POTION,
                INVENTORY_ITEM
#pragma warning restore CA1707 // Identifiers should not contain underscores
            }

            public sealed record JournalEntryR(
                string Group,
                int Order,
                JournalEntryR.BiomeE Biome,
                JournalEntryR.BehaviorE Behavior,
                string? Sound
            )
            {
                [JsonConverter(typeof(JsonStringEnumConverter<BiomeE>))]
                public enum BiomeE
                {
#pragma warning disable CA1707 // Identifiers should not contain underscores
                    NONE,
                    OVERWORLD,
                    NETHER,
                    BIRCH_FOREST,
                    DESERT,
                    FLOWER_FOREST,
                    FOREST,
                    ICE_PLAINS,
                    JUNGLE,
                    MESA,
                    MUSHROOM_ISLAND,
                    OCEAN,
                    PLAINS,
                    RIVER,
                    ROOFED_FOREST,
                    SAVANNA,
                    SUNFLOWER_PLAINS,
                    SWAMP,
                    TAIGA,
                    WARM_OCEAN,
#pragma warning restore CA1707 // Identifiers should not contain underscores
                }

                [JsonConverter(typeof(JsonStringEnumConverter<BehaviorE>))]
                public enum BehaviorE
                {
                    NONE,
                    PASSIVE,
                    HOSTILE,
                    NEUTRAL,
                }
            }

            public sealed record ExperienceR(
                int Tappable,
                int Encounter,
                int Crafting,
                int Journal    // TODO: what is this used for?
            );
        }
    }

    public sealed class ItemEfficiencyCategoriesCatalogR
    {
        public readonly ImmutableArray<EfficiencyCategory> EfficiencyCategories;

        internal ItemEfficiencyCategoriesCatalogR(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var efficiencyCategories = JsonSerializer.Deserialize(stream, AppJsonContext.Default.EfficiencyCategoryArray);

                Debug.Assert(efficiencyCategories is not null);

                EfficiencyCategories = ImmutableCollectionsMarshal.AsImmutableArray(efficiencyCategories);
            }

            HashSet<string> names = [];
            foreach (var efficiencyCategory in EfficiencyCategories)
            {
                if (!names.Add(efficiencyCategory.Name))
                {
                    throw new StaticDataException($"Duplicate efficiency category name {efficiencyCategory.Name}");
                }
            }
        }

        public sealed record EfficiencyCategory(
            string Name,
            float Hand,
            float Hoe,
            float Axe,
            float Shovel,
#pragma warning disable CA1707 // Identifiers should not contain underscores
            [property: JsonPropertyName("pickaxe_1")] float Pickaxe_1,
            [property: JsonPropertyName("pickaxe_2")] float Pickaxe_2,
            [property: JsonPropertyName("pickaxe_3")] float Pickaxe_3,
            [property: JsonPropertyName("pickaxe_4")] float Pickaxe_4,
            [property: JsonPropertyName("pickaxe_5")] float Pickaxe_5,
#pragma warning restore CA1707 // Identifiers should not contain underscores
            float Sword,
            float Sheers
        );
    }

    public sealed class ItemJournalGroupsCatalogR
    {
        public readonly ImmutableArray<JournalGroup> Groups;

        internal ItemJournalGroupsCatalogR(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var groups = JsonSerializer.Deserialize(File.ReadAllText(file), AppJsonContext.Default.JournalGroupArray);

                Debug.Assert(groups is not null);
                Groups = ImmutableCollectionsMarshal.AsImmutableArray(groups);
            }

            HashSet<string> ids = [];
            HashSet<string> names = [];
            foreach (var journalGroup in Groups)
            {
                if (!ids.Add(journalGroup.Id))
                {
                    throw new StaticDataException($"Duplicate journal group ID {journalGroup.Id}");
                }

                if (!names.Add(journalGroup.Name))
                {
                    throw new StaticDataException($"Duplicate journal group name {journalGroup.Name}");
                }
            }
        }

        public sealed record JournalGroup(
            string Id,
            string Name,
            JournalGroup.ParentCollectionE ParentCollection,
            int Order,
            string? DefaultSound
        )
        {
            [JsonConverter(typeof(JsonStringEnumConverter<ParentCollectionE>))]
            public enum ParentCollectionE
            {
#pragma warning disable CA1707 // Identifiers should not contain underscores
                BLOCKS,
                ITEMS_CRAFTED,
                ITEMS_SMELTED,
                MOBS,
#pragma warning restore CA1707 // Identifiers should not contain underscores
            }
        }
    }

    public sealed class RecipesCatalogR
    {
        public readonly ImmutableArray<CraftingRecipe> Crafting;
        public readonly ImmutableArray<SmeltingRecipe> Smelting;

        private readonly Dictionary<Guid, CraftingRecipe> craftingRecipesById = [];
        private readonly Dictionary<Guid, SmeltingRecipe> smeltingRecipesById = [];

        internal sealed record RecipesCatalogFile(
            CraftingRecipe[] Crafting,
            SmeltingRecipe[] Smelting
        );

        internal RecipesCatalogR(string file)
        {
            RecipesCatalogFile? recipesCatalogFile;
            using (var stream = File.OpenRead(file))
            {
                recipesCatalogFile = JsonSerializer.Deserialize(stream, AppJsonContext.Default.RecipesCatalogFile);
            }

            Debug.Assert(recipesCatalogFile is not null);

            Crafting = ImmutableCollectionsMarshal.AsImmutableArray(recipesCatalogFile.Crafting);
            Smelting = ImmutableCollectionsMarshal.AsImmutableArray(recipesCatalogFile.Smelting);

            HashSet<Guid> craftingIds = [];
            HashSet<Guid> smeltingIds = [];
            foreach (var craftingRecipe in Crafting)
            {
                if (!craftingIds.Add(craftingRecipe.Id))
                {
                    throw new StaticDataException($"Duplicate crafting recipe ID {craftingRecipe.Id}");
                }
            }

            foreach (var smeltingRecipe in Smelting)
            {
                if (!smeltingIds.Add(smeltingRecipe.Id))
                {
                    throw new StaticDataException($"Duplicate smelting recipe ID {smeltingRecipe.Id}");
                }
            }

            foreach (var craftingRecipe in Crafting)
            {
                craftingRecipesById[craftingRecipe.Id] = craftingRecipe;
            }

            foreach (var smeltingRecipe in Smelting)
            {
                smeltingRecipesById[smeltingRecipe.Id] = smeltingRecipe;
            }
        }

        public CraftingRecipe? GetCraftingRecipe(Guid id)
            => craftingRecipesById.GetValueOrDefault(id);

        public SmeltingRecipe? GetSmeltingRecipe(Guid id)
            => smeltingRecipesById.GetValueOrDefault(id);

        public sealed record CraftingRecipe(
            Guid Id,
            int Duration,
            CraftingRecipeCategory Category,
            CraftingRecipe.Ingredient[] Ingredients,
            CraftingRecipe.OutputR Output,
            CraftingRecipe.ReturnItem[] ReturnItems
        )
        {
            public sealed record Ingredient(
                int Count,
                Guid[] PossibleItemIds
            );

            public sealed record OutputR(
                Guid ItemId,
                int Count
            );

            public sealed record ReturnItem(
                Guid ItemId,
                int Count
            );
        }

        [JsonConverter(typeof(JsonStringEnumConverter<CraftingRecipeCategory>))]
        public enum CraftingRecipeCategory
        {
            CONSTRUCTION,
            EQUIPMENT,
            ITEMS,
            NATURE
        }

        public sealed record SmeltingRecipe(
            Guid Id,
            int HeatRequired,
            Guid Input,
            Guid Output,
            Guid? ReturnItemId
        );
    }

    public sealed class NFCBoostsCatalogR
    {
        internal sealed record NFCBoostsCatalogFile(
            NFCBoost[] MiniFigs
        );

        public readonly FrozenDictionary<string, NFCBoost> MiniFigs;

        internal NFCBoostsCatalogR(string file)
        {
            NFCBoostsCatalogFile? nfcBoostsCatalogFile;
            using (var stream = File.OpenRead(file))
            {
                nfcBoostsCatalogFile = JsonSerializer.Deserialize(stream, AppJsonContext.Default.NFCBoostsCatalogFile);
            }

            Debug.Assert(nfcBoostsCatalogFile is not null);

            MiniFigs = nfcBoostsCatalogFile.MiniFigs.ToFrozenDictionary(item => item.Id);
        }

        public sealed record NFCBoost(
            string Id,
            BoostInfo BoostMetadata,
            string Name,
            bool Deprecated,
            string ToolsVersion,
            Rewards Rewards
        );

        public sealed record BoostInfo(
            string Name,
            string Attribute,
            bool CanBeDeactivated,
            bool CanBeRemoved,
            string? ActiveDuration,
            bool Additive,
            int? Level,
            Effect[] Effects,
            string? Scenario,
            string? Cooldown
        );

        public sealed record Effect(
            string Type,
            string? Duration,
            double? Value,
            string? Unit,
            string Targets,
            Guid[] Items,
            string[] ItemScenarios,
            string Activation,
            string? ModifiesType
        );

        public sealed record Rewards(
            int? Rubies,
            int? ExperiencePoints,
            int? Level,
            Rewards.RewardItem[] Inventory,
            Guid[] Buildplates,
            Rewards.RewardChallenge[] Challenges,
            string[] PersonaItems,
            Rewards.RewardUtilityBlock[] UtilityBlocks
        )
        {
            public sealed record RewardItem(
                Guid Id,
                int Amount
            );

            public sealed record RewardChallenge(
                string Id
            );

            public sealed record RewardUtilityBlock();
        }
    }
}
