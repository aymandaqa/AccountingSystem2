namespace AccountingSystem.Models.DynamicScreens
{
    public enum DynamicScreenType
    {
        Payment = 1,
        Receipt = 2
    }

    public enum DynamicScreenPaymentMode
    {
        CashOnly = 1,
        NonCashOnly = 2,
        CashAndNonCash = 3
    }

    public enum DynamicScreenFieldType
    {
        Text = 1,
        TextArea = 2,
        Number = 3,
        Date = 4,
        Select = 5,
        Toggle = 6
    }

    public enum DynamicScreenFieldDataSource
    {
        None = 0,
        Accounts = 1,
        Suppliers = 2,
        Expenses = 3,
        Assets = 4,
        Employees = 5,
        CustomOptions = 6
    }

    public enum DynamicScreenFieldRole
    {
        None = 0,
        Amount = 1,
        Description = 2,
        Supplier = 3,
        ExpenseAccount = 4,
        SecondaryCategory = 5,
        Branch = 6,
        PaymentMode = 7
    }

    public enum DynamicScreenEntryStatus
    {
        Draft = 0,
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3
    }
}
