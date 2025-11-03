using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AssetCostCenters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostCenterId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_CostCenterId",
                table: "Assets",
                column: "CostCenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_CostCenters_CostCenterId",
                table: "Assets",
                column: "CostCenterId",
                principalTable: "CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_CostCenters_CostCenterId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_CostCenterId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                table: "Assets");
        }
    }
}
