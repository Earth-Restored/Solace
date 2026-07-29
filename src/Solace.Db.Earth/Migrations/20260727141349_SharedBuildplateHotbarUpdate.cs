using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class SharedBuildplateHotbarUpdate : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AlterColumn<string>(
            name: "Hotbar",
            table: "SharedBuildplates",
            type: "text",
            nullable: true,
            defaultValue: null,
            oldClrType: typeof(string),
            oldType: "jsonb",
            oldNullable: true);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.AlterColumn<string>(
            name: "Hotbar",
            table: "SharedBuildplates",
            type: "jsonb",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");
}
