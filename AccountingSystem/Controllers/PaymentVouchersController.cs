using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Models.Workflows;
using System.Linq;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "paymentvouchers.view")]
    public class PaymentVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IPaymentVoucherProcessor _paymentVoucherProcessor;

        public PaymentVouchersController(ApplicationDbContext context, UserManager<User> userManager, IWorkflowService workflowService, IPaymentVoucherProcessor paymentVoucherProcessor)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _paymentVoucherProcessor = paymentVoucherProcessor;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var vouchersQuery = _context.PaymentVouchers
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                .AsQueryable();

            if (user.PaymentBranchId.HasValue)
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedBy.PaymentBranchId == user.PaymentBranchId);
            }
            else
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedById == user.Id);
            }

            var vouchers = await vouchersQuery
                .OrderByDescending(v => v.Date)
                .ToListAsync();

            return View(vouchers);
        }

        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Suppliers = await _context.Suppliers
                .Include(s => s.Account).ThenInclude(a => a.Currency)
                .Where(s => s.AccountId != null)
                .Select(s => new
                {
                    s.Id,
                    s.NameAr,
                    s.AccountId,
                    CurrencyId = s.Account!.CurrencyId,
                    CurrencyCode = s.Account.Currency.Code
                })
                .ToListAsync();
        }

        private async Task PopulatePaymentAccountSelectListAsync()
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SupplierPaymentsParentAccountId");
            if (setting != null && int.TryParse(setting.Value, out var parentAccountId))
            {
                ViewBag.Accounts = await _context.Accounts
                    .Where(a => a.ParentId == parentAccountId)
                    .Include(a => a.Currency)
                    .Select(a => new { a.Id, a.Code, a.NameAr, a.CurrencyId, CurrencyCode = a.Currency.Code })
                    .ToListAsync();
            }
            else
            {
                ViewBag.Accounts = new List<object>();
            }
        }

        private async Task PopulateAgentSelectListAsync()
        {
            ViewBag.Agents = await _context.Agents
                .Include(a => a.Account).ThenInclude(a => a.Currency)
                .Where(a => a.AccountId != null)
                .OrderBy(a => a.Name)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.AccountId,
                    CurrencyId = a.Account!.CurrencyId,
                    CurrencyCode = a.Account.Currency.Code
                })
                .ToListAsync();
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vouchers = await _context.PaymentVouchers
                .Where(v => v.CreatedById == user!.Id)
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.Agent).ThenInclude(a => a.Account)
                .Include(v => v.Currency)
                .OrderByDescending(v => v.Date)
                .ToListAsync();
            return View(vouchers);
        }

        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> Create()
        {
            await PopulateSupplierSelectListAsync();
            await PopulatePaymentAccountSelectListAsync();

            return View(new PaymentVoucher { Date = DateTime.Now, IsCash = true });
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == model.SupplierId);
            if (supplier?.Account == null)
                ModelState.AddModelError("SupplierId", "المورد غير موجود");

            Account? selectedAccount = await _context.Accounts.FindAsync(model.AccountId);
            var settingAccount = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SupplierPaymentsParentAccountId");
            if (selectedAccount == null || settingAccount == null || !int.TryParse(settingAccount.Value, out var parentId) || selectedAccount.ParentId != parentId)
                ModelState.AddModelError("AccountId", "الحساب غير صالح");

            Account? cashAccount = null;
            if (model.IsCash)
            {
                cashAccount = await _context.Accounts.FindAsync(user.PaymentAccountId.Value);
                if (cashAccount != null && cashAccount.Nature == AccountNature.Debit && model.Amount > cashAccount.CurrentBalance)
                    ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
            }

            if (supplier?.Account != null && selectedAccount != null)
            {
                if (supplier.Account.CurrencyId != selectedAccount.CurrencyId)
                    ModelState.AddModelError("SupplierId", "يجب أن تكون الحسابات بنفس العملة");
                if (model.IsCash && cashAccount != null && selectedAccount.CurrencyId != cashAccount.CurrencyId)
                    ModelState.AddModelError("AccountId", "يجب أن تكون الحسابات بنفس العملة");
            }

            if (supplier?.Account != null)
            {
                model.CurrencyId = supplier.Account.CurrencyId;
            }

            ModelState.Remove(nameof(PaymentVoucher.CurrencyId));
            ModelState.Remove(nameof(PaymentVoucher.ExchangeRate));

            if (!ModelState.IsValid)
            {
                await PopulateSupplierSelectListAsync();
                await PopulatePaymentAccountSelectListAsync();
                return View(model);
            }

            return await FinalizeCreationAsync(model, user);
        }
        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> CreateFromAgent()
        {
            await PopulateSupplierSelectListAsync();
            await PopulateAgentSelectListAsync();

            return View(new PaymentVoucher { Date = DateTime.Now, IsCash = false });
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromAgent(PaymentVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            ModelState.Remove(nameof(PaymentVoucher.AccountId));

            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == model.SupplierId);
            if (supplier?.Account == null)
                ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "المورد غير موجود");

            var agent = await _context.Agents
                .Include(a => a.Account)
                .FirstOrDefaultAsync(a => a.Id == model.AgentId);
            if (agent?.Account == null)
                ModelState.AddModelError(nameof(PaymentVoucher.AgentId), "الوكيل غير موجود");

            if (supplier?.Account != null && agent?.Account != null)
            {
                if (supplier.Account.CurrencyId != agent.Account.CurrencyId)
                    ModelState.AddModelError(nameof(PaymentVoucher.AgentId), "يجب أن تكون الحسابات بنفس العملة");

                model.CurrencyId = supplier.Account.CurrencyId;
                model.AccountId = agent.Account.Id;
                model.AgentId = agent.Id;
                model.IsCash = false;
            }

            ModelState.Remove(nameof(PaymentVoucher.CurrencyId));
            ModelState.Remove(nameof(PaymentVoucher.ExchangeRate));

            if (!ModelState.IsValid)
            {
                await PopulateSupplierSelectListAsync();
                await PopulateAgentSelectListAsync();
                return View(model);
            }

            return await FinalizeCreationAsync(model, user);
        }

        private async Task<IActionResult> FinalizeCreationAsync(PaymentVoucher model, User user)
        {
            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.PaymentVoucher, user.PaymentBranchId);
            model.Status = definition != null ? PaymentVoucherStatus.PendingApproval : PaymentVoucherStatus.Approved;

            _context.PaymentVouchers.Add(model);
            await _context.SaveChangesAsync();

            if (definition != null)
            {
                var instance = await _workflowService.StartWorkflowAsync(definition, WorkflowDocumentType.PaymentVoucher, model.Id, user.Id, user.PaymentBranchId);
                if (instance != null)
                {
                    model.WorkflowInstanceId = instance.Id;
                    await _context.SaveChangesAsync();
                }

                TempData["InfoMessage"] = "تم إرسال سند الدفع لاعتمادات الموافقة";
            }
            else
            {
                await _paymentVoucherProcessor.FinalizeVoucherAsync(model, user.Id);
                TempData["SuccessMessage"] = "تم إنشاء سند الدفع واعتماده فوراً";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
