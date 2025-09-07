using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class ADDCCY1211 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 362, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "سندات القبض", true, "receiptvouchers.view" },
                    { 372, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء سند قبض", true, "receiptvouchers.create" },
                    { 382, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "سندات الصرف", true, "disbursementvouchers.view" },
                    { 392, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء سند صرف", true, "disbursementvouchers.create" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 39);
        }
    }
}
