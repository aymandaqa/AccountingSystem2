using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddBrowserAndLocationToUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrowserIcon",
                table: "UserSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BrowserName",
                table: "UserSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "UserSessions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationAccuracy",
                table: "UserSessions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LocationCapturedAt",
                table: "UserSessions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "UserSessions",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrowserIcon",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "BrowserName",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "LocationAccuracy",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "LocationCapturedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "UserSessions");
        }
    }
}
