using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ithaca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoomResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "decided_at",
                table: "rooms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result_kind",
                table: "rooms",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "result_members",
                table: "rooms",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<Guid[]>(
                name: "result_picks",
                table: "rooms",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<Guid[]>(
                name: "result_votes",
                table: "rooms",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "decided_at",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "result_kind",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "result_members",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "result_picks",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "result_votes",
                table: "rooms");
        }
    }
}
