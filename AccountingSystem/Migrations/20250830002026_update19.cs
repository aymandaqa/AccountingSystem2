using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class update19 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JournalEntryId",
                table: "PaymentTransfers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_JournalEntryId",
                table: "PaymentTransfers",
                column: "JournalEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransfers_JournalEntries_JournalEntryId",
                table: "PaymentTransfers",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransfers_JournalEntries_JournalEntryId",
                table: "PaymentTransfers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransfers_JournalEntryId",
                table: "PaymentTransfers");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "PaymentTransfers");
        }
    }
}
