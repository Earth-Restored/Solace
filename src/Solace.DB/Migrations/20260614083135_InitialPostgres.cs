using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Postgres.Migrations;

/// <inheritdoc />
public partial class InitialPostgres : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Accounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                CreatedDate = table.Column<long>(type: "bigint", nullable: false),
                Username = table.Column<string>(type: "text", nullable: true),
                ProfilePictureUrl = table.Column<string>(type: "text", nullable: true),
                FirstName = table.Column<string>(type: "text", nullable: true),
                LastName = table.Column<string>(type: "text", nullable: true),
                PasswordSalt = table.Column<byte[]>(type: "bytea", maxLength: 16, nullable: false),
                PasswordHash = table.Column<byte[]>(type: "bytea", maxLength: 64, nullable: false)
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
                ServerDataObjectId = table.Column<string>(type: "text", nullable: false)
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
                Version = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                Offset = table.Column<int>(type: "integer", nullable: false),
                Scale = table.Column<int>(type: "integer", nullable: false),
                Night = table.Column<bool>(type: "boolean", nullable: false),
                ServerDataObjectId = table.Column<string>(type: "text", nullable: false),
                PreviewObjectId = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TemplateBuildplates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Tiles",
            columns: table => new
            {
                Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                ObjectStoreId = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tiles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ActivityLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                Entries = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                table.ForeignKey(
                    name: "FK_ActivityLogs_Accounts_Id",
                    column: x => x.Id,
                    principalTable: "Accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Boosts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                ActiveBoosts = table.Column<string>(type: "text", nullable: false)
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
                Version = table.Column<int>(type: "integer", nullable: false),
                Slots = table.Column<string>(type: "text", nullable: false)
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
                Version = table.Column<int>(type: "integer", nullable: false),
                Items = table.Column<string>(type: "text", nullable: false)
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
            name: "Inventories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                StackableItemsData = table.Column<string>(type: "text", nullable: false),
                NonStackableItemsData = table.Column<string>(type: "text", nullable: false)
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
                Version = table.Column<int>(type: "integer", nullable: false),
                Items = table.Column<string>(type: "text", nullable: false)
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

        migrationBuilder.CreateTable(
            name: "PlayerBuildplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "text", nullable: false),
                Size = table.Column<int>(type: "integer", nullable: false),
                Offset = table.Column<int>(type: "integer", nullable: false),
                Scale = table.Column<int>(type: "integer", nullable: false),
                Night = table.Column<bool>(type: "boolean", nullable: false),
                LastModified = table.Column<long>(type: "bigint", nullable: false),
                ServerDataObjectId = table.Column<string>(type: "text", nullable: false),
                PreviewObjectId = table.Column<string>(type: "text", nullable: false)
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
                Version = table.Column<int>(type: "integer", nullable: false),
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
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                Tappables = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RedeemedTappables", x => x.Id);
                table.ForeignKey(
                    name: "FK_RedeemedTappables_Accounts_Id",
                    column: x => x.Id,
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
                Created = table.Column<long>(type: "bigint", nullable: false),
                BuildplateLastModifed = table.Column<long>(type: "bigint", nullable: false),
                LastViewed = table.Column<long>(type: "bigint", nullable: false),
                NumberOfTimesViewed = table.Column<int>(type: "integer", nullable: false),
                Hotbar = table.Column<string>(type: "text", nullable: false),
                ServerDataObjectId = table.Column<string>(type: "text", nullable: false)
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
                Version = table.Column<int>(type: "integer", nullable: false),
                Slots = table.Column<string>(type: "text", nullable: false)
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
            name: "Tokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                Tokens = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_Tokens_Accounts_Id",
                    column: x => x.Id,
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
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
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
            name: "Inventories");

        migrationBuilder.DropTable(
            name: "Journals");

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
            name: "TemplateBuildplates");

        migrationBuilder.DropTable(
            name: "Tiles");

        migrationBuilder.DropTable(
            name: "Tokens");

        migrationBuilder.DropTable(
            name: "Accounts");
    }
}
