using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Solace.DB.Migrations;

/// <inheritdoc />
public partial class InitialMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedDate = table.Column<long>(type: "bigint", nullable: false),
                Username = table.Column<string>(type: "text", nullable: true),
                ProfilePictureUrl = table.Column<string>(type: "text", nullable: true),
                FirstName = table.Column<string>(type: "text", nullable: true),
                LastName = table.Column<string>(type: "text", nullable: true),
                PasswordSalt = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: false),
                PasswordHash = table.Column<byte[]>(type: "bytea", maxLength: 64, nullable: false),
                SkinImageData = table.Column<byte[]>(type: "bytea", maxLength: 16384, nullable: true),
                IsSkinSlim = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Accounts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EncounterBuildplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                Offset = table.Column<int>(type: "integer", nullable: false),
                Scale = table.Column<int>(type: "integer", nullable: false),
                ServerDataObjectId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EncounterBuildplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Secrets",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Secrets", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TemplateBuildplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                Offset = table.Column<int>(type: "integer", nullable: false),
                BlocksPerMeter = table.Column<int>(type: "integer", nullable: false),
                Night = table.Column<bool>(type: "boolean", nullable: false),
                ServerDataObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviewObjectId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateBuildplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Tiles",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ObjectStoreId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tiles", x => x.Id);
            });

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
            name: "ActivityLogs",
            columns: table => new
            {
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                EntryId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                entity_type = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                Level = table.Column<int>(type: "integer", nullable: true),
                Rewards = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityLogs", x => new { x.AccountId, x.EntryId });
                table.ForeignKey(
                    name: "FK_ActivityLogs_Accounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Boosts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActiveBoosts = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Boosts", x => x.Id);
                table.ForeignKey(
                    name: "FK_Boosts_Accounts_Id",
                    column: x => x.Id,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CraftingSlots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slots = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CraftingSlots", x => x.Id);
                table.ForeignKey(
                    name: "FK_CraftingSlots_Accounts_Id",
                    column: x => x.Id,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Hotbars",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Items = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Hotbars", x => x.Id);
                table.ForeignKey(
                    name: "FK_Hotbars_Accounts_Id",
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
            name: "PlayerBuildplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "text", nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                Offset = table.Column<int>(type: "integer", nullable: false),
                BlocksPerMeter = table.Column<int>(type: "integer", nullable: false),
                Night = table.Column<bool>(type: "boolean", nullable: false),
                LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ServerDataObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviewObjectId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerBuildplates", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlayerBuildplates_Accounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Profiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Health = table.Column<int>(type: "integer", nullable: false),
                Experience = table.Column<int>(type: "integer", nullable: false),
                Level = table.Column<int>(type: "integer", nullable: false),
                Rubies = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Profiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_Profiles_Accounts_Id",
                    column: x => x.Id,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RedeemedTappables",
            columns: table => new
            {
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                TappableId = table.Column<Guid>(type: "uuid", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RedeemedTappables", x => new { x.AccountId, x.TappableId });
                table.ForeignKey(
                    name: "FK_RedeemedTappables_Accounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SharedBuildplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                Offset = table.Column<int>(type: "integer", nullable: false),
                Scale = table.Column<int>(type: "integer", nullable: false),
                Night = table.Column<bool>(type: "boolean", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BuildplateLastModifed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastViewed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NumberOfTimesViewed = table.Column<int>(type: "integer", nullable: false),
                ServerDataObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Hotbar = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SharedBuildplates", x => x.Id);
                table.ForeignKey(
                    name: "FK_SharedBuildplates_Accounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SmeltingSlots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slots = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SmeltingSlots", x => x.Id);
                table.ForeignKey(
                    name: "FK_SmeltingSlots_Accounts_Id",
                    column: x => x.Id,
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

        migrationBuilder.CreateTable(
            name: "Tokens",
            columns: table => new
            {
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                token_type = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                Date = table.Column<DateOnly>(type: "date", nullable: true),
                ClaimedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Level = table.Column<int>(type: "integer", nullable: true),
                Rewards = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tokens", x => new { x.AccountId, x.TokenId });
                table.ForeignKey(
                    name: "FK_Tokens_Accounts_AccountId",
                    column: x => x.AccountId,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Accounts_Username",
            table: "Accounts",
            column: "Username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlayerBuildplates_AccountId",
            table: "PlayerBuildplates",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_SharedBuildplates_AccountId",
            table: "SharedBuildplates",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_Tokens_Date",
            table: "Tokens",
            column: "Date");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AccountVersions");

        migrationBuilder.DropTable(
            name: "ActivityLogs");

        migrationBuilder.DropTable(
            name: "Boosts");

        migrationBuilder.DropTable(
            name: "CraftingSlots");

        migrationBuilder.DropTable(
            name: "EncounterBuildplates");

        migrationBuilder.DropTable(
            name: "Hotbars");

        migrationBuilder.DropTable(
            name: "JournalEntries");

        migrationBuilder.DropTable(
            name: "NonStackableItems");

        migrationBuilder.DropTable(
            name: "PlayerBuildplates");

        migrationBuilder.DropTable(
            name: "Profiles");

        migrationBuilder.DropTable(
            name: "RedeemedTappables");

        migrationBuilder.DropTable(
            name: "Secrets");

        migrationBuilder.DropTable(
            name: "SharedBuildplates");

        migrationBuilder.DropTable(
            name: "SmeltingSlots");

        migrationBuilder.DropTable(
            name: "StackableItems");

        migrationBuilder.DropTable(
            name: "TemplateBuildplates");

        migrationBuilder.DropTable(
            name: "Tiles");

        migrationBuilder.DropTable(
            name: "Tokens");

        migrationBuilder.DropTable(
            name: "Accounts");
    }
}
