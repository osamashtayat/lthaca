using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LibraryAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_available",
                table: "films",
                type: "boolean",
                nullable: false,
                defaultValue: true); // existing films are available (edited from the generated false)

            migrationBuilder.CreateTable(
                name: "user_films",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    film_id = table.Column<Guid>(type: "uuid", nullable: false),
                    liked = table.Column<bool>(type: "boolean", nullable: false),
                    on_watchlist = table.Column<bool>(type: "boolean", nullable: false),
                    seen = table.Column<bool>(type: "boolean", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_films", x => new { x.user_id, x.film_id });
                    table.CheckConstraint("ck_user_films_rating", "rating IS NULL OR rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_user_films_films_film_id",
                        column: x => x.film_id,
                        principalTable: "films",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_films_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_films_film_id",
                table: "user_films",
                column: "film_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_films_user_id_updated_at",
                table: "user_films",
                columns: new[] { "user_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_films");

            migrationBuilder.DropColumn(
                name: "is_available",
                table: "films");
        }
    }
}
