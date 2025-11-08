using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherAndTransferAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "ReceiptVouchers",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFilePath",
                table: "ReceiptVouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "PaymentVouchers",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFilePath",
                table: "PaymentVouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "PaymentTransfers",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFilePath",
                table: "PaymentTransfers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "DisbursementVouchers",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFilePath",
                table: "DisbursementVouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "AttachmentFilePath",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "PaymentVouchers");

            migrationBuilder.DropColumn(
                name: "AttachmentFilePath",
                table: "PaymentVouchers");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "PaymentTransfers");

            migrationBuilder.DropColumn(
                name: "AttachmentFilePath",
                table: "PaymentTransfers");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "AttachmentFilePath",
                table: "DisbursementVouchers");
        }
    }
}
