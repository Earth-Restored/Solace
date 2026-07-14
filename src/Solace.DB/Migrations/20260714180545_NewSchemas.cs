using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Solace.DB.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class NewSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Accounts_Id",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerBuildplates_Accounts_AccountId",
                table: "PlayerBuildplates");

            migrationBuilder.DropForeignKey(
                name: "FK_RedeemedTappables_Accounts_Id",
                table: "RedeemedTappables");

            migrationBuilder.DropForeignKey(
                name: "FK_Tokens_Accounts_Id",
                table: "Tokens");

            migrationBuilder.DropTable(
                name: "Inventories");

            migrationBuilder.DropTable(
                name: "Journals");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tokens",
                table: "Tokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RedeemedTappables",
                table: "RedeemedTappables");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityLogs",
                table: "ActivityLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerBuildplates",
                table: "PlayerBuildplates");

            migrationBuilder.DropColumn(
                name: "Tokens",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "Scale",
                table: "TemplateBuildplates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "SmeltingSlots");

            migrationBuilder.DropColumn(
                name: "Tappables",
                table: "RedeemedTappables");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "RedeemedTappables");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Hotbars");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "CraftingSlots");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Boosts");

            migrationBuilder.DropColumn(
                name: "Entries",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "Scale",
                table: "PlayerBuildplates");

            migrationBuilder.RenameTable(
                name: "PlayerBuildplates",
                newName: "Buildplate");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Tokens",
                newName: "TokenId");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "TemplateBuildplates",
                newName: "BlocksPerMeter");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "RedeemedTappables",
                newName: "TappableId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "ActivityLogs",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "Buildplate",
                newName: "BlocksPerMeter");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerBuildplates_AccountId",
                table: "Buildplate",
                newName: "IX_Buildplate_AccountId");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimedOn",
                table: "Tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "Tokens",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                table: "Tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Tokens",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rewards",
                table: "Tokens",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_type",
                table: "Tokens",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "ObjectStoreId",
                table: "Tiles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<long>(
                name: "Id",
                table: "Tiles",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<Guid>(
                name: "ServerDataObjectId",
                table: "TemplateBuildplates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "PreviewObjectId",
                table: "TemplateBuildplates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Slots",
                table: "SmeltingSlots",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "ServerDataObjectId",
                table: "SharedBuildplates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastViewed",
                table: "SharedBuildplates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "Hotbar",
                table: "SharedBuildplates",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Created",
                table: "SharedBuildplates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "BuildplateLastModifed",
                table: "SharedBuildplates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "RedeemedTappables",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "RedeemedTappables",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AlterColumn<string>(
                name: "Items",
                table: "Hotbars",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "ServerDataObjectId",
                table: "EncounterBuildplates",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Slots",
                table: "CraftingSlots",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ActiveBoosts",
                table: "Boosts",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<long>(
                name: "EntryId",
                table: "ActivityLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<Guid>(
                name: "ItemId",
                table: "ActivityLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "ActivityLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rewards",
                table: "ActivityLogs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Timestamp",
                table: "ActivityLogs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "entity_type",
                table: "ActivityLogs",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "ServerDataObjectId",
                table: "Buildplate",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "PreviewObjectId",
                table: "Buildplate",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LastModified",
                table: "Buildplate",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tokens",
                table: "Tokens",
                columns: new[] { "AccountId", "TokenId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_RedeemedTappables",
                table: "RedeemedTappables",
                columns: new[] { "AccountId", "TappableId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityLogs",
                table: "ActivityLogs",
                columns: new[] { "AccountId", "EntryId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Buildplate",
                table: "Buildplate",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AccountVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Profile = table.Column<int>(type: "integer", nullable: false),
                    Inventory = table.Column<int>(type: "integer", nullable: false),
                    Crafting = table.Column<int>(type: "integer", nullable: false),
                    Smelting = table.Column<int>(type: "integer", nullable: false),
                    Boosts = table.Column<int>(type: "integer", nullable: false),
                    Buildplates = table.Column<int>(type: "integer", nullable: false),
                    Journal = table.Column<int>(type: "integer", nullable: false),
                    Challenges = table.Column<int>(type: "integer", nullable: false),
                    Tokens = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountVersions_Accounts_Id",
                        column: x => x.Id,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AmountCollected = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => new { x.AccountId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_JournalEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NonStackableItems",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Wear = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NonStackableItems", x => new { x.AccountId, x.ItemId, x.InstanceId });
                    table.ForeignKey(
                        name: "FK_NonStackableItems_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StackableItems",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StackableItems", x => new { x.AccountId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_StackableItems_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_Date",
                table: "Tokens",
                column: "Date");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Accounts_AccountId",
                table: "ActivityLogs",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Buildplate_Accounts_AccountId",
                table: "Buildplate",
                column: "AccountId",
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
                name: "FK_Tokens_Accounts_AccountId",
                table: "Tokens",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Accounts_AccountId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Buildplate_Accounts_AccountId",
                table: "Buildplate");

            migrationBuilder.DropForeignKey(
                name: "FK_RedeemedTappables_Accounts_AccountId",
                table: "RedeemedTappables");

            migrationBuilder.DropForeignKey(
                name: "FK_Tokens_Accounts_AccountId",
                table: "Tokens");

            migrationBuilder.DropTable(
                name: "AccountVersions");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "NonStackableItems");

            migrationBuilder.DropTable(
                name: "StackableItems");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Tokens",
                table: "Tokens");

            migrationBuilder.DropIndex(
                name: "IX_Tokens_Date",
                table: "Tokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RedeemedTappables",
                table: "RedeemedTappables");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ActivityLogs",
                table: "ActivityLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Buildplate",
                table: "Buildplate");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "ClaimedOn",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "Rewards",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "token_type",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "RedeemedTappables");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "RedeemedTappables");

            migrationBuilder.DropColumn(
                name: "EntryId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Rewards",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "entity_type",
                table: "ActivityLogs");

            migrationBuilder.RenameTable(
                name: "Buildplate",
                newName: "PlayerBuildplates");

            migrationBuilder.RenameColumn(
                name: "TokenId",
                table: "Tokens",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "BlocksPerMeter",
                table: "TemplateBuildplates",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "TappableId",
                table: "RedeemedTappables",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "ActivityLogs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "BlocksPerMeter",
                table: "PlayerBuildplates",
                newName: "Version");

            migrationBuilder.RenameIndex(
                name: "IX_Buildplate_AccountId",
                table: "PlayerBuildplates",
                newName: "IX_PlayerBuildplates_AccountId");

            migrationBuilder.AddColumn<string>(
                name: "Tokens",
                table: "Tokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Tokens",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ObjectStoreId",
                table: "Tiles",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<decimal>(
                name: "Id",
                table: "Tiles",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "ServerDataObjectId",
                table: "TemplateBuildplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "PreviewObjectId",
                table: "TemplateBuildplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Scale",
                table: "TemplateBuildplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Slots",
                table: "SmeltingSlots",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "SmeltingSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ServerDataObjectId",
                table: "SharedBuildplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<long>(
                name: "LastViewed",
                table: "SharedBuildplates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Hotbar",
                table: "SharedBuildplates",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "Created",
                table: "SharedBuildplates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<long>(
                name: "BuildplateLastModifed",
                table: "SharedBuildplates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "Tappables",
                table: "RedeemedTappables",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "RedeemedTappables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Items",
                table: "Hotbars",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Hotbars",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ServerDataObjectId",
                table: "EncounterBuildplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Slots",
                table: "CraftingSlots",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "CraftingSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ActiveBoosts",
                table: "Boosts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Boosts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Entries",
                table: "ActivityLogs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "ActivityLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ServerDataObjectId",
                table: "PlayerBuildplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "PreviewObjectId",
                table: "PlayerBuildplates",
                type: "text",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<long>(
                name: "LastModified",
                table: "PlayerBuildplates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "Scale",
                table: "PlayerBuildplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tokens",
                table: "Tokens",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RedeemedTappables",
                table: "RedeemedTappables",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ActivityLogs",
                table: "ActivityLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerBuildplates",
                table: "PlayerBuildplates",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NonStackableItemsData = table.Column<string>(type: "text", nullable: false),
                    StackableItemsData = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventories_Accounts_Id",
                        column: x => x.Id,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Journals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Items = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journals_Accounts_Id",
                        column: x => x.Id,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Accounts_Id",
                table: "ActivityLogs",
                column: "Id",
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
                name: "FK_RedeemedTappables_Accounts_Id",
                table: "RedeemedTappables",
                column: "Id",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tokens_Accounts_Id",
                table: "Tokens",
                column: "Id",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
