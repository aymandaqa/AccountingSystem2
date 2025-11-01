using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowancesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AllowanceAmount",
                table: "PayrollBatchLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "AllowanceTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowanceTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllowanceTypes_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAllowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AllowanceTypeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAllowances_AllowanceTypes_AllowanceTypeId",
                        column: x => x.AllowanceTypeId,
                        principalTable: "AllowanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeAllowances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBatchLineAllowances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayrollBatchLineId = table.Column<int>(type: "int", nullable: false),
                    AllowanceTypeId = table.Column<int>(type: "int", nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBatchLineAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBatchLineAllowances_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollBatchLineAllowances_AllowanceTypes_AllowanceTypeId",
                        column: x => x.AllowanceTypeId,
                        principalTable: "AllowanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PayrollBatchLineAllowances_PayrollBatchLines_PayrollBatchLineId",
                        column: x => x.PayrollBatchLineId,
                        principalTable: "PayrollBatchLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceTypes_AccountId",
                table: "AllowanceTypes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAllowances_AllowanceTypeId",
                table: "EmployeeAllowances",
                column: "AllowanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAllowances_EmployeeId",
                table: "EmployeeAllowances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatchLineAllowances_AccountId",
                table: "PayrollBatchLineAllowances",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatchLineAllowances_AllowanceTypeId",
                table: "PayrollBatchLineAllowances",
                column: "AllowanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatchLineAllowances_PayrollBatchLineId",
                table: "PayrollBatchLineAllowances",
                column: "PayrollBatchLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeAllowances");

            migrationBuilder.DropTable(
                name: "PayrollBatchLineAllowances");

            migrationBuilder.DropTable(
                name: "AllowanceTypes");

            migrationBuilder.DropColumn(
                name: "AllowanceAmount",
                table: "PayrollBatchLines");
        }
    }
}
