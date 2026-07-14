using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.DB.Postgres.Migrations;

/// <inheritdoc />
public partial class RewardsMigration : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "rewards_dailylogin",
            table: "Tokens");

        migrationBuilder.RenameColumn(
            name: "rewards_levelup",
            table: "Tokens",
            newName: "Rewards");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Rewards",
            table: "Tokens",
            newName: "rewards_levelup");

        migrationBuilder.AddColumn<string>(
            name: "rewards_dailylogin",
            table: "Tokens",
            type: "jsonb",
            nullable: true);
    }
}
