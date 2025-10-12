using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierToReceiptVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "ReceiptVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptVouchers_SupplierId",
                table: "ReceiptVouchers",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptVouchers_Suppliers_SupplierId",
                table: "ReceiptVouchers",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_Suppliers_SupplierId",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptVouchers_SupplierId",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "ReceiptVouchers");
        }
    }
}
