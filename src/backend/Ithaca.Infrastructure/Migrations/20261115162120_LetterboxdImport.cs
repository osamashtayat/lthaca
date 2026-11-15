using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LetterboxdImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    film_count = table.Column<int>(type: "integer", nullable: false),
                    processed_count = table.Column<int>(type: "integer", nullable: false),
                    matched_count = table.Column<int>(type: "integer", nullable: false),
                    added_to_catalog_count = table.Column<int>(type: "integer", nullable: false),
                    not_found_count = table.Column<int>(type: "integer", nullable: false),
                    not_found_list_kept = table.Column<bool>(type: "boolean", nullable: false),
                    new_films = table.Column<int>(type: "integer", nullable: false),
                    seen_set = table.Column<int>(type: "integer", nullable: false),
                    rated_set = table.Column<int>(type: "integer", nullable: false),
                    liked_set = table.Column<int>(type: "integer", nullable: false),
                    watchlist_set = table.Column<int>(type: "integer", nullable: false),
                    ratings_kept = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_jobs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "letterboxd_film_links",
                columns: table => new
                {
                    letterboxd_uri = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tmdb_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_letterboxd_film_links", x => x.letterboxd_uri);
                });

            migrationBuilder.CreateTable(
                name: "import_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: true),
                    letterboxd_uri = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    watched = table.Column<bool>(type: "boolean", nullable: false),
                    liked = table.Column<bool>(type: "boolean", nullable: false),
                    on_watchlist = table.Column<bool>(type: "boolean", nullable: false),
                    rating_half_stars = table.Column<short>(type: "smallint", nullable: true),
                    not_found_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_items", x => x.id);
                    table.CheckConstraint("ck_import_items_rating_half_stars", "rating_half_stars IS NULL OR rating_half_stars BETWEEN 1 AND 10");
                    table.ForeignKey(
                        name: "fk_import_items_import_jobs_import_job_id",
                        column: x => x.import_job_id,
                        principalTable: "import_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_import_items_job_position",
                table: "import_items",
                columns: new[] { "import_job_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_status_created_at",
                table: "import_jobs",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_import_jobs_user_id_created_at",
                table: "import_jobs",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_import_jobs_user_active",
                table: "import_jobs",
                column: "user_id",
                unique: true,
                filter: "status IN ('Queued', 'Running')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_items");

            migrationBuilder.DropTable(
                name: "letterboxd_film_links");

            migrationBuilder.DropTable(
                name: "import_jobs");
        }
    }
}
