using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    public partial class AddAssetDisposalFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BookValueAtDisposal",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisposedAt",
                table: "Assets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DisposalProceeds",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DisposalProfitLoss",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDisposed",
                table: "Assets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookValueAtDisposal",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DisposedAt",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DisposalProceeds",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DisposalProfitLoss",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "IsDisposed",
                table: "Assets");
        }
    }
}
