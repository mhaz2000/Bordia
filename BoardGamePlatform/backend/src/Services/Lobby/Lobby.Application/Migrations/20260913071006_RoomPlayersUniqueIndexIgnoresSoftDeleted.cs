using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lobby.Application.Migrations
{
    /// <inheritdoc />
    public partial class RoomPlayersUniqueIndexIgnoresSoftDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_room_players_room_id_user_id",
                table: "room_players");

            migrationBuilder.CreateIndex(
                name: "ix_room_players_room_id_user_id",
                table: "room_players",
                columns: new[] { "room_id", "user_id" },
                unique: true,
                filter: "\"is_deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_room_players_room_id_user_id",
                table: "room_players");

            migrationBuilder.CreateIndex(
                name: "ix_room_players_room_id_user_id",
                table: "room_players",
                columns: new[] { "room_id", "user_id" },
                unique: true);
        }
    }
}
