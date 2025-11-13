using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetExpenseWorkflowInstance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkflowInstanceId",
                table: "AssetExpenses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetExpenses_WorkflowInstanceId",
                table: "AssetExpenses",
                column: "WorkflowInstanceId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetExpenses_WorkflowInstances_WorkflowInstanceId",
                table: "AssetExpenses",
                column: "WorkflowInstanceId",
                principalTable: "WorkflowInstances",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetExpenses_WorkflowInstances_WorkflowInstanceId",
                table: "AssetExpenses");

            migrationBuilder.DropIndex(
                name: "IX_AssetExpenses_WorkflowInstanceId",
                table: "AssetExpenses");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "AssetExpenses");
        }
    }
}
