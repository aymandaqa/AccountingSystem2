using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class asswttype1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTypes_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<int>(
                name: "AssetTypeId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetTypeId",
                table: "Assets",
                column: "AssetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes",
                column: "AccountId");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM Assets)
BEGIN
    INSERT INTO AssetTypes (Name, AccountId)
    SELECT DISTINCT
        CONCAT('Auto Asset Type - ', acc.Name),
        acc.Id
    FROM Assets a
    INNER JOIN Accounts acc ON acc.Id = a.AccountId
    LEFT JOIN AssetTypes at ON at.AccountId = a.AccountId
    WHERE a.AccountId IS NOT NULL AND at.Id IS NULL;

    UPDATE a
    SET AssetTypeId = at.Id
    FROM Assets a
    INNER JOIN AssetTypes at ON at.AccountId = a.AccountId
    WHERE a.AccountId IS NOT NULL;

    IF EXISTS (SELECT 1 FROM Assets WHERE AssetTypeId IS NULL)
    BEGIN
        DECLARE @fallbackAccountId INT = (SELECT TOP (1) Id FROM Accounts ORDER BY Id);
        IF @fallbackAccountId IS NOT NULL
        BEGIN
            DECLARE @fallbackAssetTypeId INT;
            INSERT INTO AssetTypes (Name, AccountId)
            VALUES (N'Auto Asset Type - Default', @fallbackAccountId);
            SET @fallbackAssetTypeId = SCOPE_IDENTITY();

            UPDATE Assets
            SET AssetTypeId = @fallbackAssetTypeId
            WHERE AssetTypeId IS NULL;
        END
    END
END
");

            migrationBuilder.AlterColumn<int>(
                name: "AssetTypeId",
                table: "Assets",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_AssetTypes_AssetTypeId",
                table: "Assets",
                column: "AssetTypeId",
                principalTable: "AssetTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_AssetTypes_AssetTypeId",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "AssetTypes");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetTypeId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "AssetTypeId",
                table: "Assets");
        }
    }
}
