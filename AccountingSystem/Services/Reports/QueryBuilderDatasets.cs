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
                new("AccountCode", "كود الحساب", QueryFieldType.String, "الحساب"),
                new("AccountName", "اسم الحساب", QueryFieldType.String, "الحساب"),
                new("BranchCode", "كود الفرع", QueryFieldType.String, "الفرع"),
                new("BranchName", "اسم الفرع", QueryFieldType.String, "الفرع"),
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
                .Include(r => r.Currency)
                .Include(r => r.CreatedBy)
                .Select(r => new VoucherQueryRow
                {
                    Id = r.Id,
                    Date = r.Date,
                    Year = r.Date.Year,
                    Month = r.Date.Month,
                    AccountCode = r.Account.Code,
                    AccountName = r.Account.NameAr,
                    BranchCode = r.Account.Branch != null ? r.Account.Branch.Code : null,
                    BranchName = r.Account.Branch != null ? r.Account.Branch.NameAr : null,
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
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal AmountBase { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
}

public class PaymentVoucherQueryRow : VoucherQueryRow
{
    public string? Supplier { get; set; }
    public bool IsCash { get; set; }
}
