using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VoyageWikidataEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "embedding_source_hash",
                table: "films",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_text_hash",
                table: "film_facts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Same value FilmFacts.HashOf computes (lowercase hex SHA-256 of the UTF-8 text), for rows written before this column.
            // Checked against HashOf for ASCII, multi-line and non-ASCII text; a mismatch would only cost one re-embed.
            migrationBuilder.Sql("UPDATE film_facts SET embedding_text_hash = encode(sha256(convert_to(embedding_text, 'UTF8')), 'hex');");

            // Every vector so far is either fake or built from TMDB text (CatalogSync:EmbedOverviews, now removed). ADR-0017
            // forbids the latter, and neither has a source hash: clear them all; the next sync embeds the Wikidata text.
            migrationBuilder.Sql("UPDATE films SET embedding = NULL, embedding_model = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "embedding_source_hash",
                table: "films");

            migrationBuilder.DropColumn(
                name: "embedding_text_hash",
                table: "film_facts");
        }
    }
}
