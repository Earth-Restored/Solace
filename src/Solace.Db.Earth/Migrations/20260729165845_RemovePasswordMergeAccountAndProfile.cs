using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class RemovePasswordMergeAccountAndProfile : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AccountVersions_Accounts_Id",
            table: "AccountVersions");

        migrationBuilder.DropForeignKey(
            name: "FK_ActivityLogs_Accounts_AccountId",
            table: "ActivityLogs");

        migrationBuilder.DropForeignKey(
            name: "FK_Boosts_Accounts_Id",
            table: "Boosts");

        migrationBuilder.DropForeignKey(
            name: "FK_CraftingSlots_Accounts_Id",
            table: "CraftingSlots");

        migrationBuilder.DropForeignKey(
            name: "FK_Hotbars_Accounts_Id",
            table: "Hotbars");

        migrationBuilder.DropForeignKey(
            name: "FK_JournalEntries_Accounts_AccountId",
            table: "JournalEntries");

        migrationBuilder.DropForeignKey(
            name: "FK_NonStackableItems_Accounts_AccountId",
            table: "NonStackableItems");

        migrationBuilder.DropForeignKey(
            name: "FK_PlayerBuildplates_Accounts_AccountId",
            table: "PlayerBuildplates");

        migrationBuilder.DropForeignKey(
            name: "FK_Profiles_Accounts_Id",
            table: "Profiles");

        migrationBuilder.DropForeignKey(
            name: "FK_RedeemedTappables_Accounts_AccountId",
            table: "RedeemedTappables");

        migrationBuilder.DropForeignKey(
            name: "FK_SharedBuildplates_Accounts_AccountId",
            table: "SharedBuildplates");

        migrationBuilder.DropForeignKey(
            name: "FK_SmeltingSlots_Accounts_Id",
            table: "SmeltingSlots");

        migrationBuilder.DropForeignKey(
            name: "FK_StackableItems_Accounts_AccountId",
            table: "StackableItems");

        migrationBuilder.DropForeignKey(
            name: "FK_Tokens_Accounts_AccountId",
            table: "Tokens");

        migrationBuilder.DropTable(
            name: "Accounts");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "Tokens",
            newName: "ProfileId");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "StackableItems",
            newName: "ProfileId");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "SharedBuildplates",
            newName: "ProfileId");

        migrationBuilder.RenameIndex(
            name: "IX_SharedBuildplates_AccountId",
            table: "SharedBuildplates",
            newName: "IX_SharedBuildplates_ProfileId");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "RedeemedTappables",
            newName: "ProfileId");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "PlayerBuildplates",
            newName: "ProfileId");

        migrationBuilder.RenameIndex(
            name: "IX_PlayerBuildplates_AccountId",
            table: "PlayerBuildplates",
            newName: "IX_PlayerBuildplates_ProfileId");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "NonStackableItems",
            newName: "ProfileId");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "JournalEntries",
            newName: "ProfileId");

        migrationBuilder.RenameColumn(
            name: "AccountId",
            table: "ActivityLogs",
            newName: "ProfileId");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedDate",
            table: "Profiles",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<bool>(
            name: "IsSkinSlim",
            table: "Profiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "ProfilePictureUrl",
            table: "Profiles",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "SkinImageData",
            table: "Profiles",
            type: "bytea",
            maxLength: 16384,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Username",
            table: "Profiles",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "WebPortalAccountId",
            table: "Profiles",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Profiles_Username",
            table: "Profiles",
            column: "Username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Profiles_WebPortalAccountId",
            table: "Profiles",
            column: "WebPortalAccountId");

        migrationBuilder.AddForeignKey(
            name: "FK_AccountVersions_Profiles_Id",
            table: "AccountVersions",
            column: "Id",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_ActivityLogs_Profiles_ProfileId",
            table: "ActivityLogs",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Boosts_Profiles_Id",
            table: "Boosts",
            column: "Id",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_CraftingSlots_Profiles_Id",
            table: "CraftingSlots",
            column: "Id",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Hotbars_Profiles_Id",
            table: "Hotbars",
            column: "Id",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_JournalEntries_Profiles_ProfileId",
            table: "JournalEntries",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_NonStackableItems_Profiles_ProfileId",
            table: "NonStackableItems",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_PlayerBuildplates_Profiles_ProfileId",
            table: "PlayerBuildplates",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_RedeemedTappables_Profiles_ProfileId",
            table: "RedeemedTappables",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_SharedBuildplates_Profiles_ProfileId",
            table: "SharedBuildplates",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_SmeltingSlots_Profiles_Id",
            table: "SmeltingSlots",
            column: "Id",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_StackableItems_Profiles_ProfileId",
            table: "StackableItems",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Tokens_Profiles_ProfileId",
            table: "Tokens",
            column: "ProfileId",
            principalTable: "Profiles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AccountVersions_Profiles_Id",
            table: "AccountVersions");

        migrationBuilder.DropForeignKey(
            name: "FK_ActivityLogs_Profiles_ProfileId",
            table: "ActivityLogs");

        migrationBuilder.DropForeignKey(
            name: "FK_Boosts_Profiles_Id",
            table: "Boosts");

        migrationBuilder.DropForeignKey(
            name: "FK_CraftingSlots_Profiles_Id",
            table: "CraftingSlots");

        migrationBuilder.DropForeignKey(
            name: "FK_Hotbars_Profiles_Id",
            table: "Hotbars");

        migrationBuilder.DropForeignKey(
            name: "FK_JournalEntries_Profiles_ProfileId",
            table: "JournalEntries");

        migrationBuilder.DropForeignKey(
            name: "FK_NonStackableItems_Profiles_ProfileId",
            table: "NonStackableItems");

        migrationBuilder.DropForeignKey(
            name: "FK_PlayerBuildplates_Profiles_ProfileId",
            table: "PlayerBuildplates");

        migrationBuilder.DropForeignKey(
            name: "FK_RedeemedTappables_Profiles_ProfileId",
            table: "RedeemedTappables");

        migrationBuilder.DropForeignKey(
            name: "FK_SharedBuildplates_Profiles_ProfileId",
            table: "SharedBuildplates");

        migrationBuilder.DropForeignKey(
            name: "FK_SmeltingSlots_Profiles_Id",
            table: "SmeltingSlots");

        migrationBuilder.DropForeignKey(
            name: "FK_StackableItems_Profiles_ProfileId",
            table: "StackableItems");

        migrationBuilder.DropForeignKey(
            name: "FK_Tokens_Profiles_ProfileId",
            table: "Tokens");

        migrationBuilder.DropIndex(
            name: "IX_Profiles_Username",
            table: "Profiles");

        migrationBuilder.DropIndex(
            name: "IX_Profiles_WebPortalAccountId",
            table: "Profiles");

        migrationBuilder.DropColumn(
            name: "CreatedDate",
            table: "Profiles");

        migrationBuilder.DropColumn(
            name: "IsSkinSlim",
            table: "Profiles");

        migrationBuilder.DropColumn(
            name: "ProfilePictureUrl",
            table: "Profiles");

        migrationBuilder.DropColumn(
            name: "SkinImageData",
            table: "Profiles");

        migrationBuilder.DropColumn(
            name: "Username",
            table: "Profiles");

        migrationBuilder.DropColumn(
            name: "WebPortalAccountId",
            table: "Profiles");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "Tokens",
            newName: "AccountId");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "StackableItems",
            newName: "AccountId");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "SharedBuildplates",
            newName: "AccountId");

        migrationBuilder.RenameIndex(
            name: "IX_SharedBuildplates_ProfileId",
            table: "SharedBuildplates",
            newName: "IX_SharedBuildplates_AccountId");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "RedeemedTappables",
            newName: "AccountId");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "PlayerBuildplates",
            newName: "AccountId");

        migrationBuilder.RenameIndex(
            name: "IX_PlayerBuildplates_ProfileId",
            table: "PlayerBuildplates",
            newName: "IX_PlayerBuildplates_AccountId");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "NonStackableItems",
            newName: "AccountId");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "JournalEntries",
            newName: "AccountId");

        migrationBuilder.RenameColumn(
            name: "ProfileId",
            table: "ActivityLogs",
            newName: "AccountId");

        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FirstName = table.Column<string>(type: "text", nullable: true),
                IsSkinSlim = table.Column<bool>(type: "boolean", nullable: false),
                LastName = table.Column<string>(type: "text", nullable: true),
                PasswordHash = table.Column<byte[]>(type: "bytea", maxLength: 64, nullable: false),
                PasswordSalt = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: false),
                ProfilePictureUrl = table.Column<string>(type: "text", nullable: true),
                SkinImageData = table.Column<byte[]>(type: "bytea", maxLength: 16384, nullable: true),
                Username = table.Column<string>(type: "text", nullable: true),
                WebPortalAccountId = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Accounts", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Accounts_Username",
            table: "Accounts",
            column: "Username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Accounts_WebPortalAccountId",
            table: "Accounts",
            column: "WebPortalAccountId");

        migrationBuilder.AddForeignKey(
            name: "FK_AccountVersions_Accounts_Id",
            table: "AccountVersions",
            column: "Id",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_ActivityLogs_Accounts_AccountId",
            table: "ActivityLogs",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Boosts_Accounts_Id",
            table: "Boosts",
            column: "Id",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_CraftingSlots_Accounts_Id",
            table: "CraftingSlots",
            column: "Id",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Hotbars_Accounts_Id",
            table: "Hotbars",
            column: "Id",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_JournalEntries_Accounts_AccountId",
            table: "JournalEntries",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_NonStackableItems_Accounts_AccountId",
            table: "NonStackableItems",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_PlayerBuildplates_Accounts_AccountId",
            table: "PlayerBuildplates",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Profiles_Accounts_Id",
            table: "Profiles",
            column: "Id",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_RedeemedTappables_Accounts_AccountId",
            table: "RedeemedTappables",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_SharedBuildplates_Accounts_AccountId",
            table: "SharedBuildplates",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_SmeltingSlots_Accounts_Id",
            table: "SmeltingSlots",
            column: "Id",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_StackableItems_Accounts_AccountId",
            table: "StackableItems",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_Tokens_Accounts_AccountId",
            table: "Tokens",
            column: "AccountId",
            principalTable: "Accounts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}
