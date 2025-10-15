using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicScreens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DynamicScreenDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ScreenType = table.Column<int>(type: "int", nullable: false),
                    PaymentMode = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    WorkflowDefinitionId = table.Column<int>(type: "int", nullable: true),
                    MenuOrder = table.Column<int>(type: "int", nullable: false),
                    PermissionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ManagePermissionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LayoutDefinition = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicScreenDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicScreenDefinitions_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenDefinitions_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenDefinitions_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DynamicScreenEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScreenId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsCash = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExpenseAccountId = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    ScreenType = table.Column<int>(type: "int", nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowInstanceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicScreenEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_Accounts_ExpenseAccountId",
                        column: x => x.ExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_AspNetUsers_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_AspNetUsers_RejectedById",
                        column: x => x.RejectedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_DynamicScreenDefinitions_ScreenId",
                        column: x => x.ScreenId,
                        principalTable: "DynamicScreenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DynamicScreenEntries_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DynamicScreenFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScreenId = table.Column<int>(type: "int", nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    DataSource = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ColumnSpan = table.Column<int>(type: "int", nullable: false),
                    Placeholder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HelpText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AllowedEntityIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DynamicScreenFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DynamicScreenFields_DynamicScreenDefinitions_ScreenId",
                        column: x => x.ScreenId,
                        principalTable: "DynamicScreenDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "Description", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 70, "التقارير", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "اختصار كشف الحساب المباشر", true, "reports.quickaccountstatement" },
                    { 71, "الشاشات الديناميكية", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "إدارة الشاشات الديناميكية", true, "dynamicscreens.manage" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenDefinitions_CreatedById",
                table: "DynamicScreenDefinitions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenDefinitions_Name",
                table: "DynamicScreenDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenDefinitions_UpdatedById",
                table: "DynamicScreenDefinitions",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenDefinitions_WorkflowDefinitionId",
                table: "DynamicScreenDefinitions",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_ApprovedById",
                table: "DynamicScreenEntries",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_BranchId",
                table: "DynamicScreenEntries",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_CreatedById",
                table: "DynamicScreenEntries",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_ExpenseAccountId",
                table: "DynamicScreenEntries",
                column: "ExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_RejectedById",
                table: "DynamicScreenEntries",
                column: "RejectedById");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_ScreenId",
                table: "DynamicScreenEntries",
                column: "ScreenId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_SupplierId",
                table: "DynamicScreenEntries",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenEntries_WorkflowInstanceId",
                table: "DynamicScreenEntries",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_DynamicScreenFields_ScreenId_FieldKey",
                table: "DynamicScreenFields",
                columns: new[] { "ScreenId", "FieldKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DynamicScreenEntries");

            migrationBuilder.DropTable(
                name: "DynamicScreenFields");

            migrationBuilder.DropTable(
                name: "DynamicScreenDefinitions");

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 71);
        }
    }
}
