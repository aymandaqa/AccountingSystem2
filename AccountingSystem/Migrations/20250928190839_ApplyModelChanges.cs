using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class ApplyModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes",
                column: "AccountId",
                unique: true);
        }
    }
}
