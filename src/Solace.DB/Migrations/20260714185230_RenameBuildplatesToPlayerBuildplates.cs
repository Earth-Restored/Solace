using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RenameBuildplatesToPlayerBuildplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildplate_Accounts_AccountId",
                table: "Buildplate");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Buildplate",
                table: "Buildplate");

            migrationBuilder.RenameTable(
                name: "Buildplate",
                newName: "PlayerBuildplates");

            migrationBuilder.RenameIndex(
                name: "IX_Buildplate_AccountId",
                table: "PlayerBuildplates",
                newName: "IX_PlayerBuildplates_AccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlayerBuildplates",
                table: "PlayerBuildplates",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerBuildplates_Accounts_AccountId",
                table: "PlayerBuildplates",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerBuildplates_Accounts_AccountId",
                table: "PlayerBuildplates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlayerBuildplates",
                table: "PlayerBuildplates");

            migrationBuilder.RenameTable(
                name: "PlayerBuildplates",
                newName: "Buildplate");

            migrationBuilder.RenameIndex(
                name: "IX_PlayerBuildplates_AccountId",
                table: "Buildplate",
                newName: "IX_Buildplate_AccountId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Buildplate",
                table: "Buildplate",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Buildplate_Accounts_AccountId",
                table: "Buildplate",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
