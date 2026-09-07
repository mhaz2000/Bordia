using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lobby.Application.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "room_code",
                table: "rooms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE rooms SET room_code = upper(substr(md5(id::text), 1, 6)) WHERE room_code = '';");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_room_code",
                table: "rooms",
                column: "room_code",
                unique: true,
                filter: "\"is_deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_rooms_room_code",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "room_code",
                table: "rooms");
        }
    }
}
