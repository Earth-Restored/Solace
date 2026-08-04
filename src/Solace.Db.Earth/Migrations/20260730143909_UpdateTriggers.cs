using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class UpdateTriggers : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AccountVersions_Profiles_Id",
            table: "AccountVersions");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AccountVersions",
            table: "AccountVersions");

        migrationBuilder.RenameTable(
            name: "AccountVersions",
            newName: "ProfileVersions");

        migrationBuilder.AddPrimaryKey(
            name: "PK_ProfileVersions",
            table: "ProfileVersions",
            column: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_ProfileVersions_Profiles_Id",
            table: "ProfileVersions",
            column: "Id",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        CreateVersionFunction(migrationBuilder, "increment_profile_version", "Profile", "Id", "ProfileVersions");
        CreateVersionFunction(migrationBuilder, "increment_inventory_version", "Inventory", "ProfileId", "ProfileVersions");
        CreateVersionFunction(migrationBuilder, "increment_crafting_version", "Crafting", "Id", "ProfileVersions");
        CreateVersionFunction(migrationBuilder, "increment_smelting_version", "Smelting", "Id", "ProfileVersions");
        CreateVersionFunction(migrationBuilder, "increment_boosts_version", "Boosts", "Id", "ProfileVersions");
        CreateVersionFunction(migrationBuilder, "increment_buildplates_version", "Buildplates", "ProfileId", "ProfileVersions");
        CreateVersionFunction(migrationBuilder, "increment_journal_version", "Journal", "ProfileId", "ProfileVersions");
        CreateVersionFunction(migrationBuilder, "increment_tokens_version", "Tokens", "ProfileId", "ProfileVersions");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        CreateVersionFunction(migrationBuilder, "increment_profile_version", "Profile", "Id", "AccountVersions");
        CreateVersionFunction(migrationBuilder, "increment_inventory_version", "Inventory", "AccountId", "AccountVersions");
        CreateVersionFunction(migrationBuilder, "increment_crafting_version", "Crafting", "Id", "AccountVersions");
        CreateVersionFunction(migrationBuilder, "increment_smelting_version", "Smelting", "Id", "AccountVersions");
        CreateVersionFunction(migrationBuilder, "increment_boosts_version", "Boosts", "Id", "AccountVersions");
        CreateVersionFunction(migrationBuilder, "increment_buildplates_version", "Buildplates", "AccountId", "AccountVersions");
        CreateVersionFunction(migrationBuilder, "increment_journal_version", "Journal", "AccountId", "AccountVersions");
        CreateVersionFunction(migrationBuilder, "increment_tokens_version", "Tokens", "AccountId", "AccountVersions");

        migrationBuilder.DropForeignKey(
            name: "FK_ProfileVersions_Profiles_Id",
            table: "ProfileVersions");

        migrationBuilder.DropPrimaryKey(
            name: "PK_ProfileVersions",
            table: "ProfileVersions");

        migrationBuilder.RenameTable(
            name: "ProfileVersions",
            newName: "AccountVersions");

        migrationBuilder.AddPrimaryKey(
            name: "PK_AccountVersions",
            table: "AccountVersions",
            column: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_AccountVersions_Profiles_Id",
            table: "AccountVersions",
            column: "Id",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    private static void CreateVersionFunction(
        MigrationBuilder migrationBuilder,
        string functionName,
        string columnName,
        string idColumnName,
        string versionTableName = "ProfileVersions")
        => migrationBuilder.Sql($"""
            CREATE OR REPLACE FUNCTION {functionName}()
            RETURNS TRIGGER AS $$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    UPDATE "{versionTableName}"
                    SET "{columnName}" = "{columnName}" + 1
                    WHERE "Id" = OLD."{idColumnName}";
                ELSE
                    UPDATE "{versionTableName}"
                    SET "{columnName}" = "{columnName}" + 1
                    WHERE "Id" = NEW."{idColumnName}";
                END IF;
                RETURN NULL; 
            END;
            $$ LANGUAGE plpgsql;
            """);
}
