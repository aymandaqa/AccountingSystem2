using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetAccountingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalance",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AccountId",
                table: "Assets",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Accounts_AccountId",
                table: "Assets",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Accounts_AccountId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AccountId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OpeningBalance",
                table: "Assets");
        }
    }
}
