using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.WebPortal.Data.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
public partial class _20260728143633_BuildplatePreviewAddBounds : Migration
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<float>(
            name: "BoundsMaxX",
            table: "BuildplatePreviews",
            type: "real",
            nullable: false,
            defaultValue: 0f);

        migrationBuilder.AddColumn<float>(
            name: "BoundsMaxY",
            table: "BuildplatePreviews",
            type: "real",
            nullable: false,
            defaultValue: 0f);

        migrationBuilder.AddColumn<float>(
            name: "BoundsMaxZ",
            table: "BuildplatePreviews",
            type: "real",
            nullable: false,
            defaultValue: 0f);

        migrationBuilder.AddColumn<float>(
            name: "BoundsMinX",
            table: "BuildplatePreviews",
            type: "real",
            nullable: false,
            defaultValue: 0f);

        migrationBuilder.AddColumn<float>(
            name: "BoundsMinY",
            table: "BuildplatePreviews",
            type: "real",
            nullable: false,
            defaultValue: 0f);

        migrationBuilder.AddColumn<float>(
            name: "BoundsMinZ",
            table: "BuildplatePreviews",
            type: "real",
            nullable: false,
            defaultValue: 0f);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BoundsMaxX",
            table: "BuildplatePreviews");

        migrationBuilder.DropColumn(
            name: "BoundsMaxY",
            table: "BuildplatePreviews");

        migrationBuilder.DropColumn(
            name: "BoundsMaxZ",
            table: "BuildplatePreviews");

        migrationBuilder.DropColumn(
            name: "BoundsMinX",
            table: "BuildplatePreviews");

        migrationBuilder.DropColumn(
            name: "BoundsMinY",
            table: "BuildplatePreviews");

        migrationBuilder.DropColumn(
            name: "BoundsMinZ",
            table: "BuildplatePreviews");
    }
}
