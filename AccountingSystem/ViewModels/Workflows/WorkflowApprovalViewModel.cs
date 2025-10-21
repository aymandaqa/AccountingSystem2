using AccountingSystem.Models;
using AccountingSystem.Models.DynamicScreens;
using AccountingSystem.Models.Workflows;
using System;

namespace AccountingSystem.ViewModels.Workflows
{
    public class WorkflowApprovalViewModel
    {
        public int ActionId { get; set; }
        public WorkflowDocumentType DocumentType { get; set; }
        public int DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public PaymentVoucher? PaymentVoucher { get; set; }
        public DynamicScreenEntry? DynamicEntry { get; set; }
        public ReceiptVoucher? ReceiptVoucher { get; set; }
        public DisbursementVoucher? DisbursementVoucher { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountInBase { get; set; }
        public string? CurrencyCode { get; set; }
    }
}
