using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class update16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 28, "المصاريف", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "عرض المصاريف", true, "expenses.view" },
                    { 29, "المصاريف", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء المصاريف", true, "expenses.create" },
                    { 30, "المصاريف", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "تعديل المصاريف", true, "expenses.edit" },
                    { 31, "المصاريف", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "حذف المصاريف", true, "expenses.delete" },
                    { 32, "المصاريف", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "اعتماد المصاريف", true, "expenses.approve" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 32);
        }
    }
}
