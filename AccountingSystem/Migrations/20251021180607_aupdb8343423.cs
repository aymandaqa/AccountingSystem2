using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class aupdb8343423 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.AddColumn<int>(
                name: "SenderJournalEntryId",
                table: "PaymentTransfers",
                type: "int",
                nullable: true);



            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                     { 1131, "الحوالات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إدارة الحوالات", true, "transfers.manage" }
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransfers_JournalEntries_SenderJournalEntryId",
                table: "PaymentTransfers");


            migrationBuilder.DropIndex(
                name: "IX_PaymentTransfers_SenderJournalEntryId",
                table: "PaymentTransfers");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 113);


            migrationBuilder.DropColumn(
                name: "SenderJournalEntryId",
                table: "PaymentTransfers");

        }
    }
}
