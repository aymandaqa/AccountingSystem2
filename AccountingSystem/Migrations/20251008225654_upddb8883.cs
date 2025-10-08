using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class upddb8883 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_ApprovedById",
                table: "PaymentVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "PaymentVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_Branches_BranchId",
                table: "WorkflowDefinitions");


            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_ApprovedById",
                table: "PaymentVouchers",
                column: "ApprovedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "PaymentVouchers",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_Branches_BranchId",
                table: "WorkflowDefinitions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_ApprovedById",
                table: "PaymentVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "PaymentVouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowDefinitions_Branches_BranchId",
                table: "WorkflowDefinitions");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_AspNetUsers_ApprovedById",
                table: "PaymentVouchers",
                column: "ApprovedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_WorkflowInstances_WorkflowInstanceId",
                table: "PaymentVouchers",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowDefinitions_Branches_BranchId",
                table: "WorkflowDefinitions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
