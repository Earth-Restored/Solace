using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CA1707 // Identifiers should not contain underscores
public partial class _20260823074043_AddIndexToPlayerBuildplateTemplateId : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.CreateIndex(
            name: "IX_PlayerBuildplates_TemplateId",
            table: "PlayerBuildplates",
            column: "TemplateId");

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropIndex(
            name: "IX_PlayerBuildplates_TemplateId",
            table: "PlayerBuildplates");
}
