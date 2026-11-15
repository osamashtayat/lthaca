using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomMoodRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #51: a room mood now points at its member (deleted with them) and keeps no text. Move the link, clear the
            // text, drop guest room moods nobody points at any more (solo moods always have a user), then the old column.
            migrationBuilder.AddColumn<Guid>(
                name: "room_member_id",
                table: "mood_queries",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE mood_queries q SET room_member_id = m.id, text = NULL
                FROM room_members m WHERE m.mood_query_id = q.id;
                DELETE FROM mood_queries WHERE room_member_id IS NULL AND user_id IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_room_members_mood_queries_mood_query_id",
                table: "room_members");

            migrationBuilder.DropIndex(
                name: "ix_room_members_mood_query_id",
                table: "room_members");

            migrationBuilder.DropColumn(
                name: "mood_query_id",
                table: "room_members");

            migrationBuilder.CreateIndex(
                name: "ux_mood_queries_room_member_id",
                table: "mood_queries",
                column: "room_member_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_mood_queries_room_mood_no_text",
                table: "mood_queries",
                sql: "room_member_id IS NULL OR text IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_mood_queries_room_members_room_member_id",
                table: "mood_queries",
                column: "room_member_id",
                principalTable: "room_members",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The link moves back; cleared texts stay cleared.
            migrationBuilder.AddColumn<Guid>(
                name: "mood_query_id",
                table: "room_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE room_members m SET mood_query_id = q.id
                FROM mood_queries q WHERE q.room_member_id = m.id;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_mood_queries_room_members_room_member_id",
                table: "mood_queries");

            migrationBuilder.DropIndex(
                name: "ux_mood_queries_room_member_id",
                table: "mood_queries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mood_queries_room_mood_no_text",
                table: "mood_queries");

            migrationBuilder.DropColumn(
                name: "room_member_id",
                table: "mood_queries");

            migrationBuilder.CreateIndex(
                name: "ix_room_members_mood_query_id",
                table: "room_members",
                column: "mood_query_id");

            migrationBuilder.AddForeignKey(
                name: "fk_room_members_mood_queries_mood_query_id",
                table: "room_members",
                column: "mood_query_id",
                principalTable: "mood_queries",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
