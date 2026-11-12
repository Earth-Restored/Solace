using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CA1707 // Identifiers should not contain underscores
public partial class _20261112135001_AddResourcePackManagement : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ResourcePackAssets",
            columns: table => new
            {
                Path = table.Column<string>(type: "text", nullable: false),
                Data = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ResourcePackAssets", x => x.Path);
            });

        migrationBuilder.CreateTable(
            name: "ResourcePackTranslations",
            columns: table => new
            {
                Language = table.Column<string>(type: "text", nullable: false),
                Key = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ResourcePackTranslations", x => new { x.Language, x.Key });
            });

        migrationBuilder.CreateTable(
            name: "SynthesizedResourcePacks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BuildNumber = table.Column<int>(type: "integer", nullable: false),
                VersionString = table.Column<string>(type: "text", nullable: false),
                ContentHash = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SynthesizedResourcePacks", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SynthesizedResourcePacks_BuildNumber",
            table: "SynthesizedResourcePacks",
            column: "BuildNumber");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ResourcePackAssets");

        migrationBuilder.DropTable(
            name: "ResourcePackTranslations");

        migrationBuilder.DropTable(
            name: "SynthesizedResourcePacks");
    }
}
