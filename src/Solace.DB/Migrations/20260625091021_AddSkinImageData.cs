using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddSkinImageData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "SkinImageData",
                table: "Accounts",
                type: "BLOB",
                maxLength: 16384,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkinImageData",
                table: "Accounts");
        }
    }
}
