using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SoloDecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "decks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decks", x => x.id);
                    table.ForeignKey(
                        name: "fk_decks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deck_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deck_id = table.Column<Guid>(type: "uuid", nullable: false),
                    film_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deck_items", x => x.id);
                    table.CheckConstraint("ck_deck_items_position", "position BETWEEN 1 AND 300");
                    table.ForeignKey(
                        name: "fk_deck_items_decks_deck_id",
                        column: x => x.deck_id,
                        principalTable: "decks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_deck_items_films_film_id",
                        column: x => x.film_id,
                        principalTable: "films",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "swipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    deck_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    seen_verdict = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_swipes", x => x.id);
                    table.CheckConstraint("ck_swipes_seen_verdict", "seen_verdict IS NULL OR direction = 'Up'");
                    table.ForeignKey(
                        name: "fk_swipes_deck_items_deck_item_id",
                        column: x => x.deck_item_id,
                        principalTable: "deck_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deck_items_film_id",
                table: "deck_items",
                column: "film_id");

            migrationBuilder.CreateIndex(
                name: "ux_deck_items_deck_id_film_id",
                table: "deck_items",
                columns: new[] { "deck_id", "film_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_deck_items_deck_id_position",
                table: "deck_items",
                columns: new[] { "deck_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decks_user_id",
                table: "decks",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_swipes_deck_item_id",
                table: "swipes",
                column: "deck_item_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "swipes");

            migrationBuilder.DropTable(
                name: "deck_items");

            migrationBuilder.DropTable(
                name: "decks");
        }
    }
}
