using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmployeeLoanInstallmentId",
                table: "PayrollBatchLineDeductions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeLoans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InstallmentCount = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeLoans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLoanInstallments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeLoanId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayrollBatchLineId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLoanInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLoanInstallments_EmployeeLoans_EmployeeLoanId",
                        column: x => x.EmployeeLoanId,
                        principalTable: "EmployeeLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeLoanInstallments_PayrollBatchLines_PayrollBatchLineId",
                        column: x => x.PayrollBatchLineId,
                        principalTable: "PayrollBatchLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatchLineDeductions_EmployeeLoanInstallmentId",
                table: "PayrollBatchLineDeductions",
                column: "EmployeeLoanInstallmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoanInstallments_EmployeeLoanId",
                table: "EmployeeLoanInstallments",
                column: "EmployeeLoanId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoanInstallments_PayrollBatchLineId",
                table: "EmployeeLoanInstallments",
                column: "PayrollBatchLineId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_AccountId",
                table: "EmployeeLoans",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_CreatedById",
                table: "EmployeeLoans",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLoans_EmployeeId",
                table: "EmployeeLoans",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollBatchLineDeductions_EmployeeLoanInstallments_EmployeeLoanInstallmentId",
                table: "PayrollBatchLineDeductions",
                column: "EmployeeLoanInstallmentId",
                principalTable: "EmployeeLoanInstallments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollBatchLineDeductions_EmployeeLoanInstallments_EmployeeLoanInstallmentId",
                table: "PayrollBatchLineDeductions");

            migrationBuilder.DropTable(
                name: "EmployeeLoanInstallments");

            migrationBuilder.DropTable(
                name: "EmployeeLoans");

            migrationBuilder.DropIndex(
                name: "IX_PayrollBatchLineDeductions_EmployeeLoanInstallmentId",
                table: "PayrollBatchLineDeductions");

            migrationBuilder.DropColumn(
                name: "EmployeeLoanInstallmentId",
                table: "PayrollBatchLineDeductions");
        }
    }
}
