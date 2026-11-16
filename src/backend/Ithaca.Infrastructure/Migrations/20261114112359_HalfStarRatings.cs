using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HalfStarRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-0024: whole stars (1–5) become a count of half stars (1–10). Add the new column, copy (× 2), then drop the old one.
            migrationBuilder.AddColumn<short>(
                name: "rating_half_stars",
                table: "user_films",
                type: "smallint",
                nullable: true);

            migrationBuilder.Sql("UPDATE user_films SET rating_half_stars = rating * 2 WHERE rating IS NOT NULL;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_films_rating",
                table: "user_films");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "user_films");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_films_rating_half_stars",
                table: "user_films",
                sql: "rating_half_stars IS NULL OR rating_half_stars BETWEEN 1 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy: a half star rounds up to the next whole star (3.5 → 4, 0.5 → 1).
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_films_rating_half_stars",
                table: "user_films");

            migrationBuilder.AddColumn<int>(
                name: "rating",
                table: "user_films",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE user_films SET rating = (rating_half_stars + 1) / 2 WHERE rating_half_stars IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "rating_half_stars",
                table: "user_films");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_films_rating",
                table: "user_films",
                sql: "rating IS NULL OR rating BETWEEN 1 AND 5");
        }
    }
}
