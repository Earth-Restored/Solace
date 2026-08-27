using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CA1707 // Identifiers should not contain underscores
public partial class _20260827163210_OptimizeSchema : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Scale",
            table: "SharedBuildplates",
            newName: "BlocksPerMeter");

        migrationBuilder.RenameColumn(
            name: "Scale",
            table: "EncounterBuildplates",
            newName: "BlocksPerMeter");

        migrationBuilder.AlterColumn<string>(
            name: "Username",
            table: "Profiles",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "BlocksPerMeter",
            table: "SharedBuildplates",
            newName: "Scale");

        migrationBuilder.RenameColumn(
            name: "BlocksPerMeter",
            table: "EncounterBuildplates",
            newName: "Scale");

        migrationBuilder.AlterColumn<string>(
            name: "Username",
            table: "Profiles",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");
    }
}
