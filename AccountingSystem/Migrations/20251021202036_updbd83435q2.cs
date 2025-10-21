using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class updbd83435q2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisbursementVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "DisbursementVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "ReceiptVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_Currencies_DocumentCurrencyId",
                table: "WorkflowInstances");

            migrationBuilder.AddForeignKey(
                name: "FK_DisbursementVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "DisbursementVouchers",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "ReceiptVouchers",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowInstances_Currencies_DocumentCurrencyId",
                table: "WorkflowInstances",
                column: "DocumentCurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DisbursementVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "DisbursementVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "ReceiptVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowInstances_Currencies_DocumentCurrencyId",
                table: "WorkflowInstances");

            migrationBuilder.AddForeignKey(
                name: "FK_DisbursementVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "DisbursementVouchers",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
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
        }
    }
}
