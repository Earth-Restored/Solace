using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.AdminPanel.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class AddLinkedInGameAccounts : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(
            name: "LinkedInGameAccounts",
            table: "AspNetUsers",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(
            name: "LinkedInGameAccounts",
            table: "AspNetUsers");
}
