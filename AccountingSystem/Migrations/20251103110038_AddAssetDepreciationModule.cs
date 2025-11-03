using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetDepreciationModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccumulatedDepreciationAccountId",
                table: "AssetTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepreciationExpenseAccountId",
                table: "AssetTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDepreciable",
                table: "AssetTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "AccumulatedDepreciation",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BookValue",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DepreciationFrequency",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepreciationPeriods",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalCost",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "Assets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalvageValue",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssetDepreciations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetId = table.Column<int>(type: "int", nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BookValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    JournalEntryId = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDepreciations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDepreciations_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDepreciations_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDepreciations_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_AccumulatedDepreciationAccountId",
                table: "AssetTypes",
                column: "AccumulatedDepreciationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_DepreciationExpenseAccountId",
                table: "AssetTypes",
                column: "DepreciationExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciations_AssetId_PeriodNumber",
                table: "AssetDepreciations",
                columns: new[] { "AssetId", "PeriodNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciations_CreatedById",
                table: "AssetDepreciations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDepreciations_JournalEntryId",
                table: "AssetDepreciations",
                column: "JournalEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetTypes_Accounts_AccumulatedDepreciationAccountId",
                table: "AssetTypes",
                column: "AccumulatedDepreciationAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetTypes_Accounts_DepreciationExpenseAccountId",
                table: "AssetTypes",
                column: "DepreciationExpenseAccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetTypes_Accounts_AccumulatedDepreciationAccountId",
                table: "AssetTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_AssetTypes_Accounts_DepreciationExpenseAccountId",
                table: "AssetTypes");

            migrationBuilder.DropTable(
                name: "AssetDepreciations");

            migrationBuilder.DropIndex(
                name: "IX_AssetTypes_AccumulatedDepreciationAccountId",
                table: "AssetTypes");

            migrationBuilder.DropIndex(
                name: "IX_AssetTypes_DepreciationExpenseAccountId",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "AccumulatedDepreciationAccountId",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "DepreciationExpenseAccountId",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "IsDepreciable",
                table: "AssetTypes");

            migrationBuilder.DropColumn(
                name: "AccumulatedDepreciation",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "BookValue",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DepreciationFrequency",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DepreciationPeriods",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "OriginalCost",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "SalvageValue",
                table: "Assets");
        }
    }
}
