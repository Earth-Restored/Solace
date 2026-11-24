using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CA1707 // Identifiers should not contain underscores
public partial class _20261124094413_SeasonRemoveKeyMakeDatesNullable : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Seasons_Key",
            table: "Seasons");

        migrationBuilder.DropColumn(
            name: "Key",
            table: "Seasons");

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "StartDate",
            table: "Seasons",
            type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone");

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "EndDate",
            table: "Seasons",
            type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone");

        migrationBuilder.CreateIndex(
            name: "IX_Seasons_SeasonNumber",
            table: "Seasons",
            column: "SeasonNumber",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Seasons_SeasonNumber",
            table: "Seasons");

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "StartDate",
            table: "Seasons",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone",
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "EndDate",
            table: "Seasons",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Key",
            table: "Seasons",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_Seasons_Key",
            table: "Seasons",
            column: "Key",
            unique: true);
    }
}
