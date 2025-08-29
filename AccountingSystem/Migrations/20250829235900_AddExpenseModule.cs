using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentAccountId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentBranchId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExpenseLimit",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    PaymentAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    BranchId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpenseAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    JournalEntryId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_Accounts_ExpenseAccountId",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Accounts_PaymentAccountId",
                        column: x => x.PaymentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expenses_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PaymentAccountId",
                table: "AspNetUsers",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PaymentBranchId",
                table: "AspNetUsers",
                column: "PaymentBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BranchId",
                table: "Expenses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseAccountId",
                table: "Expenses",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_JournalEntryId",
                table: "Expenses",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_PaymentAccountId",
                table: "Expenses",
                column: "PaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UserId",
                table: "Expenses",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Accounts_PaymentAccountId",
                table: "AspNetUsers",
                column: "PaymentAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Branches_PaymentBranchId",
                table: "AspNetUsers",
                column: "PaymentBranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Accounts_PaymentAccountId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Branches_PaymentBranchId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PaymentAccountId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PaymentBranchId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PaymentAccountId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PaymentBranchId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ExpenseLimit",
                table: "AspNetUsers");
        }
    }
}
