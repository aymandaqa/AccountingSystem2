using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    public partial class AddAssetTypes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssetTypeId",
                table: "Assets",
                type: "int",
                nullable: true);

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

            migrationBuilder.Sql(@"
UPDATE Assets SET Type = LTRIM(RTRIM(Type));

DECLARE @ParentCode NVARCHAR(20);
SELECT TOP 1 @ParentCode = Value FROM SystemSettings WHERE [Key] = 'AssetTypesParentAccountCode';
IF (@ParentCode IS NULL OR LTRIM(RTRIM(@ParentCode)) = '')
BEGIN
    SELECT TOP 1 @ParentCode = Value FROM SystemSettings WHERE [Key] = 'AssetsParentAccountCode';
END

IF (@ParentCode IS NULL OR LTRIM(RTRIM(@ParentCode)) = '')
BEGIN
    THROW 50002, 'يجب ضبط قيمة AssetTypesParentAccountCode أو AssetsParentAccountCode قبل الترقية.', 1;
END

DECLARE @ParentId INT;
SELECT @ParentId = Id FROM Accounts WHERE Code = @ParentCode;

IF (@ParentId IS NULL)
BEGIN
    THROW 50003, 'لم يتم العثور على الحساب الرئيسي لأنواع الأصول.', 1;
END

DECLARE @ParentLevel INT;
DECLARE @AccountType INT;
DECLARE @Nature INT;
DECLARE @Classification INT;
DECLARE @SubClassification INT;
DECLARE @CurrencyId INT;

SELECT
    @ParentLevel = Level,
    @AccountType = AccountType,
    @Nature = Nature,
    @Classification = Classification,
    @SubClassification = SubClassification,
    @CurrencyId = CurrencyId
FROM Accounts
WHERE Id = @ParentId;

DECLARE type_cursor CURSOR FOR
SELECT DISTINCT LTRIM(RTRIM(Type))
FROM Assets
WHERE Type IS NOT NULL AND LTRIM(RTRIM(Type)) <> '';

OPEN type_cursor;
DECLARE @TypeName NVARCHAR(200);

WHILE 1=1
BEGIN
    FETCH NEXT FROM type_cursor INTO @TypeName;
    IF @@FETCH_STATUS <> 0 BREAK;

    IF (@TypeName IS NULL OR @TypeName = '')
        CONTINUE;

    DECLARE @LastChildCode NVARCHAR(20);
    SELECT TOP 1 @LastChildCode = Code FROM Accounts WHERE ParentId = @ParentId ORDER BY LEN(Code) DESC, Code DESC;

    DECLARE @SuffixLength INT = CASE WHEN @LastChildCode IS NOT NULL AND LEN(@LastChildCode) > LEN(@ParentCode)
                                     THEN LEN(@LastChildCode) - LEN(@ParentCode)
                                     ELSE 2 END;
    IF (@SuffixLength < 2) SET @SuffixLength = 2;

    DECLARE @Next INT = 1;
    IF (@LastChildCode IS NOT NULL)
    BEGIN
        DECLARE @Suffix NVARCHAR(20) = RIGHT(@LastChildCode, @SuffixLength);
        IF TRY_CONVERT(INT, @Suffix) IS NOT NULL
            SET @Next = TRY_CONVERT(INT, @Suffix) + 1;
    END

    DECLARE @NewCode NVARCHAR(20) = @ParentCode + RIGHT(REPLICATE('0', @SuffixLength) + CAST(@Next AS NVARCHAR(20)), @SuffixLength);

    INSERT INTO Accounts (Code, NameAr, NameEn, Description, ParentId, Level, AccountType, Nature, Classification, SubClassification, OpeningBalance, CurrencyId, CurrentBalance, IsActive, CanHaveChildren, CanPostTransactions, BranchId, CreatedAt)
    VALUES (@NewCode, @TypeName, @TypeName, CONCAT(N'حساب نوع أصل: ', @TypeName), @ParentId, @ParentLevel + 1, @AccountType, @Nature, @Classification, @SubClassification, 0, @CurrencyId, 0, 1, 1, 0, NULL, GETDATE());

    DECLARE @AccountId INT = SCOPE_IDENTITY();

    INSERT INTO AssetTypes (Name, AccountId)
    VALUES (@TypeName, @AccountId);

    DECLARE @AssetTypeId INT = SCOPE_IDENTITY();

    UPDATE Assets SET AssetTypeId = @AssetTypeId WHERE LTRIM(RTRIM(Type)) = @TypeName;
END

CLOSE type_cursor;
DEALLOCATE type_cursor;

IF EXISTS (SELECT 1 FROM Assets WHERE AssetTypeId IS NULL)
BEGIN
    DECLARE @DefaultTypeName NVARCHAR(200) = N'أصول أخرى';
    DECLARE @DefaultAssetTypeId INT;
    SELECT @DefaultAssetTypeId = Id FROM AssetTypes WHERE Name = @DefaultTypeName;

    IF (@DefaultAssetTypeId IS NULL)
    BEGIN
        DECLARE @LastChildCode2 NVARCHAR(20);
        SELECT TOP 1 @LastChildCode2 = Code FROM Accounts WHERE ParentId = @ParentId ORDER BY LEN(Code) DESC, Code DESC;

        DECLARE @SuffixLength2 INT = CASE WHEN @LastChildCode2 IS NOT NULL AND LEN(@LastChildCode2) > LEN(@ParentCode)
                                         THEN LEN(@LastChildCode2) - LEN(@ParentCode)
                                         ELSE 2 END;
        IF (@SuffixLength2 < 2) SET @SuffixLength2 = 2;

        DECLARE @Next2 INT = 1;
        IF (@LastChildCode2 IS NOT NULL)
        BEGIN
            DECLARE @Suffix2 NVARCHAR(20) = RIGHT(@LastChildCode2, @SuffixLength2);
            IF TRY_CONVERT(INT, @Suffix2) IS NOT NULL
                SET @Next2 = TRY_CONVERT(INT, @Suffix2) + 1;
        END

        DECLARE @NewCode2 NVARCHAR(20) = @ParentCode + RIGHT(REPLICATE('0', @SuffixLength2) + CAST(@Next2 AS NVARCHAR(20)), @SuffixLength2);

        INSERT INTO Accounts (Code, NameAr, NameEn, Description, ParentId, Level, AccountType, Nature, Classification, SubClassification, OpeningBalance, CurrencyId, CurrentBalance, IsActive, CanHaveChildren, CanPostTransactions, BranchId, CreatedAt)
        VALUES (@NewCode2, @DefaultTypeName, @DefaultTypeName, CONCAT(N'حساب نوع أصل: ', @DefaultTypeName), @ParentId, @ParentLevel + 1, @AccountType, @Nature, @Classification, @SubClassification, 0, @CurrencyId, 0, 1, 1, 0, NULL, GETDATE());

        DECLARE @DefaultAccountId INT = SCOPE_IDENTITY();

        INSERT INTO AssetTypes (Name, AccountId)
        VALUES (@DefaultTypeName, @DefaultAccountId);

        SET @DefaultAssetTypeId = SCOPE_IDENTITY();
    END

    UPDATE Assets SET AssetTypeId = @DefaultAssetTypeId WHERE AssetTypeId IS NULL;
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

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Assets");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_AccountId",
                table: "AssetTypes",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AssetTypeId",
                table: "Assets",
                column: "AssetTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_AssetTypes_AssetTypeId",
                table: "Assets",
                column: "AssetTypeId",
                principalTable: "AssetTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_AssetTypes_AssetTypeId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_AssetTypeId",
                table: "Assets");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Assets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE A
SET A.Type = ISNULL(T.Name, '')
FROM Assets A
LEFT JOIN AssetTypes T ON A.AssetTypeId = T.Id;
");

            migrationBuilder.DropColumn(
                name: "AssetTypeId",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "AssetTypes");
        }
    }
}
