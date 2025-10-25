using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDeductionsAndTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountId",
                table: "PayrollBatchLineDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeductionTypeId",
                table: "PayrollBatchLineDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeductionTypes",
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
                    table.PrimaryKey("PK_DeductionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeductionTypes_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDeductions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    DeductionTypeId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_DeductionTypes_DeductionTypeId",
                        column: x => x.DeductionTypeId,
                        principalTable: "DeductionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeductions_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatchLineDeductions_AccountId",
                table: "PayrollBatchLineDeductions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatchLineDeductions_DeductionTypeId",
                table: "PayrollBatchLineDeductions",
                column: "DeductionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionTypes_AccountId",
                table: "DeductionTypes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_DeductionTypeId",
                table: "EmployeeDeductions",
                column: "DeductionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeductions_EmployeeId",
                table: "EmployeeDeductions",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollBatchLineDeductions_Accounts_AccountId",
                table: "PayrollBatchLineDeductions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollBatchLineDeductions_DeductionTypes_DeductionTypeId",
                table: "PayrollBatchLineDeductions",
                column: "DeductionTypeId",
                principalTable: "DeductionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollBatchLineDeductions_Accounts_AccountId",
                table: "PayrollBatchLineDeductions");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollBatchLineDeductions_DeductionTypes_DeductionTypeId",
                table: "PayrollBatchLineDeductions");

            migrationBuilder.DropTable(
                name: "EmployeeDeductions");

            migrationBuilder.DropTable(
                name: "DeductionTypes");

            migrationBuilder.DropIndex(
                name: "IX_PayrollBatchLineDeductions_AccountId",
                table: "PayrollBatchLineDeductions");

            migrationBuilder.DropIndex(
                name: "IX_PayrollBatchLineDeductions_DeductionTypeId",
                table: "PayrollBatchLineDeductions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "PayrollBatchLineDeductions");

            migrationBuilder.DropColumn(
                name: "DeductionTypeId",
                table: "PayrollBatchLineDeductions");
        }
    }
}
