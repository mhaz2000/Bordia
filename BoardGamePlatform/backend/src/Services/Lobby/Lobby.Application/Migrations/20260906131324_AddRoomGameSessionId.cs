using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lobby.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomGameSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "game_session_id",
                table: "rooms",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "game_session_id",
                table: "rooms");
        }
    }
}
