using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.WebPortal.Data.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class _20260723150554_AddBuildplatePreview : Migration
#pragma warning restore MA0048 // File name must match type name
{
#pragma warning disable IDE0022 // Use expression body for method
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BuildplatePreviews",
            columns: table => new
            {
                BuildplateId = table.Column<Guid>(type: "uuid", nullable: false),
                PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviewData = table.Column<byte[]>(type: "bytea", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BuildplatePreviews", x => new { x.BuildplateId, x.PlayerId });
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BuildplatePreviews");
    }
#pragma warning restore IDE0022 // Use expression body for method
}
