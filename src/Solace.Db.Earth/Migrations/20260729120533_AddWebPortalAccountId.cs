using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class AddWebPortalAccountId : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "WebPortalAccountId",
            table: "Accounts",
            type: "bigint",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Accounts_WebPortalAccountId",
            table: "Accounts",
            column: "WebPortalAccountId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Accounts_WebPortalAccountId",
            table: "Accounts");

        migrationBuilder.DropColumn(
            name: "WebPortalAccountId",
            table: "Accounts");
    }
}
