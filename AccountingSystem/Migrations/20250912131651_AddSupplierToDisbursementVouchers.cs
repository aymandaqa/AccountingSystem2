using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierToDisbursementVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "DisbursementVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementVouchers_SupplierId",
                table: "DisbursementVouchers",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_DisbursementVouchers_Suppliers_SupplierId",
                table: "DisbursementVouchers",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisbursementVouchers_Suppliers_SupplierId",
                table: "DisbursementVouchers");

            migrationBuilder.DropIndex(
                name: "IX_DisbursementVouchers_SupplierId",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "DisbursementVouchers");
        }
    }
}
