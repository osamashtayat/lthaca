using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VoyageEmbeddingsAndPopularity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old 1536-dim vectors can't be cast to 1024 dims; they must be re-embedded anyway (ADR-0013).
            migrationBuilder.Sql("UPDATE films SET embedding = NULL;");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "films",
                type: "vector(1024)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "films",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "popularity",
                table: "films",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE films SET embedding = NULL;");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "films");

            migrationBuilder.DropColumn(
                name: "popularity",
                table: "films");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "films",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1024)",
                oldNullable: true);
        }
    }
}
