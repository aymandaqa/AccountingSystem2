using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowStepHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Connector",
                table: "WorkflowSteps",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ParentStepId",
                table: "WorkflowSteps",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_ParentStepId",
                table: "WorkflowSteps",
                column: "ParentStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowSteps_WorkflowSteps_ParentStepId",
                table: "WorkflowSteps",
                column: "ParentStepId",
                principalTable: "WorkflowSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowSteps_WorkflowSteps_ParentStepId",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_ParentStepId",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "Connector",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ParentStepId",
                table: "WorkflowSteps");
        }
    }
}
