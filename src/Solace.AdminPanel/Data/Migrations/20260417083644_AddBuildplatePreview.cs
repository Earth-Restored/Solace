using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.AdminPanel.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class AddBuildplatePreview : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BuildplatePreviews",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                PlayerId = table.Column<string>(type: "TEXT", nullable: true),
                BuildplateId = table.Column<string>(type: "TEXT", nullable: false),
                PreviewData = table.Column<byte[]>(type: "BLOB", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BuildplatePreviews", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Player_Buildplate",
            table: "BuildplatePreviews",
            columns: ["PlayerId", "BuildplateId"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(
            name: "BuildplatePreviews");
}
