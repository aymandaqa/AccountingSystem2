using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDb124 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "ReceiptVouchers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "DisbursementVouchers",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "CurrencyId",
                table: "Accounts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptVouchers_CreatedById",
                table: "ReceiptVouchers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementVouchers_CreatedById",
                table: "DisbursementVouchers",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_DisbursementVouchers_AspNetUsers_CreatedById",
                table: "DisbursementVouchers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptVouchers_AspNetUsers_CreatedById",
                table: "ReceiptVouchers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisbursementVouchers_AspNetUsers_CreatedById",
                table: "DisbursementVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_AspNetUsers_CreatedById",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptVouchers_CreatedById",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_DisbursementVouchers_CreatedById",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "DisbursementVouchers");

            migrationBuilder.AlterColumn<int>(
                name: "CurrencyId",
                table: "Accounts",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
