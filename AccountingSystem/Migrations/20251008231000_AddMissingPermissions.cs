using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Keep this list in sync with the permissions seeded from ApplicationDbContext.SeedData.
            var permissions = new (int Id, string Category, string DisplayName, string Name)[]
            {
                (1, "المستخدمين", "عرض المستخدمين", "users.view"),
                (2, "المستخدمين", "إنشاء المستخدمين", "users.create"),
                (3, "المستخدمين", "تعديل المستخدمين", "users.edit"),
                (4, "المستخدمين", "حذف المستخدمين", "users.delete"),
                (5, "الفروع", "عرض الفروع", "branches.view"),
                (6, "الفروع", "إنشاء الفروع", "branches.create"),
                (7, "الفروع", "تعديل الفروع", "branches.edit"),
                (8, "الفروع", "حذف الفروع", "branches.delete"),
                (9, "مراكز التكلفة", "عرض مراكز التكلفة", "costcenters.view"),
                (10, "مراكز التكلفة", "إنشاء مراكز التكلفة", "costcenters.create"),
                (11, "مراكز التكلفة", "تعديل مراكز التكلفة", "costcenters.edit"),
                (12, "مراكز التكلفة", "حذف مراكز التكلفة", "costcenters.delete"),
                (13, "الحسابات", "عرض الحسابات", "accounts.view"),
                (14, "الحسابات", "إنشاء الحسابات", "accounts.create"),
                (15, "الحسابات", "تعديل الحسابات", "accounts.edit"),
                (16, "الحسابات", "حذف الحسابات", "accounts.delete"),
                (17, "القيود المالية", "عرض القيود", "journal.view"),
                (18, "القيود المالية", "إنشاء القيود", "journal.create"),
                (19, "القيود المالية", "تعديل القيود", "journal.edit"),
                (20, "القيود المالية", "حذف القيود", "journal.delete"),
                (21, "القيود المالية", "اعتماد القيود", "journal.approve"),
                (22, "التقارير", "عرض التقارير", "reports.view"),
                (23, "التقارير", "تصدير التقارير", "reports.export"),
                (24, "لوحة التحكم", "عرض لوحة التحكم", "dashboard.view"),
                (25, "لوحة التحكم", "عرض إحصائيات لوحة التحكم", "dashboard.widget.stats"),
                (26, "لوحة التحكم", "عرض أرصدة الحسابات بلوحة التحكم", "dashboard.widget.accounts"),
                (27, "لوحة التحكم", "عرض الروابط السريعة بلوحة التحكم", "dashboard.widget.links"),
                (28, "المصاريف", "عرض المصاريف", "expenses.view"),
                (29, "المصاريف", "إنشاء المصاريف", "expenses.create"),
                (30, "المصاريف", "تعديل المصاريف", "expenses.edit"),
                (31, "المصاريف", "حذف المصاريف", "expenses.delete"),
                (32, "المصاريف", "اعتماد المصاريف", "expenses.approve"),
                (33, "الحوالات", "عرض الحوالات", "transfers.view"),
                (34, "الحوالات", "إنشاء الحوالات", "transfers.create"),
                (35, "الحوالات", "اعتماد الحوالات", "transfers.approve"),
                (36, "العملات", "عرض العملات", "currencies.view"),
                (37, "العملات", "إنشاء العملات", "currencies.create"),
                (38, "العملات", "تعديل العملات", "currencies.edit"),
                (39, "العملات", "حذف العملات", "currencies.delete"),
                (40, "الموردين", "عرض الموردين", "suppliers.view"),
                (41, "الموردين", "إنشاء الموردين", "suppliers.create"),
                (42, "الموردين", "تعديل الموردين", "suppliers.edit"),
                (43, "الموردين", "حذف الموردين", "suppliers.delete"),
                (44, "إعدادات النظام", "عرض إعدادات النظام", "systemsettings.view"),
                (45, "إعدادات النظام", "إنشاء إعدادات النظام", "systemsettings.create"),
                (46, "إعدادات النظام", "تعديل إعدادات النظام", "systemsettings.edit"),
                (47, "إعدادات النظام", "حذف إعدادات النظام", "systemsettings.delete"),
                (48, "الأصول", "عرض الأصول", "assets.view"),
                (49, "الأصول", "إنشاء الأصول", "assets.create"),
                (50, "الأصول", "تعديل الأصول", "assets.edit"),
                (51, "الأصول", "حذف الأصول", "assets.delete"),
                (52, "الأصول", "عرض مصاريف الأصول", "assetexpenses.view"),
                (53, "الأصول", "إنشاء مصروف أصل", "assetexpenses.create"),
                (54, "التقارير", "عرض الحركات غير المرحلة", "reports.pending"),
                (55, "التقارير", "التقارير التفاعلية", "reports.dynamic"),
                (56, "الصلاحيات", "عرض مجموعات الصلاحيات", "permissiongroups.view"),
                (57, "الصلاحيات", "إنشاء مجموعة صلاحيات", "permissiongroups.create"),
                (58, "الصلاحيات", "تعديل مجموعة صلاحيات", "permissiongroups.edit"),
                (59, "الصلاحيات", "حذف مجموعة صلاحيات", "permissiongroups.delete"),
                (60, "السندات", "اعتماد سندات الدفع", "paymentvouchers.approve"),
                (61, "سير العمل", "عرض موافقات سندات الدفع", "workflowapprovals.view"),
                (62, "سير العمل", "معالجة موافقات سندات الدفع", "workflowapprovals.process"),
                (63, "سير العمل", "إدارة سير عمل السندات", "workflowdefinitions.manage"),
                (64, "سير العمل", "عرض الإشعارات", "notifications.view"),
                (65, "الوكلاء", "عرض الوكلاء", "agents.view"),
                (66, "الوكلاء", "إنشاء وكيل", "agents.create"),
                (67, "الوكلاء", "تعديل وكيل", "agents.edit"),
                (68, "الوكلاء", "حذف وكيل", "agents.delete"),
                (69, "المستخدمين", "عرض أرصدة حسابات المستخدم", "userbalances.view"),
                (70, "التقارير", "اختصار كشف الحساب المباشر", "reports.quickaccountstatement"),
                (71, "الشاشات الديناميكية", "إدارة الشاشات الديناميكية", "dynamicscreens.manage"),
                (72, "التقارير", "عرض كشف الحساب من السجلات", "reports.accountstatement"),
                (73, "إدارة الحسابات", "كشف حساب العميل الكلي", "accountmanagement.businessstatementbulk"),
                (74, "إدارة الحسابات", "كشف حساب العميل", "accountmanagement.busnissstatment"),
                (75, "إدارة الحسابات", "دفعات السائق", "accountmanagement.driverpayment"),
                (76, "إدارة الحسابات", "كشف حساب السائق", "accountmanagement.driverstatment"),
                (77, "إدارة الحسابات", "دفعات العميل", "accountmanagement.userpayment"),
                (78, "إدارة الحسابات", "شحنات العميل المرتجعة", "accountmanagement.busnissshipmentsreturn"),
                (79, "إدارة الحسابات", "استلام المدفوعات", "accountmanagement.receivepayments"),
                (80, "إدارة الحسابات", "استلام مدفوعات المرتجعات", "accountmanagement.receiveretpayments"),
                (81, "إدارة الحسابات", "كشف مرتجعات العميل الكلي", "accountmanagement.businessretstatementbulk"),
                (82, "إدارة الحسابات", "طباعة سند الحساب", "accountmanagement.printslip"),
                (83, "الرواتب", "عرض الرواتب", "payroll.view"),
                (84, "الرواتب", "معالجة الرواتب", "payroll.process"),
                (85, "الموظفين", "عرض الموظفين", "employees.view"),
                (86, "الموظفين", "إنشاء موظف", "employees.create"),
                (87, "الموظفين", "تعديل موظف", "employees.edit"),
                (88, "الموظفين", "حذف موظف", "employees.delete"),
                (89, "سلف الموظفين", "عرض سندات صرف السلف", "employeeadvances.view"),
                (90, "سلف الموظفين", "إنشاء سند صرف سلفة", "employeeadvances.create"),
                (91, "الرواتب", "عرض سندات صرف الرواتب", "salarypayments.view"),
                (92, "الرواتب", "إنشاء سند صرف راتب", "salarypayments.create"),
                (93, "السندات", "عرض سندات الدفع", "disbursementvouchers.view"),
                (94, "السندات", "إنشاء سند دفع", "disbursementvouchers.create"),
                (95, "السندات", "حذف سند دفع", "disbursementvouchers.delete"),
                (96, "السندات", "عرض سندات القبض", "receiptvouchers.view"),
                (97, "السندات", "إنشاء سند قبض", "receiptvouchers.create"),
                (98, "السندات", "حذف سند قبض", "receiptvouchers.delete"),
                (99, "السندات", "عرض سندات الدفع", "paymentvouchers.view"),
                (100, "السندات", "إنشاء سند دفع", "paymentvouchers.create"),
                (101, "السندات", "حذف سند دفع", "paymentvouchers.delete"),
                (102, "إغلاق الصندوق", "عرض إغلاقات الصندوق", "cashclosures.view"),
                (103, "إغلاق الصندوق", "إنشاء إغلاق صندوق", "cashclosures.create"),
                (104, "إغلاق الصندوق", "اعتماد إغلاقات الصندوق", "cashclosures.approve"),
                (105, "إغلاق الصندوق", "تقرير إغلاقات الصندوق", "cashclosures.report"),
                (106, "الأصول", "عرض أنواع الأصول", "assettypes.view"),
                (107, "الأصول", "إنشاء نوع أصل", "assettypes.create"),
                (108, "الأصول", "تعديل نوع أصل", "assettypes.edit"),
                (109, "الأصول", "حذف نوع أصل", "assettypes.delete"),
            };

            foreach (var permission in permissions)
            {
                var escapedCategory = permission.Category.Replace("'", "''");
                var escapedDisplayName = permission.DisplayName.Replace("'", "''");
                var escapedName = permission.Name.Replace("'", "''");
                var createdAtSql = createdAt.ToString("yyyy-MM-ddTHH:mm:ss");

                migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = N'{escapedName}') AND
   NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Id] = {permission.Id})
BEGIN
    INSERT INTO [Permissions] ([Id], [Category], [CreatedAt], [DisplayName], [IsActive], [Name])
    VALUES ({permission.Id}, N'{escapedCategory}', '{createdAtSql}', N'{escapedDisplayName}', 1, N'{escapedName}');
END");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            for (var id = 73; id <= 109; id++)
            {
                migrationBuilder.DeleteData(
                    table: "Permissions",
                    keyColumn: "Id",
                    keyValue: id);
            }
        }
    }
}
