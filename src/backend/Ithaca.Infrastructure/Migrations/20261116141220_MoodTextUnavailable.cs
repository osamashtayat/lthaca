using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoodTextUnavailable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "text_unavailable",
                table: "mood_queries",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "text_unavailable",
                table: "mood_queries");
        }
    }
}
