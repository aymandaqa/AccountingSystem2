using System;

namespace AccountingSystem.Extensions
{
    public static class TransactionTypeHelper
    {
        public static string GetTransactionType(string? reference, string? description)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return "قيد محاسبي يدوي";
            }

            var trimmed = reference.Trim();

            if (trimmed == "-")
            {
                return "قيد محاسبي يدوي";
            }

            if (trimmed.StartsWith("RCV:", StringComparison.OrdinalIgnoreCase))
            {
                return "سند قبض";
            }

            if (trimmed.StartsWith("DSBV:", StringComparison.OrdinalIgnoreCase))
            {
                return "سند دفع";
            }

            if (trimmed.StartsWith("سند مصاريف:", StringComparison.Ordinal))
            {
                return "سند مصاريف";
            }

            if (trimmed.StartsWith("سند دفع وكيل:", StringComparison.Ordinal))
            {
                return "سند دفع وكيل";
            }

            if (trimmed.StartsWith("SALPAY:", StringComparison.OrdinalIgnoreCase))
            {
                return "دفع راتب";
            }

            if (trimmed.StartsWith("EMPADV:", StringComparison.OrdinalIgnoreCase))
            {
                return "سلفة موظف";
            }

            if (trimmed.StartsWith("PR-", StringComparison.OrdinalIgnoreCase))
            {
                return "دفعة رواتب";
            }

            if (trimmed.StartsWith("CashBoxClosure:", StringComparison.OrdinalIgnoreCase))
            {
                return "إقفال صندوق";
            }

            if (trimmed.StartsWith("DriverInvoice:", StringComparison.OrdinalIgnoreCase))
            {
                return "فاتورة سائق";
            }

            if (trimmed.StartsWith("PaymenToBusiness:", StringComparison.OrdinalIgnoreCase))
            {
                return "دفعة بزنس";
            }

            if (trimmed.StartsWith("ASSETEXP:", StringComparison.OrdinalIgnoreCase))
            {
                return "مصروف أصل";
            }

            if (trimmed.StartsWith("ASSET:", StringComparison.OrdinalIgnoreCase))
            {
                return "عملية أصل";
            }

            if (trimmed.StartsWith("PAYV:", StringComparison.OrdinalIgnoreCase))
            {
                return "سند صرف";
            }

            return string.IsNullOrWhiteSpace(description)
                ? "حركة محاسبية"
                : description;
        }
    }
}
