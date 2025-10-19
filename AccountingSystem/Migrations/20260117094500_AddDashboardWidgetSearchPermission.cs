using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardWidgetSearchPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = N'dashboard.widget.search')
BEGIN
    INSERT INTO [Permissions] ([Id], [Category], [CreatedAt], [DisplayName], [IsActive], [Name])
    VALUES (112, N'لوحة التحكم', '{createdAt:yyyy-MM-ddTHH:mm:ss}', N'عرض البحث السريع بلوحة التحكم', 1, N'dashboard.widget.search');
END");

            migrationBuilder.Sql(@"
INSERT INTO [UserPermissions] ([UserId], [PermissionId], [IsGranted], [CreatedAt])
SELECT up.[UserId], 112, up.[IsGranted], up.[CreatedAt]
FROM [UserPermissions] up
WHERE up.[PermissionId] = 27
  AND up.[IsGranted] = 1
  AND NOT EXISTS (
        SELECT 1
        FROM [UserPermissions] existing
        WHERE existing.[UserId] = up.[UserId]
          AND existing.[PermissionId] = 112
    );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [UserPermissions] WHERE [PermissionId] = 112;");
            migrationBuilder.Sql("DELETE FROM [Permissions] WHERE [Id] = 112 AND [Name] = N'dashboard.widget.search';");
        }
    }
}
