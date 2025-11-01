using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    public partial class AddPeriodToEmployeeAllowancesAndDeductions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var currentDate = DateTime.UtcNow;

            migrationBuilder.AddColumn<int>(
                name: "Month",
                table: "EmployeeAllowances",
                type: "int",
                nullable: false,
                defaultValue: currentDate.Month);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "EmployeeAllowances",
                type: "int",
                nullable: false,
                defaultValue: currentDate.Year);

            migrationBuilder.AddColumn<int>(
                name: "Month",
                table: "EmployeeDeductions",
                type: "int",
                nullable: false,
                defaultValue: currentDate.Month);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "EmployeeDeductions",
                type: "int",
                nullable: false,
                defaultValue: currentDate.Year);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Month",
                table: "EmployeeAllowances");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "EmployeeAllowances");

            migrationBuilder.DropColumn(
                name: "Month",
                table: "EmployeeDeductions");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "EmployeeDeductions");
        }
    }
}
