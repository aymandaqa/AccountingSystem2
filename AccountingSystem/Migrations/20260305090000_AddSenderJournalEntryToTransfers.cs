using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    public partial class AddSenderJournalEntryToTransfers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SenderJournalEntryId",
                table: "PaymentTransfers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_SenderJournalEntryId",
                table: "PaymentTransfers",
                column: "SenderJournalEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransfers_JournalEntries_SenderJournalEntryId",
                table: "PaymentTransfers",
                column: "SenderJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "DisplayName", "IsActive", "Name", "UpdatedAt" },
                values: new object[] { 113, "الحوالات", createdAt, "إدارة الحوالات", true, "transfers.manage", null });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 113);

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransfers_JournalEntries_SenderJournalEntryId",
                table: "PaymentTransfers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransfers_SenderJournalEntryId",
                table: "PaymentTransfers");

            migrationBuilder.DropColumn(
                name: "SenderJournalEntryId",
                table: "PaymentTransfers");
        }
    }
}
