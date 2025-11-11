using Microsoft.AspNetCore.Mvc.Rendering;
using AccountingSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;

namespace AccountingSystem.ViewModels
{
    // Journal Entry ViewModels
    public class CreateJournalEntryViewModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string Description { get; set; } = "-";
        public string? Reference { get; set; } = "-";
        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار الفرع")]
        public int BranchId { get; set; }
        public int? CostCenterId { get; set; }
        public List<JournalEntryLineViewModel> Lines { get; set; } = new List<JournalEntryLineViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CostCenters { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Accounts { get; set; } = new List<SelectListItem>();
    }

    public class JournalEntryLineViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public int? CostCenterId { get; set; }
        public string? CostCenterName { get; set; }
        public decimal DebitAmount { get; set; } = 0;
        public decimal CreditAmount { get; set; } = 0;
        public string Description { get; set; } = "-";
    }

    public class JournalEntryDetailsViewModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string? CreatedByUserName { get; set; }
        public string? ApprovedByName { get; set; }
        public string? ApprovedByUserName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public List<JournalEntryLineViewModel> Lines { get; set; } = new List<JournalEntryLineViewModel>();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    // Account ViewModels
    public class CreateAccountViewModel
    {
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public AccountNature Nature { get; set; }
        public AccountClassification Classification { get; set; }
        public decimal OpeningBalance { get; set; }
        public bool IsActive { get; set; } = true;
        public bool CanPostTransactions { get; set; } = true;
        public int? ParentId { get; set; }
        public int? BranchId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار العملة")]
        public int CurrencyId { get; set; }
        public List<SelectListItem> ParentAccounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Currencies { get; set; } = new List<SelectListItem>();
    }

    public class EditAccountViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public AccountNature Nature { get; set; }
        public AccountClassification Classification { get; set; }
        public decimal OpeningBalance { get; set; }
        public bool IsActive { get; set; }
        public bool CanPostTransactions { get; set; }
        public int? ParentId { get; set; }
        public int? BranchId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار العملة")]
        public int CurrencyId { get; set; }
        public List<SelectListItem> ParentAccounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Currencies { get; set; } = new List<SelectListItem>();
    }

    public class AccountViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public AccountNature Nature { get; set; }
        public AccountClassification Classification { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsActive { get; set; }
        public bool CanPostTransactions { get; set; }
        public int? ParentId { get; set; }
        public string ParentAccountName { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int Level { get; set; }
        public bool HasChildren { get; set; }
        public bool HasTransactions { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
    }

    public class AccountDetailsViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public AccountNature Nature { get; set; }
        public AccountSubClassification SubClassification { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsActive { get; set; }
        public bool CanPostTransactions { get; set; }
        public bool RequiresCostCenter { get; set; }
        public int Level { get; set; }
        public int? ParentAccountId { get; set; }
        public string ParentAccountName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public List<AccountDetailsChildViewModel> ChildAccounts { get; set; } = new List<AccountDetailsChildViewModel>();
    }

    public class AccountDetailsChildViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public AccountSubClassification SubClassification { get; set; }
        public decimal CurrentBalance { get; set; }
        public bool IsActive { get; set; }
    }

    // Report ViewModels
    public class TrialBalanceViewModel
    {
        public DateTime FromDate { get; set; } = new DateTime(2025, 1, 1);
        public DateTime ToDate { get; set; } = DateTime.Today;
        public DateTime AsOfDate { get; set; } = DateTime.Now;
        public bool IncludePending { get; set; }
        public List<TrialBalanceItemViewModel> Items { get; set; } = new List<TrialBalanceItemViewModel>();
        public List<TrialBalanceAccountViewModel> Accounts { get; set; } = new List<TrialBalanceAccountViewModel>();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalDebitsBase { get; set; }
        public decimal TotalCreditsBase { get; set; }
        public int? SelectedCurrencyId { get; set; }
        public string SelectedCurrencyCode { get; set; } = string.Empty;
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public List<SelectListItem> Currencies { get; set; } = new List<SelectListItem>();
        public bool IsBalanced { get; set; }
        public int SelectedLevel { get; set; } = 5;
        public List<SelectListItem> Levels { get; set; } = new List<SelectListItem>();
    }

    public class TrialBalanceAccountViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = "";
        public string AccountName { get; set; } = "";
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public decimal DebitBalanceBase { get; set; }
        public decimal CreditBalanceBase { get; set; }
        public decimal PostingDebitBalance { get; set; }
        public decimal PostingCreditBalance { get; set; }
        public decimal PostingDebitBalanceBase { get; set; }
        public decimal PostingCreditBalanceBase { get; set; }
        public int Level { get; set; }
        public int? ParentAccountId { get; set; }
        public bool HasChildren { get; set; }
        public bool IsVisibleLeaf { get; set; }
    }

    public class TrialBalanceItemViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
    }

    public class BalanceSheetViewModel
    {
        public int? BranchId { get; set; }
        public bool IncludePending { get; set; }
        public List<AccountTreeNodeViewModel> Assets { get; set; } = new List<AccountTreeNodeViewModel>();
        public List<AccountTreeNodeViewModel> Liabilities { get; set; } = new List<AccountTreeNodeViewModel>();
        public List<AccountTreeNodeViewModel> Equity { get; set; } = new List<AccountTreeNodeViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public decimal TotalAssetsBase { get; set; }
        public decimal TotalLiabilitiesBase { get; set; }
        public decimal TotalEquityBase { get; set; }
        public int? SelectedCurrencyId { get; set; }
        public string SelectedCurrencyCode { get; set; } = string.Empty;
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public List<SelectListItem> Currencies { get; set; } = new List<SelectListItem>();
        public bool IsBalanced { get; set; }
        public int SelectedLevel { get; set; } = 6;
        public List<SelectListItem> Levels { get; set; } = new List<SelectListItem>();
        public DateTime FromDate { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
        public DateTime ToDate { get; set; } = DateTime.Today;
    }

    public class BalanceSheetItemViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class IncomeStatementViewModel
    {
        public DateTime FromDate { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
        public DateTime ToDate { get; set; } = DateTime.Now;
        public int? BranchId { get; set; }
        public bool IncludePending { get; set; }
        public List<AccountTreeNodeViewModel> Revenues { get; set; } = new List<AccountTreeNodeViewModel>();
        public List<AccountTreeNodeViewModel> Expenses { get; set; } = new List<AccountTreeNodeViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public decimal TotalRevenues { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public decimal NetIncomeDisplay { get; set; }
        public decimal TotalRevenuesBase { get; set; }
        public decimal TotalExpensesBase { get; set; }
        public decimal NetIncomeBase { get; set; }
        public decimal NetIncomeDisplayBase { get; set; }
        public int? SelectedCurrencyId { get; set; }
        public string SelectedCurrencyCode { get; set; } = string.Empty;
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public List<SelectListItem> Currencies { get; set; } = new List<SelectListItem>();
    }

    public class IncomeStatementItemViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class BranchExpensesReportViewModel
    {
        public DateTime FromDate { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
        public DateTime ToDate { get; set; } = DateTime.Today;
        public List<int> SelectedBranchIds { get; set; } = new List<int>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public BranchExpensesViewMode ViewMode { get; set; } = BranchExpensesViewMode.ByBranch;
        public BranchExpensesPeriodGrouping PeriodGrouping { get; set; } = BranchExpensesPeriodGrouping.Monthly;
        public List<BranchExpensesReportColumn> Columns { get; set; } = new List<BranchExpensesReportColumn>();
        public List<BranchExpensesReportRow> Rows { get; set; } = new List<BranchExpensesReportRow>();
        public Dictionary<DateTime, decimal> ColumnTotals { get; set; } = new Dictionary<DateTime, decimal>();
        public decimal GrandTotal { get; set; }
        public bool FiltersApplied { get; set; }
        public CultureInfo DisplayCulture { get; set; } = CultureInfo.CurrentCulture;
        public bool HasResults => Rows.Any();
    }

    public class BranchExpensesReportColumn
    {
        public DateTime PeriodStart { get; set; }
        public string Label { get; set; } = string.Empty;
        public DateTime PeriodEnd { get; set; }
    }

    public class BranchExpensesReportRow
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public Dictionary<DateTime, decimal> Amounts { get; set; } = new Dictionary<DateTime, decimal>();
        public decimal Total => Amounts.Values.Sum();
    }

    public enum BranchExpensesViewMode
    {
        ByBranch = 0,
        Combined = 1
    }

    public enum BranchExpensesPeriodGrouping
    {
        Monthly = 0,
        Quarterly = 1,
        Yearly = 2
    }

    public class BranchIncomeStatementReportViewModel
    {
        public BranchIncomeStatementRangeMode RangeMode { get; set; } = BranchIncomeStatementRangeMode.DateRange;
        public DateTime FromDate { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
        public DateTime ToDate { get; set; } = DateTime.Today;
        public int SelectedYear { get; set; } = DateTime.Today.Year;
        public int SelectedQuarter { get; set; } = (DateTime.Today.Month - 1) / 3 + 1;
        public List<SelectListItem> AvailableYears { get; set; } = new List<SelectListItem>();
        public List<BranchIncomeStatementRow> Rows { get; set; } = new List<BranchIncomeStatementRow>();
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public bool FiltersApplied { get; set; }
        public bool HasResults => Rows.Any();
    }

    public class BranchIncomeStatementRow
    {
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetIncome => Revenue - Expenses;
    }

    public enum BranchIncomeStatementRangeMode
    {
        DateRange = 0,
        Quarter = 1
    }

    public class BranchPerformanceSummaryViewModel
    {
        public DateTime FromDate { get; set; } = new DateTime(DateTime.Today.Year, 1, 1);
        public DateTime ToDate { get; set; } = DateTime.Today;
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public List<BranchPerformanceSummaryBranch> Branches { get; set; } = new();
        public List<BranchPerformanceSummarySection> Sections { get; set; } = new();
        public List<BranchPerformanceSummaryRow> SummaryRows { get; set; } = new();
        public bool FiltersApplied { get; set; }
        public bool HasResults => Sections.Any();
    }

    public class BranchPerformanceSummaryBranch
    {
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }

    public class BranchPerformanceSummarySection
    {
        public AccountType AccountType { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<BranchPerformanceSummaryRow> Rows { get; set; } = new();
        public Dictionary<int?, decimal> TotalsByBranch { get; set; } = new();
        public decimal OverallTotal => TotalsByBranch.Values.Sum();
    }

    public class BranchPerformanceSummaryRow
    {
        public int AccountId { get; set; }
        public int? ParentAccountId { get; set; }
        public int Level { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public Dictionary<int?, decimal> Values { get; set; } = new();
        public decimal Total => Values.Values.Sum();
    }

    public class AccountStatementViewModel
    {
        public int? AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? BranchId { get; set; }
        public string? SelectedBranchName { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal OpeningBalanceBase { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal ClosingBalanceBase { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalDebitBase { get; set; }
        public decimal TotalCreditBase { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public List<AccountTransactionViewModel> Transactions { get; set; } = new List<AccountTransactionViewModel>();
        public List<SelectListItem> Accounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
    }

    public class AccountTransactionViewModel
    {
        public DateTime Date { get; set; }
        public int JournalEntryId { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string MovementType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal RunningBalance { get; set; }
        public decimal DebitAmountBase { get; set; }
        public decimal CreditAmountBase { get; set; }
        public decimal RunningBalanceBase { get; set; }
    }

    public class GeneralLedgerViewModel
    {
        public int? AccountId { get; set; }
        public DateTime FromDate { get; set; } = DateTime.Now.AddMonths(-1);
        public DateTime ToDate { get; set; } = DateTime.Now;
        public int? BranchId { get; set; }
        public bool IncludePending { get; set; }
        public List<GeneralLedgerAccountViewModel> Accounts { get; set; } = new List<GeneralLedgerAccountViewModel>();
        public List<SelectListItem> AccountOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
    }

    public class GeneralLedgerAccountViewModel
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public List<GeneralLedgerTransactionViewModel> Transactions { get; set; } = new List<GeneralLedgerTransactionViewModel>();
    }

    public class GeneralLedgerTransactionViewModel
    {
        public DateTime Date { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
    }


    public class DashboardViewModel
    {
        public int TotalAccounts { get; set; }
        public int TotalBranches { get; set; }
        public int TotalUsers { get; set; }
        public int TotalJournalEntries { get; set; }
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public decimal TotalRevenues { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public decimal TotalAssetsBase { get; set; }
        public decimal TotalLiabilitiesBase { get; set; }
        public decimal TotalEquityBase { get; set; }
        public decimal TotalRevenuesBase { get; set; }
        public decimal TotalExpensesBase { get; set; }
        public decimal NetIncomeBase { get; set; }
        public int? SelectedBranchId { get; set; }
        public int? SelectedCurrencyId { get; set; }
        public string SelectedCurrencyCode { get; set; } = string.Empty;
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public DateTime FromDate { get; set; } = DateTime.Today;
        public DateTime ToDate { get; set; } = DateTime.Today;
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<AccountTreeNodeViewModel> AccountTypeTrees { get; set; } = new List<AccountTreeNodeViewModel>();
        public List<AccountTreeNodeViewModel> CashBoxTree { get; set; } = new List<AccountTreeNodeViewModel>();
        public bool CashBoxParentAccountConfigured { get; set; }
        public List<AccountTreeNodeViewModel> ParentAccountTree { get; set; } = new List<AccountTreeNodeViewModel>();
        public bool ParentAccountConfigured { get; set; }
        public string? SelectedParentAccountName { get; set; }
        public List<DriverCodBranchSummaryViewModel> DriverCodBranchSummaries { get; set; } = new List<DriverCodBranchSummaryViewModel>();
        public List<CustomerBranchAccountNode> CustomerAccountBranches { get; set; } = new List<CustomerBranchAccountNode>();
        public List<BusinessShipmentBranchSummaryViewModel> BusinessShipmentBranchSummaries { get; set; } = new List<BusinessShipmentBranchSummaryViewModel>();
    }

    public class CashBoxTreeViewModel
    {
        public List<AccountTreeNodeViewModel> Nodes { get; set; } = new List<AccountTreeNodeViewModel>();
        public bool ParentConfigured { get; set; }
    }

    public class DriverCodBranchSummaryViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal ShipmentTotal { get; set; }
        public decimal ShipmentCod { get; set; }
    }

    public class DriverCodBranchDetailViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string DriverId { get; set; } = string.Empty;
        public decimal ShipmentTotal { get; set; }
        public decimal ShipmentCod { get; set; }
    }

    public class BusinessShipmentPriceViewModel
    {
        public long Id { get; set; }
        public string? ShipmentTrackingNo { get; set; }
        public long? ShipmentId { get; set; }
        public DateTime? EntryDate { get; set; }
        public int? BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public decimal ShipmentPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? DriverId { get; set; }
        public int? CompanyBranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int? BranchId { get; set; }
    }

    public class BusinessShipmentBranchSummaryViewModel
    {
        public int? BranchId { get; set; }
        public int? RoadCompanyBranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal TotalShipmentPrice { get; set; }
        public int ShipmentCount { get; set; }
        public bool CanLoadDetails => BranchId.HasValue;
    }

    public class DriverRevenueReportViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today;
        public DateTime ToDate { get; set; } = DateTime.Today;
        public string BaseCurrencyCode { get; set; } = string.Empty;
        public bool FiltersApplied { get; set; }
        public string? SearchTerm { get; set; }
        public List<DriverRevenueReportRowViewModel> Rows { get; set; } = new List<DriverRevenueReportRowViewModel>();
        public decimal GrandTotal { get; set; }
        public bool HasResults => Rows.Count > 0;
    }

    public class DriverRevenueReportRowViewModel
    {
        public int? DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
    }

    // Additional ViewModels
    public class EditJournalEntryViewModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public int BranchId { get; set; }
        public int? CostCenterId { get; set; }
        public List<JournalEntryLineViewModel> Lines { get; set; } = new List<JournalEntryLineViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CostCenters { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Accounts { get; set; } = new List<SelectListItem>();
    }

    public class AccountTreeNodeViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string ParentAccountName { get; set; } = string.Empty;
        public AccountType AccountType { get; set; }
        public AccountNature Nature { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal CurrentBalanceSelected { get; set; }
        public decimal CurrentBalanceBase { get; set; }
        public decimal Balance { get; set; }
        public decimal BalanceSelected { get; set; }
        public decimal BalanceBase { get; set; }
        public decimal DisplayBalance { get; set; }
        public decimal DisplayBalanceSelected { get; set; }
        public decimal DisplayBalanceBase { get; set; }
        public bool IsActive { get; set; }
        public bool CanPostTransactions { get; set; }
        public int? ParentId { get; set; }
        public int Level { get; set; }
        public bool HasChildren { get; set; }
        public List<AccountTreeNodeViewModel> Children { get; set; } = new List<AccountTreeNodeViewModel>();
    }

    public class JournalEntryViewModel
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string Status { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int LinesCount { get; set; }
        public string StatusDisplay { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public string DateFormatted { get; set; } = string.Empty;
        public string DateGroup { get; set; } = string.Empty;
        public string TotalAmountFormatted { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string TotalDebitFormatted { get; set; } = string.Empty;
        public string TotalCreditFormatted { get; set; } = string.Empty;
        public bool IsDraft { get; set; }
        public bool CanDelete { get; set; }
        public bool IsBalanced { get; set; }
    }

    public class JournalEntriesIndexViewModel
    {
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Statuses { get; set; } = new List<SelectListItem>();
    }

    // User Index ViewModels
    public class UserIndexViewModel
    {
        public List<UserViewModel> Users { get; set; } = new List<UserViewModel>();
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
    }

    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int BranchesCount { get; set; }
        public int PermissionsCount { get; set; }
    }

    public class UserPermissionsViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<int> SelectedPermissions { get; set; } = new List<int>();
        public List<SelectListItem> Permissions { get; set; } = new List<SelectListItem>();
    }
}

