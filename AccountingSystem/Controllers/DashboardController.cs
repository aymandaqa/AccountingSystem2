using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models.Reports;
using AccountingSystem.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "dashboard.view")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            try
            {
                var records = await LoadRecordsForUserAsync();

                var viewModel = new CashPerformanceDashboardViewModel
                {
                    Records = records,
                    TotalCustomerDuesOnRoad = records.Sum(r => r.CustomerDuesOnRoad),
                    TotalCashWithDriverOnRoad = records.Sum(r => r.CashWithDriverOnRoad),
                    TotalCustomerDues = records.Sum(r => r.CustomerDues),
                    TotalCashOnBranchBox = records.Sum(r => r.CashOnBranchBox)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load cash performance data for the dashboard.");
                return View(new CashPerformanceDashboardViewModel());
            }
        }

        private async Task<IReadOnlyList<CashPerformanceRecord>> LoadRecordsForUserAsync()
        {


            var records = await _context.CashPerformanceRecords
                .AsNoTracking()
                .ToListAsync();

            return records;
        }
    }
}
