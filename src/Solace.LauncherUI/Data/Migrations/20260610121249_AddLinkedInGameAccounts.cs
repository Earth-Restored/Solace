using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.LauncherUI.Migrations;

/// <inheritdoc />
public partial class AddLinkedInGameAccounts : Migration
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
