using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CA1707 // Identifiers should not contain underscores
public partial class _20260823133143_BuildplateTemplateRelation : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddForeignKey(
            name: "FK_PlayerBuildplates_TemplateBuildplates_TemplateId",
            table: "PlayerBuildplates",
            column: "TemplateId",
            principalTable: "TemplateBuildplates",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropForeignKey(
            name: "FK_PlayerBuildplates_TemplateBuildplates_TemplateId",
            table: "PlayerBuildplates");
}
