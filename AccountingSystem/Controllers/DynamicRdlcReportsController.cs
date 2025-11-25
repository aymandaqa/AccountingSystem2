using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Services.Reports;
using AccountingSystem.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Syncfusion.EJ.ReportViewer;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class DynamicRdlcReportsController : Controller, IReportController
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IFinancialReportCatalog _catalog;
        private readonly IFinancialReportDataService _dataService;
        private readonly IMemoryCache _cache;
        private readonly ApplicationDbContext _context;

        public DynamicRdlcReportsController(
            IWebHostEnvironment environment,
            IFinancialReportCatalog catalog,
            IFinancialReportDataService dataService,
            IMemoryCache cache,
            ApplicationDbContext context)
        {
            _environment = environment;
            _catalog = catalog;
            _dataService = dataService;
            _cache = cache;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var viewModel = new DynamicRdlcReportViewModel
            {
                Reports = _catalog.GetReports().OrderBy(r => r.Name).ToList(),
                Lookups =
                {
                    ["branchId"] = await _context.Branches
                        .OrderBy(b => b.NameAr)
                        .Select(b => new SelectListItem { Text = b.NameAr, Value = b.Id.ToString() })
                        .ToListAsync(),
                    ["accountId"] = await _context.Accounts
                        .OrderBy(a => a.Number)
                        .Select(a => new SelectListItem { Text = $"{a.Number} - {a.NameAr ?? a.NameEn}", Value = a.Id.ToString() })
                        .ToListAsync(),
                    ["currencyId"] = await _context.Currencies
                        .OrderBy(c => c.Code)
                        .Select(c => new SelectListItem { Text = c.Code, Value = c.Id.ToString() })
                        .ToListAsync()
                }
            };

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult PostReportAction([FromBody] Dictionary<string, object> jsonResult)
        {
            return ReportHelper.ProcessReport(jsonResult, this, _cache);
        }

        [HttpGet]
        public IActionResult GetResource(string key, string resourcetype, bool isPrint)
        {
            return ReportHelper.GetResource(key, resourcetype, isPrint);
        }

        [HttpPost]
        public void OnInitReportOptions([FromBody] ReportViewerOptions reportOption)
        {
            ConfigureReport(reportOption).GetAwaiter().GetResult();
        }

        [HttpPost]
        public void OnReportLoaded([FromBody] ReportViewerOptions reportOption)
        {
        }

        private async Task ConfigureReport(ReportViewerOptions reportOption)
        {
            var reportKey = reportOption.ReportModel?.ReportPath ?? string.Empty;
            var definition = _catalog.GetReport(reportKey);
            if (definition == null)
            {
                return;
            }

            var fullPath = Path.Combine(_environment.ContentRootPath, definition.ReportPath);
            if (reportOption.ReportModel != null)
            {
                reportOption.ReportModel.ReportPath = fullPath;
                reportOption.ReportModel.DataSources ??= new List<ReportDataSource>();
                var parameterMap = BuildParameterMap(reportOption);
                var dataSources = await _dataService.GetDataSourcesAsync(definition.Key, parameterMap);
                foreach (var ds in dataSources)
                {
                    reportOption.ReportModel.DataSources.Add(new ReportDataSource { Name = ds.Key, Value = ds.Value });
                }
            }
        }

        private static Dictionary<string, string?> BuildParameterMap(ReportViewerOptions reportOption)
        {
            var parameterMap = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var parameters = reportOption.ReportModel?.Parameters;
            if (parameters == null)
            {
                return parameterMap;
            }

            foreach (var parameter in parameters)
            {
                var name = parameter.Name;
                string? value = null;
                if (parameter.Values != null && parameter.Values.Count > 0)
                {
                    value = parameter.Values[0]?.ToString();
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    parameterMap[name] = value;
                }
            }

            return parameterMap;
        }

        public void Dispose()
        {
        }
    }
}
