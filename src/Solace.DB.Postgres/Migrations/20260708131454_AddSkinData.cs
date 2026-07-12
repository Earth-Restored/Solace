using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Postgres.Migrations;

/// <inheritdoc />
public partial class AddSkinData : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsSkinSlim",
            table: "Accounts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<byte[]>(
            name: "SkinImageData",
            table: "Accounts",
            type: "bytea",
            maxLength: 16384,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsSkinSlim",
            table: "Accounts");

        migrationBuilder.DropColumn(
            name: "SkinImageData",
            table: "Accounts");
    }
}
