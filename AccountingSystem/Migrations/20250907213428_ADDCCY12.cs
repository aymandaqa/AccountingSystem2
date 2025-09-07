using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class ADDCCY12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "ReceiptVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "DisbursementVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptVouchers_CurrencyId",
                table: "ReceiptVouchers",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementVouchers_CurrencyId",
                table: "DisbursementVouchers",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_DisbursementVouchers_Currencies_CurrencyId",
                table: "DisbursementVouchers",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptVouchers_Currencies_CurrencyId",
                table: "ReceiptVouchers",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisbursementVouchers_Currencies_CurrencyId",
                table: "DisbursementVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_Currencies_CurrencyId",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptVouchers_CurrencyId",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_DisbursementVouchers_CurrencyId",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "DisbursementVouchers");
        }
    }
}
