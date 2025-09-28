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
                IF EXISTS (SELECT 1 FROM [Assets] WHERE [AssetTypeId] IS NULL)
                BEGIN
                    DECLARE @ParentAccountId INT;
                    SELECT TOP (1) @ParentAccountId = a.[Id]
                    FROM [SystemSettings] s
                    JOIN [Accounts] a ON a.[Code] = s.[Value]
                    WHERE s.[Key] IN (N'AssetTypesParentAccountCode', N'AssetsParentAccountCode')
                        AND s.[Value] IS NOT NULL
                    ORDER BY CASE s.[Key] WHEN N'AssetTypesParentAccountCode' THEN 0 ELSE 1 END;

                    IF @ParentAccountId IS NULL
                    BEGIN
                        SELECT TOP (1) @ParentAccountId = [Id]
                        FROM [Accounts]
                        WHERE [AccountType] = 1
                        ORDER BY [Level], [Id];
                    END

                    IF @ParentAccountId IS NULL
                    BEGIN
                        THROW 51000, N'لم يتم العثور على حساب أصل رئيسي لإنشاء نوع الأصل الافتراضي. يرجى إعداد الحسابات قبل تشغيل الترحيل.', 1;
                    END

                    DECLARE @ParentCode NVARCHAR(20);
                    DECLARE @ParentLevel INT;
                    DECLARE @AccountType INT;
                    DECLARE @Nature INT;
                    DECLARE @Classification INT;
                    DECLARE @SubClassification INT;
                    DECLARE @CurrencyId INT;

                    SELECT
                        @ParentCode = [Code],
                        @ParentLevel = [Level],
                        @AccountType = [AccountType],
                        @Nature = [Nature],
                        @Classification = [Classification],
                        @SubClassification = [SubClassification],
                        @CurrencyId = [CurrencyId]
                    FROM [Accounts]
                    WHERE [Id] = @ParentAccountId;

                    DECLARE @LastChildCode NVARCHAR(20);
                    SELECT TOP (1) @LastChildCode = [Code]
                    FROM [Accounts]
                    WHERE [ParentId] = @ParentAccountId
                    ORDER BY LEN([Code]) DESC, [Code] DESC;

                    DECLARE @SuffixLength INT = 2;
                    DECLARE @Next INT = 1;

                    IF @LastChildCode IS NOT NULL AND LEN(@LastChildCode) > LEN(@ParentCode)
                    BEGIN
                        SET @SuffixLength = CASE WHEN LEN(@LastChildCode) - LEN(@ParentCode) > 2 THEN LEN(@LastChildCode) - LEN(@ParentCode) ELSE 2 END;
                        DECLARE @Suffix NVARCHAR(20) = SUBSTRING(@LastChildCode, LEN(@ParentCode) + 1, 20);
                        IF TRY_CAST(@Suffix AS INT) IS NOT NULL
                        BEGIN
                            SET @Next = TRY_CAST(@Suffix AS INT) + 1;
                        END
                    END

                    DECLARE @NewCode NVARCHAR(20) = @ParentCode + RIGHT(REPLICATE('0', @SuffixLength) + CAST(@Next AS NVARCHAR(20)), @SuffixLength);

                    DECLARE @Now DATETIME2 = SYSUTCDATETIME();

                    INSERT INTO [Accounts]
                        ([Code], [NameAr], [NameEn], [Description], [ParentId], [Level], [AccountType], [Nature], [Classification], [SubClassification], [OpeningBalance], [CurrencyId], [CurrentBalance], [IsActive], [CanHaveChildren], [CanPostTransactions], [BranchId], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@NewCode, N'نوع أصل افتراضي', N'Default Asset Type', N'حساب تم إنشاؤه تلقائيًا لترحيل الأصول الحالية', @ParentAccountId, @ParentLevel + 1, @AccountType, @Nature, @Classification, @SubClassification, 0, @CurrencyId, 0, 1, 1, 0, NULL, @Now, @Now);

                    DECLARE @NewAccountId INT = SCOPE_IDENTITY();

                    INSERT INTO [AssetTypes] ([Name], [AccountId])
                    VALUES (N'نوع أصل افتراضي', @NewAccountId);

                    DECLARE @NewAssetTypeId INT = SCOPE_IDENTITY();

                    UPDATE [Assets]
                    SET [AssetTypeId] = @NewAssetTypeId
                    WHERE [AssetTypeId] IS NULL;
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
