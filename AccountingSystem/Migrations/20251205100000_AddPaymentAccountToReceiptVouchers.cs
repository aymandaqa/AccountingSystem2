using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentAccountToReceiptVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentAccountId",
                table: "ReceiptVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE rv
                SET PaymentAccountId = rv.AccountId
                FROM ReceiptVouchers rv");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptVouchers_PaymentAccountId",
                table: "ReceiptVouchers",
                column: "PaymentAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptVouchers_Accounts_PaymentAccountId",
                table: "ReceiptVouchers",
                column: "PaymentAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_Accounts_PaymentAccountId",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptVouchers_PaymentAccountId",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "PaymentAccountId",
                table: "ReceiptVouchers");
        }
    }
}
