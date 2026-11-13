using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogDisplayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "backdrop_path",
                table: "films",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_language",
                table: "films",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "vote_average",
                table: "films",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "vote_count",
                table: "films",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "backdrop_path",
                table: "films");

            migrationBuilder.DropColumn(
                name: "original_language",
                table: "films");

            migrationBuilder.DropColumn(
                name: "vote_average",
                table: "films");

            migrationBuilder.DropColumn(
                name: "vote_count",
                table: "films");
        }
    }
}
