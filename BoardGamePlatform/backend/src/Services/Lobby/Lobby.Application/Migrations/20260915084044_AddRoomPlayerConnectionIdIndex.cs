using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lobby.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomPlayerConnectionIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_room_players_connection_id",
                table: "room_players",
                column: "connection_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_room_players_connection_id",
                table: "room_players");
        }
    }
}
