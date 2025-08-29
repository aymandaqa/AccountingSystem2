using Microsoft.AspNetCore.Mvc.Rendering;
using AccountingSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    // Journal Entry ViewModels
    public class CreateJournalEntryViewModel
    {
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; } = DateTime.Now;
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
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
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string Description { get; set; } = string.Empty;
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
        public int? CostCenterId { get; set; }
        public List<SelectListItem> ParentAccounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CostCenters { get; set; } = new List<SelectListItem>();
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
        public int? CostCenterId { get; set; }
        public List<SelectListItem> ParentAccounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> CostCenters { get; set; } = new List<SelectListItem>();
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
        public int? CostCenterId { get; set; }
        public string CostCenterName { get; set; } = string.Empty;
        public int Level { get; set; }
        public bool HasChildren { get; set; }
        public bool HasTransactions { get; set; }
    }

    // Report ViewModels
    public class TrialBalanceViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Now.AddMonths(-1);
        public DateTime ToDate { get; set; } = DateTime.Now;
        public DateTime AsOfDate { get; set; } = DateTime.Now;
        public int? BranchId { get; set; }
        public List<TrialBalanceItemViewModel> Items { get; set; } = new List<TrialBalanceItemViewModel>();
        public List<TrialBalanceAccountViewModel> Accounts { get; set; } = new List<TrialBalanceAccountViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsBalanced { get; set; }
    }

    public class TrialBalanceAccountViewModel
    {
        public string AccountCode { get; set; } = "";
        public string AccountName { get; set; } = "";
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
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
        public DateTime AsOfDate { get; set; } = DateTime.Now;
        public int? BranchId { get; set; }
        public List<BalanceSheetItemViewModel> Assets { get; set; } = new List<BalanceSheetItemViewModel>();
        public List<BalanceSheetItemViewModel> Liabilities { get; set; } = new List<BalanceSheetItemViewModel>();
        public List<BalanceSheetItemViewModel> Equity { get; set; } = new List<BalanceSheetItemViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public bool IsBalanced { get; set; }
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
        public List<IncomeStatementItemViewModel> Revenues { get; set; } = new List<IncomeStatementItemViewModel>();
        public List<IncomeStatementItemViewModel> Expenses { get; set; } = new List<IncomeStatementItemViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public decimal TotalRevenues { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
    }

    public class IncomeStatementItemViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class AccountStatementViewModel
    {
        public int? AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? BranchId { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<AccountTransactionViewModel> Transactions { get; set; } = new List<AccountTransactionViewModel>();
        public List<SelectListItem> Accounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
    }

    public class AccountTransactionViewModel
    {
        public DateTime Date { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal RunningBalance { get; set; }
    }

    public class GeneralLedgerViewModel
    {
        public int? AccountId { get; set; }
        public DateTime FromDate { get; set; } = DateTime.Now.AddMonths(-1);
        public DateTime ToDate { get; set; } = DateTime.Now;
        public int? BranchId { get; set; }
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


    // Dashboard ViewModels
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
        public int? SelectedBranchId { get; set; }
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
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
        public AccountType AccountType { get; set; }
        public AccountNature Nature { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public bool CanPostTransactions { get; set; }
        public int? ParentId { get; set; }
        public int Level { get; set; }
        public bool HasChildren { get; set; }
        public List<AccountTreeNodeViewModel> Children { get; set; } = new List<AccountTreeNodeViewModel>();
    }

    public class JournalEntriesIndexViewModel
    {
        public List<JournalEntryViewModel> JournalEntries { get; set; } = new List<JournalEntryViewModel>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? BranchId { get; set; }
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
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
        public decimal TotalAmount { get; set; }
        public int LinesCount { get; set; }
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

