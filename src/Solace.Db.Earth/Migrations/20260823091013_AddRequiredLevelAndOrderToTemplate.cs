using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CA1707 // Identifiers should not contain underscores
public partial class _20260823091013_AddRequiredLevelAndOrderToTemplate : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Order",
            table: "TemplateBuildplates",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RequiredLevel",
            table: "TemplateBuildplates",
            type: "integer",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Order",
            table: "TemplateBuildplates");

        migrationBuilder.DropColumn(
            name: "RequiredLevel",
            table: "TemplateBuildplates");
    }
}
