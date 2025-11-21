using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierTypesManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                schema: "dbo",
                table: "Suppliers",
                newName: "SupplierTypeId");

            migrationBuilder.AlterColumn<int>(
                name: "SupplierTypeId",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "SupplierTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SupplierTypes",
                columns: new[] { "Id", "IsActive", "Name" },
                values: new object[,]
                {
                    { 0, true, "غير محدد" },
                    { 1, true, "شركة" },
                    { 2, true, "فرد" },
                    { 3, true, "مورد خارجي" }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 115, "الموردين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "عرض أنواع الموردين", true, "suppliertypes.view" },
                    { 116, "الموردين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء نوع مورد", true, "suppliertypes.create" },
                    { 117, "الموردين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "تعديل نوع مورد", true, "suppliertypes.edit" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_SupplierTypeId",
                table: "Suppliers",
                column: "SupplierTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_SupplierTypes_SupplierTypeId",
                table: "Suppliers",
                column: "SupplierTypeId",
                principalTable: "SupplierTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_SupplierTypes_SupplierTypeId",
                table: "Suppliers");

            migrationBuilder.DropTable(
                name: "SupplierTypes");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_SupplierTypeId",
                table: "Suppliers");

            migrationBuilder.AlterColumn<int>(
                name: "SupplierTypeId",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 2);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 117);

            migrationBuilder.RenameColumn(
                name: "SupplierTypeId",
                schema: "dbo",
                table: "Suppliers",
                newName: "Type");
        }
    }
}
