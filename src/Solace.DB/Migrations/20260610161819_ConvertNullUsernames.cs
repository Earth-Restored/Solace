using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Migrations
{
    /// <inheritdoc />
    public partial class ConvertNullUsernames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Accounts",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.Sql("UPDATE \"Accounts\" SET \"Username\" = NULL WHERE \"Username\" = '[null]';");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Username",
                table: "Accounts",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_Username",
                table: "Accounts");

            migrationBuilder.Sql("UPDATE \"Accounts\" SET \"Username\" = '[null]' WHERE \"Username\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Accounts",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

        }
    }
}
