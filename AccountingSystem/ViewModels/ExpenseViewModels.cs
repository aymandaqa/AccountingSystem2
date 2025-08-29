using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class CreateExpenseViewModel
    {
        [Range(0.01, double.MaxValue, ErrorMessage = "يجب إدخال قيمة للمصروف")] 
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار حساب المصروف")] 
        public int ExpenseAccountId { get; set; }
        public List<SelectListItem> ExpenseAccounts { get; set; } = new List<SelectListItem>();
        public string PaymentAccountName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
    }

    public class ExpenseViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string PaymentAccountName { get; set; } = string.Empty;
        public string ExpenseAccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
        public bool IsApproved { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
