using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowAmountsAndVoucherStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MaxAmount",
                table: "WorkflowSteps",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinAmount",
                table: "WorkflowSteps",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DocumentAmount",
                table: "WorkflowInstances",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DocumentAmountInBase",
                table: "WorkflowInstances",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DocumentCurrencyId",
                table: "WorkflowInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ReceiptVouchers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedById",
                table: "ReceiptVouchers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ReceiptVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowInstanceId",
                table: "ReceiptVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "DisbursementVouchers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedById",
                table: "DisbursementVouchers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DisbursementVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowInstanceId",
                table: "DisbursementVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_DocumentCurrencyId",
                table: "WorkflowInstances",
                column: "DocumentCurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptVouchers_ApprovedById",
                table: "ReceiptVouchers",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptVouchers_WorkflowInstanceId",
                table: "ReceiptVouchers",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementVouchers_ApprovedById",
                table: "DisbursementVouchers",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_DisbursementVouchers_WorkflowInstanceId",
                table: "DisbursementVouchers",
                column: "WorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_DisbursementVouchers_AspNetUsers_ApprovedById",
                table: "DisbursementVouchers",
                column: "ApprovedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DisbursementVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "DisbursementVouchers",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptVouchers_AspNetUsers_ApprovedById",
                table: "ReceiptVouchers",
                column: "ApprovedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "ReceiptVouchers",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_Currencies_DocumentCurrencyId",
                table: "WorkflowInstances",
                column: "DocumentCurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id");

            migrationBuilder.Sql("UPDATE ReceiptVouchers SET Status = 1 WHERE Status = 0");
            migrationBuilder.Sql("UPDATE DisbursementVouchers SET Status = 1 WHERE Status = 0");
            migrationBuilder.Sql(@"UPDATE wi
SET DocumentAmount = pv.Amount,
    DocumentAmountInBase = pv.Amount * pv.ExchangeRate,
    DocumentCurrencyId = pv.CurrencyId
FROM WorkflowInstances wi
INNER JOIN PaymentVouchers pv ON pv.WorkflowInstanceId = wi.Id
WHERE wi.DocumentType = 1");
            migrationBuilder.Sql(@"UPDATE wi
SET DocumentAmount = dse.Amount,
    DocumentAmountInBase = dse.Amount
FROM WorkflowInstances wi
INNER JOIN DynamicScreenEntries dse ON dse.WorkflowInstanceId = wi.Id
WHERE wi.DocumentType = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisbursementVouchers_AspNetUsers_ApprovedById",
                table: "DisbursementVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_DisbursementVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "DisbursementVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_AspNetUsers_ApprovedById",
                table: "ReceiptVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "ReceiptVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_Currencies_DocumentCurrencyId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_DocumentCurrencyId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptVouchers_ApprovedById",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptVouchers_WorkflowInstanceId",
                table: "ReceiptVouchers");

            migrationBuilder.DropIndex(
                name: "IX_DisbursementVouchers_ApprovedById",
                table: "DisbursementVouchers");

            migrationBuilder.DropIndex(
                name: "IX_DisbursementVouchers_WorkflowInstanceId",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "MaxAmount",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "MinAmount",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DocumentAmount",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "DocumentAmountInBase",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "DocumentCurrencyId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "ReceiptVouchers");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DisbursementVouchers");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "DisbursementVouchers");
        }
    }
}
