using System;
using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "assetexpenses.view")]
    public class AssetExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IAssetExpenseProcessor _assetExpenseProcessor;

        public AssetExpensesController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWorkflowService workflowService,
            IAssetExpenseProcessor assetExpenseProcessor)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _assetExpenseProcessor = assetExpenseProcessor;
        }

        public async Task<IActionResult> Index()
        {
            var expenses = await _context.AssetExpenses
                .Include(e => e.Asset).ThenInclude(a => a.Branch)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Supplier)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var expenseIds = expenses.Select(e => e.Id).ToList();
            var workflowInstances = await _context.WorkflowInstances
                .Where(i => i.DocumentType == WorkflowDocumentType.AssetExpense && expenseIds.Contains(i.DocumentId))
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var workflowLookup = workflowInstances
                .GroupBy(i => i.DocumentId)
                .ToDictionary(g => g.Key, g => g.First());

            var references = expenses
                .Select(e => $"ASSETEXP:{e.Id}")
                .ToList();

            var journalEntries = await _context.JournalEntries
                .Where(j => j.Reference != null && references.Contains(j.Reference))
                .Select(j => new { j.Reference, j.Id, j.Number })
                .ToListAsync();

            var journalEntryLookup = journalEntries
                .ToDictionary(j => j.Reference!, j => (j.Id, j.Number));

            var model = expenses.Select(e =>
            {
                var reference = $"ASSETEXP:{e.Id}";
                int? journalEntryId = null;
                string? journalEntryNumber = null;

                if (journalEntryLookup.TryGetValue(reference, out var entryInfo))
                {
                    journalEntryId = entryInfo.Id;
                    journalEntryNumber = entryInfo.Number;
                }

                workflowLookup.TryGetValue(e.Id, out var instance);

                return new AssetExpenseListViewModel
                {
                    Id = e.Id,
                    AssetName = e.Asset.Name,
                    BranchName = e.Asset.Branch.NameAr,
                    ExpenseAccountName = e.ExpenseAccount.NameAr,
                    SupplierName = e.Supplier.NameAr,
                    Amount = e.Amount,
                    IsCash = e.IsCash,
                    Date = e.Date,
                    Notes = e.Notes,
                    JournalEntryId = journalEntryId,
                    JournalEntryNumber = journalEntryNumber,
                    WorkflowStatus = instance?.Status
                };
            }).ToList();

            return View(model);
        }

        [Authorize(Policy = "assetexpenses.create")]
        public async Task<IActionResult> Create()
        {
            var model = new CreateAssetExpenseViewModel
            {
                Date = DateTime.Now,
                Assets = await GetAssetsAsync(),
                ExpenseAccounts = await GetExpenseAccountsAsync(),
                Suppliers = await GetSuppliersAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assetexpenses.create")]
        public async Task<IActionResult> Create(CreateAssetExpenseViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var asset = await _context.Assets
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == model.AssetId);
            if (asset == null)
            {
                ModelState.AddModelError(nameof(model.AssetId), "الأصل غير موجود");
            }

            Account? expenseAccount = null;
            var expenseSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AssetExpensesParentAccountId");
            if (!string.IsNullOrEmpty(expenseSetting?.Value) && int.TryParse(expenseSetting.Value, out var expenseParentId))
            {
                expenseAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == model.ExpenseAccountId && a.ParentId == expenseParentId);
            }
            else
            {
                ModelState.AddModelError(string.Empty, "لم يتم ضبط حسابات مصاريف الأصول في الإعدادات");
            }

            Supplier? supplier = null;
            if (!model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SupplierId), "الرجاء اختيار المورد");
            }
            else
            {
                supplier = await _context.Suppliers
                    .Include(s => s.Account)
                        .ThenInclude(a => a.Currency)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);

                if (supplier?.Account == null)
                {
                    ModelState.AddModelError(nameof(model.SupplierId), "المورد غير موجود أو لا يملك حساباً");
                }
            }

            if (expenseAccount != null)
            {
                model.CurrencyId = expenseAccount.CurrencyId;
                if (supplier?.Account != null && supplier.Account.CurrencyId != expenseAccount.CurrencyId)
                {
                    ModelState.AddModelError(nameof(model.SupplierId), "يجب أن تكون الحسابات بنفس العملة");
                }
                if (model.IsCash && user.PaymentAccountId.HasValue)
                {
                    var paymentAccount = await _context.Accounts.FindAsync(user.PaymentAccountId.Value);
                    if (paymentAccount != null)
                    {
                        if (paymentAccount.CurrencyId != expenseAccount.CurrencyId)
                        {
                            ModelState.AddModelError(string.Empty, "يجب أن تكون الحسابات بنفس العملة");
                        }

                        if (paymentAccount.Nature == AccountNature.Debit && model.Amount > paymentAccount.CurrentBalance)
                        {
                            ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
                        }
                    }
                }
            }

            if (model.IsCash && (!user.PaymentAccountId.HasValue || !user.PaymentBranchId.HasValue))
            {
                ModelState.AddModelError(string.Empty, "لا يوجد حساب/فرع للدفع مضبوط للمستخدم");
            }

            if (!ModelState.IsValid)
            {
                model.Assets = await GetAssetsAsync();
                model.ExpenseAccounts = await GetExpenseAccountsAsync();
                model.Suppliers = await GetSuppliersAsync();
                return View(model);
            }

            if (model.ExchangeRate <= 0)
            {
                var currency = await _context.Currencies.FindAsync(model.CurrencyId);
                model.ExchangeRate = currency?.ExchangeRate ?? 1m;
            }

            var assetExpense = new AssetExpense
            {
                AssetId = model.AssetId,
                ExpenseAccountId = model.ExpenseAccountId,
                SupplierId = model.SupplierId!.Value,
                CurrencyId = model.CurrencyId,
                Amount = model.Amount,
                ExchangeRate = model.ExchangeRate,
                Date = model.Date,
                Notes = model.Notes,
                IsCash = model.IsCash,
                CreatedById = user.Id
            };

            if (assetExpense.IsCash)
            {
                assetExpense.AccountId = user.PaymentAccountId;
            }

            _context.AssetExpenses.Add(assetExpense);
            await _context.SaveChangesAsync();

            var branchId = asset.BranchId;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.AssetExpense, branchId);

            if (definition != null)
            {
                var baseAmount = assetExpense.Amount * assetExpense.ExchangeRate;
                await _workflowService.StartWorkflowAsync(
                    definition,
                    WorkflowDocumentType.AssetExpense,
                    assetExpense.Id,
                    user.Id,
                    branchId,
                    assetExpense.Amount,
                    baseAmount,
                    assetExpense.CurrencyId);

                TempData["InfoMessage"] = "تم إرسال مصروف الأصل لاعتمادات الموافقة";
            }
            else
            {
                await _assetExpenseProcessor.FinalizeAsync(assetExpense, user.Id);
                TempData["SuccessMessage"] = "تم إنشاء مصروف الأصل واعتماده فوراً";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<AssetExpenseSupplierOption>> GetSuppliersAsync()
        {
            return await _context.Suppliers
                .Include(s => s.Account)
                    .ThenInclude(a => a.Currency)
                .Where(s => s.AccountId != null && s.Account != null)
                .OrderBy(s => s.NameAr)
                .Select(s => new AssetExpenseSupplierOption
                {
                    Id = s.Id,
                    DisplayName = s.NameAr,
                    AccountId = s.AccountId!.Value,
                    CurrencyId = s.Account!.CurrencyId,
                    CurrencyCode = s.Account.Currency.Code
                })
                .ToListAsync();
        }

        private async Task<IEnumerable<SelectListItem>> GetAssetsAsync()
        {
            return await _context.Assets
                .Include(a => a.Branch)
                .OrderBy(a => a.Name)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Name} - {a.Branch.NameAr}"
                }).ToListAsync();
        }

        private async Task<IEnumerable<AssetExpenseAccountOption>> GetExpenseAccountsAsync()
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AssetExpensesParentAccountId");
            if (setting != null)
            {

                var acc = await _context.Accounts.FirstOrDefaultAsync(t => t.Code == setting.Value);
                return await _context.Accounts
                    .Where(a => a.ParentId == acc.Id)
                    .Include(a => a.Currency)
                    .OrderBy(a => a.Code)
                    .Select(a => new AssetExpenseAccountOption
                    {
                        Id = a.Id,
                        DisplayName = $"{a.Code} - {a.NameAr} ({a.Currency.Code})",
                        CurrencyId = a.CurrencyId,
                        CurrencyCode = a.Currency.Code
                    }).ToListAsync();
            }

            return Enumerable.Empty<AssetExpenseAccountOption>();
        }

    }
}
