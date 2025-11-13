using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models.Reports
{
    [Table("ViewCashPerformance")]
    public class CashPerformanceRecord
    {
        public string? BranchCode { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public decimal CustomerDuesOnRoad { get; set; }

        public decimal CashWithDriverOnRoad { get; set; }

        public decimal CustomerDues { get; set; }

        public decimal CashOnBranchBox { get; set; }
    }
}
