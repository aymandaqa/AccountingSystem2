using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierToAssetExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "AssetExpenses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM AssetExpenses WHERE SupplierId = 0)
                BEGIN
                    DECLARE @FirstSupplierId INT = (SELECT TOP 1 Id FROM Suppliers ORDER BY Id);
                    IF @FirstSupplierId IS NOT NULL
                    BEGIN
                        UPDATE AssetExpenses SET SupplierId = @FirstSupplierId WHERE SupplierId = 0;
                    END
                END");

            migrationBuilder.CreateIndex(
                name: "IX_AssetExpenses_SupplierId",
                table: "AssetExpenses",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetExpenses_Suppliers_SupplierId",
                table: "AssetExpenses",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetExpenses_Suppliers_SupplierId",
                table: "AssetExpenses");

            migrationBuilder.DropIndex(
                name: "IX_AssetExpenses_SupplierId",
                table: "AssetExpenses");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "AssetExpenses");
        }
    }
}
