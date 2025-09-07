using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class ADDCCY1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 360, "العملات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "عرض العملات", true, "currencies.view" },
                    { 370, "العملات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء العملات", true, "currencies.create" },
                    { 380, "العملات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "تعديل العملات", true, "currencies.edit" },
                    { 390, "العملات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "حذف العملات", true, "currencies.delete" }
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
