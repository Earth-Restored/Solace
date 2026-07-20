using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.AdminPanel.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class AddIsBuiltInToApplicationRole : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<bool>(
            name: "IsBuiltIn",
            table: "AspNetRoles",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(
            name: "IsBuiltIn",
            table: "AspNetRoles");
}
