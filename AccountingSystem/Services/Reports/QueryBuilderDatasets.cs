using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services;

public static class QueryBuilderDatasets
{
    public static IReadOnlyList<QueryDatasetDefinition> All { get; } = new List<QueryDatasetDefinition>
    {
        new(
            "journalEntries",
            "قيود اليومية",
            "تحليل قيود اليومية مع تفاصيل الحسابات والفروع ومراكز التكلفة.",
            new List<QueryDatasetField>
            {
                new("JournalEntryId", "معرّف القيد", QueryFieldType.Number, "القيد"),
                new("EntryNumber", "رقم القيد", QueryFieldType.String, "القيد"),
                new("EntryDate", "تاريخ القيد", QueryFieldType.Date, "القيد"),
                new("EntryYear", "سنة القيد", QueryFieldType.Number, "القيد"),
                new("EntryMonth", "شهر القيد", QueryFieldType.Number, "القيد"),
                new("EntryStatus", "حالة القيد", QueryFieldType.String, "القيد"),
                new("BranchCode", "كود الفرع", QueryFieldType.String, "الفرع"),
                new("BranchName", "اسم الفرع", QueryFieldType.String, "الفرع"),
                new("AccountCode", "كود الحساب", QueryFieldType.String, "الحساب"),
                new("AccountName", "اسم الحساب", QueryFieldType.String, "الحساب"),
                new("AccountBranch", "فرع الحساب", QueryFieldType.String, "الحساب"),
                new("CostCenter", "مركز التكلفة", QueryFieldType.String, "مراكز التكلفة"),
                new("LineDescription", "وصف السطر", QueryFieldType.String, "القيد"),
                new("Reference", "المرجع", QueryFieldType.String, "القيد"),
                new("Debit", "مدين", QueryFieldType.Decimal, "القيم"),
                new("Credit", "دائن", QueryFieldType.Decimal, "القيم"),
            },
            context => context.JournalEntryLines
                .AsNoTracking()
                .Include(l => l.JournalEntry).ThenInclude(e => e.Branch)
                .Include(l => l.Account).ThenInclude(a => a.Branch)
                .Include(l => l.CostCenter)
                .Select(l => new JournalEntryQueryRow
                {
                    JournalEntryId = l.JournalEntryId,
                    EntryNumber = l.JournalEntry.Number,
                    EntryDate = l.JournalEntry.Date,
                    EntryYear = l.JournalEntry.Date.Year,
                    EntryMonth = l.JournalEntry.Date.Month,
                    EntryStatus = l.JournalEntry.Status.ToString(),
                    BranchCode = l.JournalEntry.Branch != null ? l.JournalEntry.Branch.Code : null,
                    BranchName = l.JournalEntry.Branch != null ? l.JournalEntry.Branch.NameAr : null,
                    AccountCode = l.Account.Code,
                    AccountName = l.Account.NameAr,
                    AccountBranch = l.Account.Branch != null ? l.Account.Branch.NameAr : null,
                    CostCenter = l.CostCenter != null ? l.CostCenter.NameAr : null,
                    LineDescription = l.Description,
                    Reference = l.Reference,
                    Debit = l.DebitAmount,
                    Credit = l.CreditAmount
                })
        ),
        new(
            "receiptVouchers",
            "سندات القبض",
            "عرض سندات القبض مع تفاصيل الحسابات والعملات والمستخدمين.",
            new List<QueryDatasetField>
            {
                new("Id", "المعرف", QueryFieldType.Number, "السند"),
                new("Date", "تاريخ السند", QueryFieldType.Date, "السند"),
                new("Year", "السنة", QueryFieldType.Number, "السند"),
                new("Month", "الشهر", QueryFieldType.Number, "السند"),
                new("Supplier", "المورد", QueryFieldType.String, "المورد"),
                new("SupplierAccountCode", "كود حساب المورد", QueryFieldType.String, "الحساب"),
                new("SupplierAccountName", "اسم حساب المورد", QueryFieldType.String, "الحساب"),
                new("SupplierBranchCode", "كود فرع المورد", QueryFieldType.String, "الفرع"),
                new("SupplierBranchName", "اسم فرع المورد", QueryFieldType.String, "الفرع"),
                new("PaymentAccountCode", "كود حساب الدفع", QueryFieldType.String, "الحساب"),
                new("PaymentAccountName", "اسم حساب الدفع", QueryFieldType.String, "الحساب"),
                new("PaymentBranchCode", "كود فرع حساب الدفع", QueryFieldType.String, "الفرع"),
                new("PaymentBranchName", "اسم فرع حساب الدفع", QueryFieldType.String, "الفرع"),
                new("Currency", "العملة", QueryFieldType.String, "العملة"),
                new("Amount", "المبلغ", QueryFieldType.Decimal, "القيم"),
                new("ExchangeRate", "سعر الصرف", QueryFieldType.Decimal, "القيم"),
                new("AmountBase", "المبلغ بالأساس", QueryFieldType.Decimal, "القيم"),
                new("CreatedBy", "تم الإنشاء بواسطة", QueryFieldType.String, "السند"),
                new("Notes", "ملاحظات", QueryFieldType.String, "السند"),
            },
            context => context.ReceiptVouchers
                .AsNoTracking()
                .Include(r => r.Account).ThenInclude(a => a.Branch)
                .Include(r => r.PaymentAccount).ThenInclude(a => a.Branch)
                .Include(r => r.Currency)
                .Include(r => r.CreatedBy)
                .Include(r => r.Supplier)
                .Select(r => new VoucherQueryRow
                {
                    Id = r.Id,
                    Date = r.Date,
                    Year = r.Date.Year,
                    Month = r.Date.Month,
                    Supplier = r.Supplier != null ? r.Supplier.NameAr : null,
                    SupplierAccountCode = r.Account.Code,
                    SupplierAccountName = r.Account.NameAr,
                    SupplierBranchCode = r.Account.Branch != null ? r.Account.Branch.Code : null,
                    SupplierBranchName = r.Account.Branch != null ? r.Account.Branch.NameAr : null,
                    PaymentAccountCode = r.PaymentAccount.Code,
                    PaymentAccountName = r.PaymentAccount.NameAr,
                    PaymentBranchCode = r.PaymentAccount.Branch != null ? r.PaymentAccount.Branch.Code : null,
                    PaymentBranchName = r.PaymentAccount.Branch != null ? r.PaymentAccount.Branch.NameAr : null,
                    AccountCode = r.PaymentAccount.Code,
                    AccountName = r.PaymentAccount.NameAr,
                    BranchCode = r.PaymentAccount.Branch != null ? r.PaymentAccount.Branch.Code : null,
                    BranchName = r.PaymentAccount.Branch != null ? r.PaymentAccount.Branch.NameAr : null,
                    Currency = r.Currency.Code,
                    Amount = r.Amount,
                    ExchangeRate = r.ExchangeRate,
                    AmountBase = r.Amount * r.ExchangeRate,
                    CreatedBy = r.CreatedBy != null ? r.CreatedBy.UserName : null,
                    Notes = r.Notes
                })
        ),
        new(
            "paymentVouchers",
            "سندات الدفع",
            "متابعة سندات الدفع مع بيانات المورد والحساب والعملة.",
            new List<QueryDatasetField>
            {
                new("Id", "المعرف", QueryFieldType.Number, "السند"),
                new("Date", "تاريخ السند", QueryFieldType.Date, "السند"),
                new("Year", "السنة", QueryFieldType.Number, "السند"),
                new("Month", "الشهر", QueryFieldType.Number, "السند"),
                new("Supplier", "المورد", QueryFieldType.String, "المورد"),
                new("AccountCode", "كود الحساب", QueryFieldType.String, "الحساب"),
                new("AccountName", "اسم الحساب", QueryFieldType.String, "الحساب"),
                new("BranchCode", "كود الفرع", QueryFieldType.String, "الفرع"),
                new("BranchName", "اسم الفرع", QueryFieldType.String, "الفرع"),
                new("Currency", "العملة", QueryFieldType.String, "العملة"),
                new("Amount", "المبلغ", QueryFieldType.Decimal, "القيم"),
                new("ExchangeRate", "سعر الصرف", QueryFieldType.Decimal, "القيم"),
                new("AmountBase", "المبلغ بالأساس", QueryFieldType.Decimal, "القيم"),
                new("CreatedBy", "تم الإنشاء بواسطة", QueryFieldType.String, "السند"),
                new("IsCash", "نقدي؟", QueryFieldType.Boolean, "السند"),
                new("Notes", "ملاحظات", QueryFieldType.String, "السند"),
            },
            context => context.PaymentVouchers
                .AsNoTracking()
                .Include(v => v.Supplier)
                .Include(v => v.Account).ThenInclude(a => a!.Branch)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                .Select(v => new PaymentVoucherQueryRow
                {
                    Id = v.Id,
                    Date = v.Date,
                    Year = v.Date.Year,
                    Month = v.Date.Month,
                    Supplier = v.Supplier != null ? v.Supplier.NameAr : null,
                    AccountCode = v.Account != null ? v.Account.Code : null,
                    AccountName = v.Account != null ? v.Account.NameAr : null,
                    BranchCode = v.Account != null && v.Account.Branch != null ? v.Account.Branch.Code : null,
                    BranchName = v.Account != null && v.Account.Branch != null ? v.Account.Branch.NameAr : null,
                    SupplierAccountCode = v.Account != null ? v.Account.Code ?? string.Empty : string.Empty,
                    SupplierAccountName = v.Account != null ? v.Account.NameAr ?? string.Empty : string.Empty,
                    SupplierBranchCode = v.Account != null && v.Account.Branch != null ? v.Account.Branch.Code : null,
                    SupplierBranchName = v.Account != null && v.Account.Branch != null ? v.Account.Branch.NameAr : null,
                    PaymentAccountCode = v.Account != null ? v.Account.Code ?? string.Empty : string.Empty,
                    PaymentAccountName = v.Account != null ? v.Account.NameAr ?? string.Empty : string.Empty,
                    PaymentBranchCode = v.Account != null && v.Account.Branch != null ? v.Account.Branch.Code : null,
                    PaymentBranchName = v.Account != null && v.Account.Branch != null ? v.Account.Branch.NameAr : null,
                    Currency = v.Currency.Code,
                    Amount = v.Amount,
                    ExchangeRate = v.ExchangeRate,
                    AmountBase = v.Amount * v.ExchangeRate,
                    CreatedBy = v.CreatedBy != null ? v.CreatedBy.UserName : null,
                    IsCash = v.IsCash,
                    Notes = v.Notes
                })
        )
    };

    public static QueryDatasetDefinition? GetByKey(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
}

public record QueryDatasetDefinition(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<QueryDatasetField> Fields,
    Func<ApplicationDbContext, IQueryable> QueryFactory);

public record QueryDatasetField(
    string Field,
    string Label,
    QueryFieldType FieldType,
    string Category);

public enum QueryFieldType
{
    String,
    Number,
    Decimal,
    Date,
    Boolean
}

public class JournalEntryQueryRow
{
    public int JournalEntryId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public int EntryYear { get; set; }
    public int EntryMonth { get; set; }
    public string EntryStatus { get; set; } = string.Empty;
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? AccountBranch { get; set; }
    public string? CostCenter { get; set; }
    public string? LineDescription { get; set; }
    public string? Reference { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class VoucherQueryRow
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string? Supplier { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string SupplierAccountCode { get; set; } = string.Empty;
    public string SupplierAccountName { get; set; } = string.Empty;
    public string? SupplierBranchCode { get; set; }
    public string? SupplierBranchName { get; set; }
    public string PaymentAccountCode { get; set; } = string.Empty;
    public string PaymentAccountName { get; set; } = string.Empty;
    public string? PaymentBranchCode { get; set; }
    public string? PaymentBranchName { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal AmountBase { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
}

public class PaymentVoucherQueryRow : VoucherQueryRow
{
    public bool IsCash { get; set; }

    public new string? Supplier
    {
        get => base.Supplier;
        set => base.Supplier = value;
    }
}
