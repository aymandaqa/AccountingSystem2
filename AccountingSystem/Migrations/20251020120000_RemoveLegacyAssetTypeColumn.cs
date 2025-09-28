using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20251020120000_RemoveLegacyAssetTypeColumn")]
    public partial class RemoveLegacyAssetTypeColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Assets");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Assets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: string.Empty);
        }
    }
}
