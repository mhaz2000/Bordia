using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDeadlinesAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "game_ends_at_utc",
                table: "game_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_action_deadline_utc",
                table: "game_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "game_sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_game_ends_at_utc",
                table: "game_sessions",
                column: "game_ends_at_utc",
                filter: "\"status\" = 0 AND \"is_deleted\" = false AND \"game_ends_at_utc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_next_action_deadline_utc",
                table: "game_sessions",
                column: "next_action_deadline_utc",
                filter: "\"status\" = 0 AND \"is_deleted\" = false AND \"next_action_deadline_utc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_game_players_connection_id",
                table: "game_players",
                column: "connection_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_game_sessions_game_ends_at_utc",
                table: "game_sessions");

            migrationBuilder.DropIndex(
                name: "ix_game_sessions_next_action_deadline_utc",
                table: "game_sessions");

            migrationBuilder.DropIndex(
                name: "ix_game_players_connection_id",
                table: "game_players");

            migrationBuilder.DropColumn(
                name: "game_ends_at_utc",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "next_action_deadline_utc",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "version",
                table: "game_sessions");
        }
    }
}
