using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class update17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SenderId = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiverId = table.Column<string>(type: "TEXT", nullable: false),
                    FromPaymentAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToPaymentAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromBranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    ToBranchId = table.Column<int>(type: "INTEGER", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransfers_Accounts_FromPaymentAccountId",
                        column: x => x.FromPaymentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransfers_Accounts_ToPaymentAccountId",
                        column: x => x.ToPaymentAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransfers_AspNetUsers_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransfers_AspNetUsers_SenderId",
                        column: x => x.SenderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransfers_Branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentTransfers_Branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 33, "الحوالات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "عرض الحوالات", true, "transfers.view" },
                    { 34, "الحوالات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إنشاء الحوالات", true, "transfers.create" },
                    { 35, "الحوالات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "اعتماد الحوالات", true, "transfers.approve" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_FromBranchId",
                table: "PaymentTransfers",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_FromPaymentAccountId",
                table: "PaymentTransfers",
                column: "FromPaymentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_ReceiverId",
                table: "PaymentTransfers",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_SenderId",
                table: "PaymentTransfers",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_ToBranchId",
                table: "PaymentTransfers",
                column: "ToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransfers_ToPaymentAccountId",
                table: "PaymentTransfers",
                column: "ToPaymentAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentTransfers");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 35);
        }
    }
}
