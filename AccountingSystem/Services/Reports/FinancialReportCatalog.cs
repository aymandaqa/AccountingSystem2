using System;
using System.Collections.Generic;
using System.Linq;
using AccountingSystem.ViewModels.Reports;

namespace AccountingSystem.Services.Reports
{
    public interface IFinancialReportCatalog
    {
        IReadOnlyCollection<DynamicRdlcReportDefinition> GetReports();
        DynamicRdlcReportDefinition? GetReport(string key);
    }

    public class FinancialReportCatalog : IFinancialReportCatalog
    {
        private readonly List<DynamicRdlcReportDefinition> _reports;

        public FinancialReportCatalog()
        {
            _reports = new List<DynamicRdlcReportDefinition>
            {
                new()
                {
                    Key = "JournalEntryLines",
                    Name = "تفاصيل القيود اليومية",
                    Description = "تقرير RDLC يعتمد على مصدر بيانات الكيانات ويعرض الحركات المالية حسب التصفية",
                    ReportPath = "wwwroot/ReportDefinitions/JournalEntryLines.rdlc",
                    Parameters =
                    {
                        new DynamicReportParameter("fromDate", "من تاريخ", DynamicReportParameterType.DateTime),
                        new DynamicReportParameter("toDate", "إلى تاريخ", DynamicReportParameterType.DateTime),
                        new DynamicReportParameter("branchId", "الفرع", DynamicReportParameterType.Lookup),
                        new DynamicReportParameter("accountId", "الحساب", DynamicReportParameterType.Lookup)
                    }
                },
                new()
                {
                    Key = "VoucherActivity",
                    Name = "حركة السندات",
                    Description = "يظهر سندات القبض والدفع والصرف بمصدر بيانات ديناميكي موحد",
                    ReportPath = "wwwroot/ReportDefinitions/VoucherActivity.rdlc",
                    Parameters =
                    {
                        new DynamicReportParameter("fromDate", "من تاريخ", DynamicReportParameterType.DateTime),
                        new DynamicReportParameter("toDate", "إلى تاريخ", DynamicReportParameterType.DateTime),
                        new DynamicReportParameter("currencyId", "العملة", DynamicReportParameterType.Lookup)
                    }
                }
            };
        }

        public IReadOnlyCollection<DynamicRdlcReportDefinition> GetReports() => _reports;

        public DynamicRdlcReportDefinition? GetReport(string key) => _reports.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}
