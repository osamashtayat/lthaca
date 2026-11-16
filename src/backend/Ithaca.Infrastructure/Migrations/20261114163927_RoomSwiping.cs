using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomSwiping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_decks_mood_query",
                table: "decks");

            migrationBuilder.AddColumn<string[]>(
                name: "hard_nos",
                table: "room_members",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "mood_query_id",
                table: "room_members",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "mood_queries",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "decks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "room_id",
                table: "decks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "votes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deck_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    seen_verdict = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_votes", x => x.id);
                    table.CheckConstraint("ck_votes_seen_verdict", "seen_verdict IS NULL OR direction = 'Up'");
                    table.ForeignKey(
                        name: "fk_votes_deck_items_deck_item_id",
                        column: x => x.deck_item_id,
                        principalTable: "deck_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_votes_room_members_room_member_id",
                        column: x => x.room_member_id,
                        principalTable: "room_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_room_members_mood_query_id",
                table: "room_members",
                column: "mood_query_id");

            migrationBuilder.CreateIndex(
                name: "ux_decks_room_id",
                table: "decks",
                column: "room_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_decks_mood_query",
                table: "decks",
                sql: "CASE WHEN room_id IS NULL THEN (source = 'Mood') = (mood_query_id IS NOT NULL) ELSE mood_query_id IS NULL END");

            migrationBuilder.AddCheckConstraint(
                name: "ck_decks_user_or_room",
                table: "decks",
                sql: "(user_id IS NULL) <> (room_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_votes_room_member_id",
                table: "votes",
                column: "room_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_votes_deck_item_id_room_member_id",
                table: "votes",
                columns: new[] { "deck_item_id", "room_member_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_decks_rooms_room_id",
                table: "decks",
                column: "room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_room_members_mood_queries_mood_query_id",
                table: "room_members",
                column: "mood_query_id",
                principalTable: "mood_queries",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Room decks (with their items and votes) and guests' room moods can't exist before this migration.
            migrationBuilder.Sql("DELETE FROM decks WHERE room_id IS NOT NULL; DELETE FROM mood_queries WHERE user_id IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_decks_rooms_room_id",
                table: "decks");

            migrationBuilder.DropForeignKey(
                name: "fk_room_members_mood_queries_mood_query_id",
                table: "room_members");

            migrationBuilder.DropTable(
                name: "votes");

            migrationBuilder.DropIndex(
                name: "ix_room_members_mood_query_id",
                table: "room_members");

            migrationBuilder.DropIndex(
                name: "ux_decks_room_id",
                table: "decks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_decks_mood_query",
                table: "decks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_decks_user_or_room",
                table: "decks");

            migrationBuilder.DropColumn(
                name: "hard_nos",
                table: "room_members");

            migrationBuilder.DropColumn(
                name: "mood_query_id",
                table: "room_members");

            migrationBuilder.DropColumn(
                name: "room_id",
                table: "decks");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "mood_queries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "decks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_decks_mood_query",
                table: "decks",
                sql: "(source = 'Mood') = (mood_query_id IS NOT NULL)");
        }
    }
}
