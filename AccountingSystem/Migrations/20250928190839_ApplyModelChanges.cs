using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class ApplyModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AssetTypes_AccountId'
      AND object_id = OBJECT_ID(N'[dbo].[AssetTypes]')
)
BEGIN
    DROP INDEX [IX_AssetTypes_AccountId] ON [dbo].[AssetTypes];
END");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AssetTypes_AccountId'
      AND object_id = OBJECT_ID(N'[dbo].[AssetTypes]')
)
BEGIN
    DROP INDEX [IX_AssetTypes_AccountId] ON [dbo].[AssetTypes];
END");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes",
                column: "AccountId",
                unique: true);
        }
    }
}
