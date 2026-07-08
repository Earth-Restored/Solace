using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSkinSlim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSkinSlim",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSkinSlim",
                table: "Accounts");
        }
    }
}
