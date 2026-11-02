using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Solace.Db.Earth.Migrations;

/// <inheritdoc />
#pragma warning disable MA0048 // File name must match type name
#pragma warning disable CA1707 // Identifiers should not contain underscores
public partial class _20261111122311_AddSeasons : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore MA0048 // File name must match type name
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Tokens_Date",
            table: "Tokens");

        migrationBuilder.DropColumn(
            name: "Date",
            table: "Tokens");

        migrationBuilder.RenameColumn(
            name: "ClaimedOn",
            table: "Tokens",
            newName: "ExpirationTime");

        migrationBuilder.AlterColumn<string>(
            name: "token_type",
            table: "Tokens",
            type: "character varying(34)",
            maxLength: 34,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(21)",
            oldMaxLength: 21);

        migrationBuilder.AddColumn<int>(
            name: "Category",
            table: "Tokens",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ChallengeId",
            table: "Tokens",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CurrentCount",
            table: "Tokens",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PreviousCount",
            table: "Tokens",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SeasonId",
            table: "Tokens",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "TotalThreshold",
            table: "Tokens",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ChallengeId",
            table: "ActivityLogs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Duration",
            table: "ActivityLogs",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsFinalSeasonChallenge",
            table: "ActivityLogs",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Order",
            table: "ActivityLogs",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Seasons",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                Key = table.Column<string>(type: "text", nullable: false),
                StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                ExclusiveRewards = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Seasons", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SeedingHistory",
            columns: table => new
            {
                Key = table.Column<string>(type: "text", nullable: false),
                SeededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Version = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SeedingHistory", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "Challenges",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                PrerequisiteIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                PrerequisiteLogicalCondition = table.Column<int>(type: "integer", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                TotalThreshold = table.Column<int>(type: "integer", nullable: false),
                ObjectiveType = table.Column<int>(type: "integer", nullable: false),
                ObjectiveTarget = table.Column<Guid>(type: "uuid", nullable: true),
                IsFinalSeasonChallenge = table.Column<bool>(type: "boolean", nullable: false),
                Rewards = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Challenges", x => x.Id);
                table.ForeignKey(
                    name: "FK_Challenges_Seasons_SeasonId",
                    column: x => x.SeasonId,
                    principalTable: "Seasons",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PlayerSeasonProgresses",
            columns: table => new
            {
                ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                ActiveSeasonChallengeId = table.Column<Guid>(type: "uuid", nullable: true),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsCompleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerSeasonProgresses", x => new { x.ProfileId, x.SeasonId });
                table.ForeignKey(
                    name: "FK_PlayerSeasonProgresses_Profiles_ProfileId",
                    column: x => x.ProfileId,
                    principalTable: "Profiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlayerSeasonProgresses_Seasons_SeasonId",
                    column: x => x.SeasonId,
                    principalTable: "Seasons",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PlayerChallengeProgresses",
            columns: table => new
            {
                ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentCount = table.Column<int>(type: "integer", nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                IsComplete = table.Column<bool>(type: "boolean", nullable: false),
                EndTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlayerChallengeProgresses", x => new { x.ProfileId, x.ChallengeId });
                table.ForeignKey(
                    name: "FK_PlayerChallengeProgresses_Challenges_ChallengeId",
                    column: x => x.ChallengeId,
                    principalTable: "Challenges",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PlayerChallengeProgresses_Profiles_ProfileId",
                    column: x => x.ProfileId,
                    principalTable: "Profiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Challenges_SeasonId",
            table: "Challenges",
            column: "SeasonId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayerChallengeProgresses_ChallengeId",
            table: "PlayerChallengeProgresses",
            column: "ChallengeId");

        migrationBuilder.CreateIndex(
            name: "IX_PlayerSeasonProgresses_SeasonId",
            table: "PlayerSeasonProgresses",
            column: "SeasonId");

        migrationBuilder.CreateIndex(
            name: "IX_Seasons_IsActive",
            table: "Seasons",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_Seasons_Key",
            table: "Seasons",
            column: "Key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PlayerChallengeProgresses");

        migrationBuilder.DropTable(
            name: "PlayerSeasonProgresses");

        migrationBuilder.DropTable(
            name: "SeedingHistory");

        migrationBuilder.DropTable(
            name: "Challenges");

        migrationBuilder.DropTable(
            name: "Seasons");

        migrationBuilder.DropColumn(
            name: "Category",
            table: "Tokens");

        migrationBuilder.DropColumn(
            name: "ChallengeId",
            table: "Tokens");

        migrationBuilder.DropColumn(
            name: "CurrentCount",
            table: "Tokens");

        migrationBuilder.DropColumn(
            name: "PreviousCount",
            table: "Tokens");

        migrationBuilder.DropColumn(
            name: "SeasonId",
            table: "Tokens");

        migrationBuilder.DropColumn(
            name: "TotalThreshold",
            table: "Tokens");

        migrationBuilder.DropColumn(
            name: "ChallengeId",
            table: "ActivityLogs");

        migrationBuilder.DropColumn(
            name: "Duration",
            table: "ActivityLogs");

        migrationBuilder.DropColumn(
            name: "IsFinalSeasonChallenge",
            table: "ActivityLogs");

        migrationBuilder.DropColumn(
            name: "Order",
            table: "ActivityLogs");

        migrationBuilder.RenameColumn(
            name: "ExpirationTime",
            table: "Tokens",
            newName: "ClaimedOn");

        migrationBuilder.AlterColumn<string>(
            name: "token_type",
            table: "Tokens",
            type: "character varying(21)",
            maxLength: 21,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(34)",
            oldMaxLength: 34);

        migrationBuilder.AddColumn<DateOnly>(
            name: "Date",
            table: "Tokens",
            type: "date",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Tokens_Date",
            table: "Tokens",
            column: "Date");
    }
}
