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
            // In some environments the old "Type" column might have already been removed or renamed,
            // so guard the rename to avoid the SQL Server ambiguity error.
            migrationBuilder.Sql(
                @"IF COL_LENGTH('dbo.Suppliers', 'Type') IS NOT NULL AND COL_LENGTH('dbo.Suppliers', 'SupplierTypeId') IS NULL
BEGIN
    EXEC sp_rename N'[dbo].[Suppliers].[Type]', N'SupplierTypeId', 'COLUMN';
END");

            // Ensure the SupplierTypeId column exists before altering it (for databases where Type never existed).
            migrationBuilder.Sql(
                @"IF COL_LENGTH('dbo.Suppliers', 'SupplierTypeId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Suppliers]
    ADD [SupplierTypeId] int NOT NULL CONSTRAINT DF_Suppliers_SupplierTypeId DEFAULT 2;
    -- Remove the default constraint to let EF handle defaults below
    DECLARE @df_name nvarchar(128);
    SELECT @df_name = df.name
    FROM sys.default_constraints df
    INNER JOIN sys.columns c ON c.default_object_id = df.object_id
    INNER JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = 'Suppliers' AND c.name = 'SupplierTypeId';
    IF @df_name IS NOT NULL EXEC('ALTER TABLE [dbo].[Suppliers] DROP CONSTRAINT [' + @df_name + ']');
END");

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

            migrationBuilder.Sql(
                @"IF COL_LENGTH('dbo.Suppliers', 'Type') IS NULL AND COL_LENGTH('dbo.Suppliers', 'SupplierTypeId') IS NOT NULL
BEGIN
    EXEC sp_rename N'[dbo].[Suppliers].[SupplierTypeId]', N'Type', 'COLUMN';
END");
        }
    }
}
