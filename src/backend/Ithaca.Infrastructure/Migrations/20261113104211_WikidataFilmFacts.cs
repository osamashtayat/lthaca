using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WikidataFilmFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "film_facts",
                columns: table => new
                {
                    film_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wikidata_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    genres = table.Column<string[]>(type: "text[]", nullable: false),
                    main_subjects = table.Column<string[]>(type: "text[]", nullable: false),
                    directors = table.Column<string[]>(type: "text[]", nullable: false),
                    cast = table.Column<string[]>(type: "text[]", nullable: false),
                    countries = table.Column<string[]>(type: "text[]", nullable: false),
                    languages = table.Column<string[]>(type: "text[]", nullable: false),
                    release_year = table.Column<int>(type: "integer", nullable: true),
                    runtime_minutes = table.Column<int>(type: "integer", nullable: true),
                    awards = table.Column<string[]>(type: "text[]", nullable: false),
                    embedding_text = table.Column<string>(type: "text", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_film_facts", x => x.film_id);
                    table.ForeignKey(
                        name: "fk_film_facts_films_film_id",
                        column: x => x.film_id,
                        principalTable: "films",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_film_facts_fetched_at",
                table: "film_facts",
                column: "fetched_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "film_facts");
        }
    }
}
