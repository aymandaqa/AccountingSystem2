using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountStatementQuickLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[] { 72, "التقارير", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "عرض كشف الحساب من السجلات", true, "reports.accountstatement" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 72);
        }
    }
}
