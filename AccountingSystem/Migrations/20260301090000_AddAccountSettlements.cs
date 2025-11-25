using System;
using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260301090000_AddAccountSettlements")]
    public partial class AddAccountSettlements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountSettlements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountSettlements_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSettlements_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountSettlementPairs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountSettlementId = table.Column<int>(type: "int", nullable: false),
                    DebitLineId = table.Column<int>(type: "int", nullable: false),
                    CreditLineId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountSettlementPairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountSettlementPairs_AccountSettlements_AccountSettlementId",
                        column: x => x.AccountSettlementId,
                        principalTable: "AccountSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccountSettlementPairs_JournalEntryLines_CreditLineId",
                        column: x => x.CreditLineId,
                        principalTable: "JournalEntryLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountSettlementPairs_JournalEntryLines_DebitLineId",
                        column: x => x.DebitLineId,
                        principalTable: "JournalEntryLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountSettlementPairs_AccountSettlementId",
                table: "AccountSettlementPairs",
                column: "AccountSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSettlementPairs_CreditLineId",
                table: "AccountSettlementPairs",
                column: "CreditLineId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSettlementPairs_DebitLineId",
                table: "AccountSettlementPairs",
                column: "DebitLineId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSettlements_AccountId",
                table: "AccountSettlements",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountSettlements_CreatedById",
                table: "AccountSettlements",
                column: "CreatedById");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountSettlementPairs");

            migrationBuilder.DropTable(
                name: "AccountSettlements");
        }
    }
}
