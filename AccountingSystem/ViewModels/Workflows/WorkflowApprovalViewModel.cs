using System;
using System.Collections.Generic;
using AccountingSystem.Models;
using AccountingSystem.Models.DynamicScreens;
using AccountingSystem.Models.Workflows;

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
        public AssetExpense? AssetExpense { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountInBase { get; set; }
        public string? CurrencyCode { get; set; }
        public List<WorkflowAttachmentViewModel> Attachments { get; set; } = new();
        public List<WorkflowJournalEntryLineViewModel> JournalLines { get; set; } = new();
        public string? JournalPreviewError { get; set; }
    }

    public class WorkflowAttachmentViewModel
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
    }

    public class WorkflowJournalEntryLineViewModel
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Description { get; set; }
        public string? CostCenter { get; set; }
    }
}
