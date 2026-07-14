using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Postgres.Migrations;

/// <inheritdoc />
public partial class AddDbTriggersForAccountVersions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateAccountVersionFunction(migrationBuilder, "increment_profile_version", "Profile", "Id");
        CreateAccountVersionTrigger(migrationBuilder, "Profiles", "trg_profiles_version", "increment_profile_version");

        CreateAccountVersionFunction(migrationBuilder, "increment_inventory_version", "Inventory", "AccountId");
        CreateAccountVersionTrigger(migrationBuilder, "StackableItems", "trg_stackable_items_version", "increment_inventory_version");
        CreateAccountVersionTrigger(migrationBuilder, "NonStackableItemInstances", "trg_non_stackable_items_version", "increment_inventory_version");

        CreateAccountVersionFunction(migrationBuilder, "increment_crafting_version", "Crafting", "Id");
        CreateAccountVersionTrigger(migrationBuilder, "CraftingSlots", "trg_crafting_slots_version", "increment_crafting_version");

        CreateAccountVersionFunction(migrationBuilder, "increment_smelting_version", "Smelting", "Id");
        CreateAccountVersionTrigger(migrationBuilder, "SmeltingSlots", "trg_smelting_slots_version", "increment_smelting_version");

        CreateAccountVersionFunction(migrationBuilder, "increment_boosts_version", "Boosts", "Id");
        CreateAccountVersionTrigger(migrationBuilder, "Boosts", "trg_boosts_version", "increment_boosts_version");

        CreateAccountVersionFunction(migrationBuilder, "increment_buildplates_version", "Buildplates", "AccountId");
        CreateAccountVersionTrigger(migrationBuilder, "PlayerBuildplates", "trg_buildplates_version", "increment_buildplates_version");

        CreateAccountVersionFunction(migrationBuilder, "increment_journal_version", "Journal", "AccountId");
        CreateAccountVersionTrigger(migrationBuilder, "JournalEntries", "trg_journal_entries_version", "increment_journal_version");

        CreateAccountVersionFunction(migrationBuilder, "increment_tokens_version", "Tokens", "AccountId");
        CreateAccountVersionTrigger(migrationBuilder, "Tokens", "trg_tokens_version", "increment_tokens_version");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropAccountVersionTrigger(migrationBuilder, "Tokens", "trg_tokens_version");
        DropAccountVersionFunction(migrationBuilder, "increment_tokens_version");

        DropAccountVersionTrigger(migrationBuilder, "JournalEntries", "trg_journal_entries_version");
        DropAccountVersionFunction(migrationBuilder, "increment_journal_version");

        DropAccountVersionTrigger(migrationBuilder, "PlayerBuildplates", "trg_buildplates_version");
        DropAccountVersionFunction(migrationBuilder, "increment_buildplates_version");

        DropAccountVersionTrigger(migrationBuilder, "Boosts", "trg_boosts_version");
        DropAccountVersionFunction(migrationBuilder, "increment_boosts_version");

        DropAccountVersionTrigger(migrationBuilder, "SmeltingSlots", "trg_smelting_slots_version");
        DropAccountVersionFunction(migrationBuilder, "increment_smelting_version");

        DropAccountVersionTrigger(migrationBuilder, "CraftingSlots", "trg_crafting_slots_version");
        DropAccountVersionFunction(migrationBuilder, "increment_crafting_version");

        DropAccountVersionTrigger(migrationBuilder, "NonStackableItemInstances", "trg_non_stackable_items_version");
        DropAccountVersionTrigger(migrationBuilder, "StackableItems", "trg_stackable_items_version");
        DropAccountVersionFunction(migrationBuilder, "increment_inventory_version");

        DropAccountVersionTrigger(migrationBuilder, "Profiles", "trg_profiles_version");
        DropAccountVersionFunction(migrationBuilder, "increment_profile_version");
    }

    private static void CreateAccountVersionFunction(MigrationBuilder migrationBuilder, string functionName, string columnName, string idColumnName)
        => migrationBuilder.Sql($"""
            CREATE OR REPLACE FUNCTION {functionName}()
            RETURNS TRIGGER AS $$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    UPDATE "AccountVersions"
                    SET "{columnName}" = "{columnName}" + 1
                    WHERE "Id" = OLD."{idColumnName}";
                ELSE
                    UPDATE "AccountVersions"
                    SET "{columnName}" = "{columnName}" + 1
                    WHERE "Id" = NEW."{idColumnName}";
                END IF;
                RETURN NULL; 
            END;
            $$ LANGUAGE plpgsql;
        """);

    private static void CreateAccountVersionTrigger(MigrationBuilder migrationBuilder, string tableName, string triggerName, string functionName)
        => migrationBuilder.Sql($"""
            CREATE TRIGGER {triggerName}
            AFTER INSERT OR UPDATE OR DELETE ON "{tableName}"
            FOR EACH ROW
            EXECUTE FUNCTION {functionName}();
        """);

    private static void DropAccountVersionTrigger(MigrationBuilder migrationBuilder, string tableName, string triggerName)
        => migrationBuilder.Sql($"""
            DROP TRIGGER IF EXISTS "{triggerName}" ON "{tableName}";
            """);

    private static void DropAccountVersionFunction(MigrationBuilder migrationBuilder, string functionName)
        => migrationBuilder.Sql($"""
            DROP FUNCTION IF EXISTS {functionName}();
            """);
}
