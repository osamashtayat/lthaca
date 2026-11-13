using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoodQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "mood_query_id",
                table: "decks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "mood_queries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    moods = table.Column<string[]>(type: "text[]", nullable: false),
                    max_runtime = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    text = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    include_genres = table.Column<string[]>(type: "text[]", nullable: false),
                    exclude_genres = table.Column<string[]>(type: "text[]", nullable: false),
                    max_runtime_minutes = table.Column<int>(type: "integer", nullable: true),
                    min_release_year = table.Column<int>(type: "integer", nullable: true),
                    max_release_year = table.Column<int>(type: "integer", nullable: true),
                    keywords = table.Column<string[]>(type: "text[]", nullable: false),
                    text_ignored = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mood_queries", x => x.id);
                    table.ForeignKey(
                        name: "fk_mood_queries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_decks_mood_query_id",
                table: "decks",
                column: "mood_query_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_decks_mood_query",
                table: "decks",
                sql: "(source = 'Mood') = (mood_query_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_mood_queries_user_id",
                table: "mood_queries",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_decks_mood_queries_mood_query_id",
                table: "decks",
                column: "mood_query_id",
                principalTable: "mood_queries",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_decks_mood_queries_mood_query_id",
                table: "decks");

            migrationBuilder.DropTable(
                name: "mood_queries");

            migrationBuilder.DropIndex(
                name: "ix_decks_mood_query_id",
                table: "decks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_decks_mood_query",
                table: "decks");

            migrationBuilder.DropColumn(
                name: "mood_query_id",
                table: "decks");
        }
    }
}
