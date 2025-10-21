using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Models;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace AccountingSystem.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Apply migrations to ensure the schema is up to date
            await context.Database.MigrateAsync();

            // Seed roles
            await SeedRolesAsync(roleManager);

            // Seed permissions
            await SeedPermissionsAsync(context);

            // Seed default admin user
            var adminUser = await SeedAdminUserAsync(userManager);

            // Seed default branch
            await SeedDefaultBranchAsync(context);

            // Seed chart of accounts
            await SeedChartOfAccountsAsync(context);

            // Seed system settings
            await SeedSystemSettingsAsync(context);

            // Grant all permissions to admin
            if (adminUser != null)
            {
                await SeedAdminPermissionsAsync(context, adminUser);
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            string[] roles = { "Admin", "Manager", "Accountant", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task SeedPermissionsAsync(ApplicationDbContext context)
        {
            var permissions = new List<Permission>
            {
                new Permission { Name = "users.view", DisplayName = "عرض المستخدمين", Category = "المستخدمين" },
                new Permission { Name = "users.create", DisplayName = "إنشاء المستخدمين", Category = "المستخدمين" },
                new Permission { Name = "users.edit", DisplayName = "تعديل المستخدمين", Category = "المستخدمين" },
                new Permission { Name = "users.delete", DisplayName = "حذف المستخدمين", Category = "المستخدمين" },
                new Permission { Name = "userbalances.view", DisplayName = "عرض أرصدة حسابات المستخدم", Category = "المستخدمين" },
                new Permission { Name = "branches.view", DisplayName = "عرض الفروع", Category = "الفروع" },
                new Permission { Name = "branches.create", DisplayName = "إنشاء الفروع", Category = "الفروع" },
                new Permission { Name = "branches.edit", DisplayName = "تعديل الفروع", Category = "الفروع" },
                new Permission { Name = "branches.delete", DisplayName = "حذف الفروع", Category = "الفروع" },
                new Permission { Name = "costcenters.view", DisplayName = "عرض مراكز التكلفة", Category = "مراكز التكلفة" },
                new Permission { Name = "costcenters.create", DisplayName = "إنشاء مراكز التكلفة", Category = "مراكز التكلفة" },
                new Permission { Name = "costcenters.edit", DisplayName = "تعديل مراكز التكلفة", Category = "مراكز التكلفة" },
                new Permission { Name = "costcenters.delete", DisplayName = "حذف مراكز التكلفة", Category = "مراكز التكلفة" },
                new Permission { Name = "currencies.view", DisplayName = "عرض العملات", Category = "العملات" },
                new Permission { Name = "currencies.create", DisplayName = "إنشاء العملات", Category = "العملات" },
                new Permission { Name = "currencies.edit", DisplayName = "تعديل العملات", Category = "العملات" },
                new Permission { Name = "currencies.delete", DisplayName = "حذف العملات", Category = "العملات" },
                new Permission { Name = "accounts.view", DisplayName = "عرض الحسابات", Category = "الحسابات" },
                new Permission { Name = "accounts.create", DisplayName = "إنشاء الحسابات", Category = "الحسابات" },
                new Permission { Name = "accounts.edit", DisplayName = "تعديل الحسابات", Category = "الحسابات" },
                new Permission { Name = "accounts.delete", DisplayName = "حذف الحسابات", Category = "الحسابات" },
                new Permission { Name = "expenses.view", DisplayName = "عرض المصاريف", Category = "المصاريف" },
                new Permission { Name = "expenses.create", DisplayName = "إنشاء المصاريف", Category = "المصاريف" },
                new Permission { Name = "expenses.edit", DisplayName = "تعديل المصاريف", Category = "المصاريف" },
                new Permission { Name = "expenses.delete", DisplayName = "حذف المصاريف", Category = "المصاريف" },
                new Permission { Name = "expenses.approve", DisplayName = "اعتماد المصاريف", Category = "المصاريف" },
                new Permission { Name = "journal.view", DisplayName = "عرض القيود", Category = "القيود المالية" },
                new Permission { Name = "journal.create", DisplayName = "إنشاء القيود", Category = "القيود المالية" },
                new Permission { Name = "journal.edit", DisplayName = "تعديل القيود", Category = "القيود المالية" },
                new Permission { Name = "journal.delete", DisplayName = "حذف القيود", Category = "القيود المالية" },
                new Permission { Name = "journal.approve", DisplayName = "اعتماد القيود", Category = "القيود المالية" },
                new Permission { Name = "reports.view", DisplayName = "عرض التقارير", Category = "التقارير" },
                new Permission { Name = "reports.export", DisplayName = "تصدير التقارير", Category = "التقارير" },
                new Permission { Name = "reports.pending", DisplayName = "عرض الحركات غير المرحلة", Category = "التقارير" },
                new Permission { Name = "reports.dynamic", DisplayName = "التقارير التفاعلية", Category = "التقارير" },
                new Permission { Name = "reports.quickaccountstatement", DisplayName = "اختصار كشف الحساب المباشر", Category = "التقارير" },
                new Permission { Name = "reports.accountstatement", DisplayName = "عرض كشف الحساب من السجلات", Category = "التقارير" },
                new Permission { Name = "dashboard.view", DisplayName = "عرض لوحة التحكم", Category = "لوحة التحكم" },
                new Permission { Name = "dashboard.widget.stats", DisplayName = "عرض لوحة التحكم stats", Category = "لوحة التحكم" },
                new Permission { Name = "dashboard.widget.accounts", DisplayName = " accountsعرض لوحة التحكم", Category = "لوحة التحكم" },
                new Permission { Name = "dashboard.widget.links", DisplayName = " linksعرض لوحة التحكم", Category = "لوحة التحكم" },
                new Permission { Name = "dashboard.widget.search", DisplayName = "عرض البحث السريع بلوحة التحكم", Category = "لوحة التحكم" },
                new Permission { Name = "dashboard.widget.cashboxes", DisplayName = "عرض أرصدة الصناديق بلوحة التحكم", Category = "لوحة التحكم" },
                new Permission { Name = "transfers.view", DisplayName = "عرض الحوالات", Category = "الحوالات" },
                new Permission { Name = "transfers.create", DisplayName = "إنشاء الحوالات", Category = "الحوالات" },
                new Permission { Name = "transfers.approve", DisplayName = "اعتماد الحوالات", Category = "الحوالات" },
                new Permission { Name = "transfers.manage", DisplayName = "إدارة الحوالات", Category = "الحوالات" },
                new Permission { Name = "cashclosures.view", DisplayName = "عرض إغلاقات الصندوق", Category = "الصندوق" },
                new Permission { Name = "cashclosures.create", DisplayName = "إنشاء إغلاق صندوق", Category = "الصندوق" },
                new Permission { Name = "cashclosures.approve", DisplayName = "اعتماد إغلاق الصندوق", Category = "الصندوق" },
                new Permission { Name = "cashclosures.report", DisplayName = "تقرير إغلاقات الصندوق", Category = "الصندوق" }
                ,
                new Permission { Name = "receiptvouchers.view", DisplayName = " سندات القبض", Category = "السندات" },
                new Permission { Name = "receiptvouchers.create", DisplayName = "إنشاء سند قبض", Category = "السندات" },
                new Permission { Name = "receiptvouchers.delete", DisplayName = "حذف سند قبض", Category = "السندات" },
                new Permission { Name = "disbursementvouchers.view", DisplayName = " سندات الصرف", Category = "السندات" },
                new Permission { Name = "disbursementvouchers.create", DisplayName = "إنشاء سند صرف", Category = "السندات" },
                new Permission { Name = "disbursementvouchers.delete", DisplayName = "حذف سند صرف", Category = "السندات" },
                new Permission { Name = "paymentvouchers.view", DisplayName = "سندات  الدفع", Category = "السندات" },
                new Permission { Name = "paymentvouchers.create", DisplayName = "إنشاء سند دفع", Category = "السندات" },
                new Permission { Name = "paymentvouchers.delete", DisplayName = "حذف سند دفع", Category = "السندات" },
                new Permission { Name = "paymentvouchers.approve", DisplayName = "اعتماد سندات الدفع", Category = "السندات" },
                new Permission { Name = "workflowapprovals.view", DisplayName = "عرض موافقات سندات الدفع", Category = "سير العمل" },
                new Permission { Name = "workflowapprovals.process", DisplayName = "معالجة موافقات سندات الدفع", Category = "سير العمل" },
                new Permission { Name = "workflowdefinitions.manage", DisplayName = "إدارة سير عمل السندات", Category = "سير العمل" },
                new Permission { Name = "notifications.view", DisplayName = "عرض الإشعارات", Category = "سير العمل" },
                new Permission { Name = "suppliers.view", DisplayName = "عرض الموردين", Category = "الموردين" },
                new Permission { Name = "suppliers.create", DisplayName = "إنشاء الموردين", Category = "الموردين" },
                new Permission { Name = "suppliers.edit", DisplayName = "تعديل الموردين", Category = "الموردين" },
                new Permission { Name = "suppliers.delete", DisplayName = "حذف الموردين", Category = "الموردين" },
                new Permission { Name = "agents.view", DisplayName = "عرض الوكلاء", Category = "الوكلاء" },
                new Permission { Name = "agents.create", DisplayName = "إنشاء وكيل", Category = "الوكلاء" },
                new Permission { Name = "agents.edit", DisplayName = "تعديل وكيل", Category = "الوكلاء" },
                new Permission { Name = "agents.delete", DisplayName = "حذف وكيل", Category = "الوكلاء" },
                new Permission { Name = "assets.view", DisplayName = "عرض الأصول", Category = "الأصول" },
                new Permission { Name = "assets.create", DisplayName = "إنشاء الأصول", Category = "الأصول" },
                new Permission { Name = "assets.edit", DisplayName = "تعديل الأصول", Category = "الأصول" },
                new Permission { Name = "assets.delete", DisplayName = "حذف الأصول", Category = "الأصول" },
                new Permission { Name = "assettypes.view", DisplayName = "عرض أنواع الأصول", Category = "الأصول" },
                new Permission { Name = "assettypes.create", DisplayName = "إنشاء نوع أصل", Category = "الأصول" },
                new Permission { Name = "assettypes.edit", DisplayName = "تعديل نوع أصل", Category = "الأصول" },
                new Permission { Name = "assettypes.delete", DisplayName = "حذف نوع أصل", Category = "الأصول" },
                new Permission { Name = "assetexpenses.view", DisplayName = "عرض مصاريف الأصول", Category = "الأصول" },
                new Permission { Name = "assetexpenses.create", DisplayName = "إنشاء مصروف أصل", Category = "الأصول" },
                new Permission { Name = "systemsettings.view", DisplayName = "عرض إعدادات النظام", Category = "إعدادات النظام" },
                new Permission { Name = "systemsettings.create", DisplayName = "إنشاء إعدادات النظام", Category = "إعدادات النظام" },
                new Permission { Name = "systemsettings.edit", DisplayName = "تعديل إعدادات النظام", Category = "إعدادات النظام" },
                new Permission { Name = "systemsettings.delete", DisplayName = "حذف إعدادات النظام", Category = "إعدادات النظام" },
                new Permission { Name = "employees.view", DisplayName = "عرض الموظفين", Category = "شؤون الموظفين" },
                new Permission { Name = "employees.create", DisplayName = "إضافة موظف", Category = "شؤون الموظفين" },
                new Permission { Name = "employees.edit", DisplayName = "تعديل موظف", Category = "شؤون الموظفين" },
                new Permission { Name = "employees.delete", DisplayName = "تغيير حالة موظف", Category = "شؤون الموظفين" },
                new Permission { Name = "payroll.view", DisplayName = "عرض إدارة الرواتب", Category = "شؤون الموظفين" },
                new Permission { Name = "payroll.process", DisplayName = "تنزيل الرواتب", Category = "شؤون الموظفين" },
                new Permission { Name = "salarypayments.view", DisplayName = "عرض سندات صرف الرواتب", Category = "شؤون الموظفين" },
                new Permission { Name = "salarypayments.create", DisplayName = "إنشاء سند صرف راتب", Category = "شؤون الموظفين" },
                new Permission { Name = "employeeadvances.view", DisplayName = "عرض سندات صرف السلف", Category = "شؤون الموظفين" },
                new Permission { Name = "employeeadvances.create", DisplayName = "إنشاء سند صرف سلفة", Category = "شؤون الموظفين" },
                new Permission { Name = "dynamicscreens.manage", DisplayName = "إدارة الشاشات الديناميكية", Category = "الشاشات الديناميكية" }
            };


            foreach (var perm in permissions)
            {
                if (!await context.Permissions.AnyAsync(p => p.Name == perm.Name))
                {
                    context.Permissions.Add(perm);
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task<User?> SeedAdminUserAsync(UserManager<User> userManager)
        {
            var adminEmail = "admin@accounting.com";

            var existing = await userManager.FindByEmailAsync(adminEmail);
            if (existing == null)
            {
                var adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "مدير",
                    LastName = "النظام",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    return adminUser;
                }
            }
            else
            {
                return existing;
            }

            return null;
        }

        private static async Task SeedDefaultBranchAsync(ApplicationDbContext context)
        {
            if (!context.Branches.Any())
            {
                var defaultBranch = new Branch
                {
                    Code = "001",
                    NameAr = "الفرع الرئيسي",
                    NameEn = "Main Branch",
                    Description = "الفرع الرئيسي للشركة",
                    IsActive = true
                };

                context.Branches.Add(defaultBranch);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedChartOfAccountsAsync(ApplicationDbContext context)
        {
            if (!context.Accounts.Any())
            {
                var accounts = new List<Account>
                {
                    // الأصول - Assets (Level 1)
                    new Account { Code = "1", NameAr = "الأصول", NameEn = "Assets", Level = 1, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // الأصول المتداولة - Current Assets (Level 2)
                    new Account { Code = "11", NameAr = "الأصول المتداولة", NameEn = "Current Assets", Level = 2, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // الصندوق - Cash Fund (Level 3)
                    new Account { Code = "1101", NameAr = "الصندوق", NameEn = "Cash Fund", Level = 3, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = true, OpeningBalance = 10000 },
                    
                    // البنوك - Banks (Level 3)
                    new Account { Code = "1102", NameAr = "البنوك", NameEn = "Banks", Level = 3, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = true, OpeningBalance = 50000 },
                    
                    // الذمم المدينة - Account Receivables (Level 3)
                    new Account { Code = "1103", NameAr = "الذمم المدينة", NameEn = "Account Receivables", Level = 3, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = true, OpeningBalance = 25000 },
                    
                    // المدفوع مقدماً - Advance Payments (Level 3)
                    new Account { Code = "1104", NameAr = "المدفوع مقدماً", NameEn = "Advance Payments", Level = 3, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = true, OpeningBalance = 5000 },
                    
                    // الأصول الثابتة - Fixed Assets (Level 2)
                    new Account { Code = "12", NameAr = "الأصول الثابتة", NameEn = "Fixed Assets", Level = 2, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // المشاريع - Projects (Level 3)
                    new Account { Code = "1207", NameAr = "المشاريع", NameEn = "Projects", Level = 3, AccountType = AccountType.Assets, Nature = AccountNature.Debit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Assets, CanPostTransactions = true, OpeningBalance = 100000 },
                    
                    // الالتزامات - Liabilities (Level 1)
                    new Account { Code = "2", NameAr = "الالتزامات", NameEn = "Liabilities", Level = 1, AccountType = AccountType.Liabilities, Nature = AccountNature.Credit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Liabilities, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // الالتزامات المتداولة - Current Liabilities (Level 2)
                    new Account { Code = "21", NameAr = "الالتزامات المتداولة", NameEn = "Current Liabilities", Level = 2, AccountType = AccountType.Liabilities, Nature = AccountNature.Credit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Liabilities, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // الذمم الدائنة - Account Payables (Level 3)
                    new Account { Code = "2101", NameAr = "الذمم الدائنة", NameEn = "Account Payables", Level = 3, AccountType = AccountType.Liabilities, Nature = AccountNature.Credit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Liabilities, CanPostTransactions = true, OpeningBalance = 15000 },
                    
                    // المستحق الدفع - Accrued Payables (Level 3)
                    new Account { Code = "2102", NameAr = "المستحق الدفع", NameEn = "Accrued Payables", Level = 3, AccountType = AccountType.Liabilities, Nature = AccountNature.Credit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.Liabilities, CanPostTransactions = true, OpeningBalance = 8000 },
                    
                    // حقوق الملكية - Equity (Level 1)
                    new Account { Code = "3", NameAr = "حقوق الملكية", NameEn = "Equity", Level = 1, AccountType = AccountType.Equity, Nature = AccountNature.Credit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.OwnerEquity, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // رأس المال - Capital (Level 2)
                    new Account { Code = "31", NameAr = "رأس المال", NameEn = "Capital", Level = 2, AccountType = AccountType.Equity, Nature = AccountNature.Credit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.OwnerEquity, CanPostTransactions = true, OpeningBalance = 150000 },
                    
                    // الأرباح المحتجزة - Retained Earnings (Level 2)
                    new Account { Code = "32", NameAr = "الأرباح المحتجزة", NameEn = "Retained Earnings", Level = 2, AccountType = AccountType.Equity, Nature = AccountNature.Credit, Classification = AccountClassification.BalanceSheet, SubClassification = AccountSubClassification.OwnerEquity, CanPostTransactions = true, OpeningBalance = 17000 },
                    
                    // الإيرادات - Revenue (Level 1)
                    new Account { Code = "4", NameAr = "الإيرادات", NameEn = "Revenue", Level = 1, AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Revenue, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // إيرادات المبيعات - Sales Revenue (Level 2)
                    new Account { Code = "41", NameAr = "إيرادات المبيعات", NameEn = "Sales Revenue", Level = 2, AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Revenue, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // إيرادات الخدمات - Service Revenue (Level 2)
                    new Account { Code = "42", NameAr = "إيرادات الخدمات", NameEn = "Service Revenue", Level = 2, AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Revenue, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // إيرادات أخرى - Other Revenue (Level 2)
                    new Account { Code = "43", NameAr = "إيرادات أخرى", NameEn = "Other Revenue", Level = 2, AccountType = AccountType.Revenue, Nature = AccountNature.Credit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Revenue, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // المصاريف - Expenses (Level 1)
                    new Account { Code = "5", NameAr = "المصاريف", NameEn = "Expenses", Level = 1, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // مصاريف التشغيل - Operating Expenses (Level 2)
                    new Account { Code = "51", NameAr = "مصاريف التشغيل", NameEn = "Operating Expenses", Level = 2, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // الرواتب والأجور - Salaries and Wages (Level 3)
                    new Account { Code = "5101", NameAr = "الرواتب والأجور", NameEn = "Salaries and Wages", Level = 3, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // الإيجار - Rent Expense (Level 3)
                    new Account { Code = "5102", NameAr = "الإيجار", NameEn = "Rent Expense", Level = 3, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // الكهرباء والماء - Utilities (Level 3)
                    new Account { Code = "5103", NameAr = "الكهرباء والماء", NameEn = "Utilities", Level = 3, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // مصاريف الصيانة - Maintenance Expenses (Level 3)
                    new Account { Code = "5104", NameAr = "مصاريف الصيانة", NameEn = "Maintenance Expenses", Level = 3, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // مصاريف إدارية - Administrative Expenses (Level 2)
                    new Account { Code = "52", NameAr = "مصاريف إدارية", NameEn = "Administrative Expenses", Level = 2, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = false, OpeningBalance = 0 },
                    
                    // القرطاسية - Office Supplies (Level 3)
                    new Account { Code = "5201", NameAr = "القرطاسية", NameEn = "Office Supplies", Level = 3, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = true, OpeningBalance = 0 },
                    
                    // الاتصالات - Communications (Level 3)
                    new Account { Code = "5202", NameAr = "الاتصالات", NameEn = "Communications", Level = 3, AccountType = AccountType.Expenses, Nature = AccountNature.Debit, Classification = AccountClassification.IncomeStatement, SubClassification = AccountSubClassification.Expense, CanPostTransactions = true, OpeningBalance = 0 }
                };

                context.Accounts.AddRange(accounts);
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedSystemSettingsAsync(ApplicationDbContext context)
        {
            if (!context.SystemSettings.Any(s => s.Key == "SuppliersParentAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "SuppliersParentAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "SupplierPaymentsParentAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "SupplierPaymentsParentAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "AgentsParentAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "AgentsParentAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "AssetExpensesParentAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "AssetExpensesParentAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "CashBoxDifferenceAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "CashBoxDifferenceAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "AssetsParentAccountCode"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "AssetsParentAccountCode", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "AssetTypesParentAccountCode"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "AssetTypesParentAccountCode", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "PayrollExpenseAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "PayrollExpenseAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "CashBoxesParentAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "CashBoxesParentAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "DashboardParentAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "DashboardParentAccountId", Value = null });
            }

            if (!context.SystemSettings.Any(s => s.Key == "TransferIntermediaryAccountId"))
            {
                context.SystemSettings.Add(new SystemSetting { Key = "TransferIntermediaryAccountId", Value = null });
            }

            await context.SaveChangesAsync();
        }

        private static async Task SeedAdminPermissionsAsync(ApplicationDbContext context, User adminUser)
        {
            var allPermissions = await context.Permissions.Select(p => p.Id).ToListAsync();
            var existing = await context.UserPermissions
                .Where(up => up.UserId == adminUser.Id)
                .Select(up => up.PermissionId)
                .ToListAsync();

            var toAdd = allPermissions.Except(existing);
            foreach (var pid in toAdd)
            {
                context.UserPermissions.Add(new UserPermission
                {
                    UserId = adminUser.Id,
                    PermissionId = pid,
                    IsGranted = true
                });
            }

            await context.SaveChangesAsync();
        }
    }
}

