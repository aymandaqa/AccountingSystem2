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
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "DisplayName", "IsActive", "Name" },
                values: new object[,]
                {
                    { 73, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "كشف حساب العميل الكلي", true, "accountmanagement.businessstatementbulk" },
                    { 74, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "كشف حساب العميل", true, "accountmanagement.busnissstatment" },
                    { 75, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "دفعات السائق", true, "accountmanagement.driverpayment" },
                    { 76, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "كشف حساب السائق", true, "accountmanagement.driverstatment" },
                    { 77, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "دفعات العميل", true, "accountmanagement.userpayment" },
                    { 78, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "شحنات العميل المرتجعة", true, "accountmanagement.busnissshipmentsreturn" },
                    { 79, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "استلام المدفوعات", true, "accountmanagement.receivepayments" },
                    { 80, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "استلام مدفوعات المرتجعات", true, "accountmanagement.receiveretpayments" },
                    { 81, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "كشف مرتجعات العميل الكلي", true, "accountmanagement.businessretstatementbulk" },
                    { 82, "إدارة الحسابات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "طباعة سند الحساب", true, "accountmanagement.printslip" },
                    { 83, "الرواتب", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض الرواتب", true, "payroll.view" },
                    { 84, "الرواتب", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "معالجة الرواتب", true, "payroll.process" },
                    { 85, "الموظفين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض الموظفين", true, "employees.view" },
                    { 86, "الموظفين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء موظف", true, "employees.create" },
                    { 87, "الموظفين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "تعديل موظف", true, "employees.edit" },
                    { 88, "الموظفين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "حذف موظف", true, "employees.delete" },
                    { 89, "سلف الموظفين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض سندات صرف السلف", true, "employeeadvances.view" },
                    { 90, "سلف الموظفين", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء سند صرف سلفة", true, "employeeadvances.create" },
                    { 91, "الرواتب", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض سندات صرف الرواتب", true, "salarypayments.view" },
                    { 92, "الرواتب", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء سند صرف راتب", true, "salarypayments.create" },
                    { 93, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض سندات الدفع", true, "disbursementvouchers.view" },
                    { 94, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء سند دفع", true, "disbursementvouchers.create" },
                    { 95, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "حذف سند دفع", true, "disbursementvouchers.delete" },
                    { 96, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض سندات القبض", true, "receiptvouchers.view" },
                    { 97, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء سند قبض", true, "receiptvouchers.create" },
                    { 98, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "حذف سند قبض", true, "receiptvouchers.delete" },
                    { 99, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض سندات الدفع", true, "paymentvouchers.view" },
                    { 100, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء سند دفع", true, "paymentvouchers.create" },
                    { 101, "السندات", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "حذف سند دفع", true, "paymentvouchers.delete" },
                    { 102, "إغلاق الصندوق", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض إغلاقات الصندوق", true, "cashclosures.view" },
                    { 103, "إغلاق الصندوق", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء إغلاق صندوق", true, "cashclosures.create" },
                    { 104, "إغلاق الصندوق", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "اعتماد إغلاقات الصندوق", true, "cashclosures.approve" },
                    { 105, "إغلاق الصندوق", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "تقرير إغلاقات الصندوق", true, "cashclosures.report" },
                    { 106, "الأصول", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "عرض أنواع الأصول", true, "assettypes.view" },
                    { 107, "الأصول", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "إنشاء نوع أصل", true, "assettypes.create" },
                    { 108, "الأصول", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "تعديل نوع أصل", true, "assettypes.edit" },
                    { 109, "الأصول", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "حذف نوع أصل", true, "assettypes.delete" }
                });
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
