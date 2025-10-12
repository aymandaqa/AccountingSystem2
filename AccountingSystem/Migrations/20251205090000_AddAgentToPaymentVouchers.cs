using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20251205090000_AddAgentToPaymentVouchers")]
    public partial class AddAgentToPaymentVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentId",
                table: "PaymentVouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_AgentId",
                table: "PaymentVouchers",
                column: "AgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentVouchers_Agents_AgentId",
                table: "PaymentVouchers",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentVouchers_Agents_AgentId",
                table: "PaymentVouchers");

            migrationBuilder.DropIndex(
                name: "IX_PaymentVouchers_AgentId",
                table: "PaymentVouchers");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "PaymentVouchers");
        }
    }
}
