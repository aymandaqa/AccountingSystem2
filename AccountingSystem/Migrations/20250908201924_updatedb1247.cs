using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class updatedb1247 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 240, "الموردين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "عرض الموردين", true, "suppliers.view" },
                    { 241, "الموردين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء الموردين", true, "suppliers.create" },
                    { 242, "الموردين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "تعديل الموردين", true, "suppliers.edit" },
                    { 243, "الموردين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "حذف الموردين", true, "suppliers.delete" },
                    { 244, "إعدادات النظام", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "عرض إعدادات النظام", true, "systemsettings.view" },
                    { 245, "إعدادات النظام", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء إعدادات النظام", true, "systemsettings.create" },
                    { 246, "إعدادات النظام", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "تعديل إعدادات النظام", true, "systemsettings.edit" },
                    { 247, "إعدادات النظام", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "حذف إعدادات النظام", true, "systemsettings.delete" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_AccountId",
                table: "Suppliers",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 47);
        }
    }
}
