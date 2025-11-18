using System;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using AccountingSystem.Configuration;

namespace AccountingSystem.Controllers
{
    [ApiController]
    [Route("api/journal-entries")]
    public class JournalEntriesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;
        private readonly JournalEntryApiOptions _apiOptions;

        public JournalEntriesApiController(
            ApplicationDbContext context,
            IJournalEntryService journalEntryService,
            IOptions<JournalEntryApiOptions> apiOptions)
        {
            _context = context;
            _journalEntryService = journalEntryService;
            _apiOptions = apiOptions.Value;
        }

        [HttpPost]
        public async Task<IActionResult> CreateEntry([FromBody] CreateJournalEntryApiRequest request)
        {
            if (request == null)
            {
                return BadRequest("لم يتم تقديم بيانات صحيحة للقيد.");
            }

            if (string.IsNullOrWhiteSpace(_apiOptions.ApiKey))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "لم يتم إعداد مفتاح الـ API للقيد.");
            }

            if (!Request.Headers.TryGetValue("X-API-Key", out var providedKey) ||
                !string.Equals(providedKey, _apiOptions.ApiKey, StringComparison.Ordinal))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(_apiOptions.UserId))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "لم يتم إعداد المستخدم المرتبط بمفتاح الـ API.");
            }

            var userId = _apiOptions.UserId;
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "المستخدم المرتبط بمفتاح الـ API غير موجود.");
            }

            if (request.Lines == null || request.Lines.Count == 0)
            {
                ModelState.AddModelError(nameof(request.Lines), "يجب إضافة بند واحد على الأقل للقيد.");
            }

            if (request.Status != JournalEntryStatus.Draft && request.Status != JournalEntryStatus.Posted)
            {
                ModelState.AddModelError(nameof(request.Status), "يمكن إنشاء القيود بحالة مسودة أو مرحلة فقط.");
            }

            for (var i = 0; i < (request.Lines?.Count ?? 0); i++)
            {
                var line = request.Lines![i];
                var hasDebit = line.DebitAmount > 0;
                var hasCredit = line.CreditAmount > 0;

                if (!hasDebit && !hasCredit)
                {
                    ModelState.AddModelError($"{nameof(request.Lines)}[{i}]", "يجب تحديد مبلغ مدين أو دائن لكل بند.");
                }

                if (hasDebit && hasCredit)
                {
                    ModelState.AddModelError($"{nameof(request.Lines)}[{i}]", "لا يمكن أن يكون البند مديناً ودائناً في نفس الوقت.");
                }
            }

            var branchExists = await _context.Branches.AnyAsync(b => b.Id == request.BranchId);
            if (!branchExists)
            {
                ModelState.AddModelError(nameof(request.BranchId), "الفرع المحدد غير موجود.");
            }

            var branchCashAccountId = await _context.Users
                .Where(u => u.PaymentBranchId == request.BranchId && u.PaymentAccountId != null)
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.PaymentAccountId)
                .FirstOrDefaultAsync();

            if (branchCashAccountId == null)
            {
                ModelState.AddModelError(nameof(request.BranchId), "لا يوجد مستخدم مرتبط بالفرع يحتوي على حساب دفع ليكون حساب الصندوق.");
            }
            else
            {
                var cashAccount = await _context.Accounts
                    .Where(a => a.Id == branchCashAccountId.Value)
                    .Select(a => new { a.Id, a.CanPostTransactions })
                    .FirstOrDefaultAsync();

                if (cashAccount == null)
                {
                    ModelState.AddModelError(nameof(request.BranchId), "حساب الصندوق المرتبط بالفرع غير موجود.");
                }
                else if (!cashAccount.CanPostTransactions)
                {
                    ModelState.AddModelError(nameof(request.BranchId), "حساب الصندوق المرتبط بالفرع غير قابل للترحيل.");
                }
                else if (!(request.Lines?.Any(l => l.AccountId == cashAccount.Id) ?? false))
                {
                    ModelState.AddModelError(nameof(request.Lines), "يجب استخدام حساب الصندوق المرتبط بالفرع ضمن بنود القيد.");
                }
            }

            var accountIds = request.Lines?.Select(l => l.AccountId).Distinct().ToList() ?? new List<int>();
            if (accountIds.Count > 0)
            {
                var accounts = await _context.Accounts
                    .Where(a => accountIds.Contains(a.Id))
                    .Select(a => new { a.Id, a.CanPostTransactions })
                    .ToListAsync();

                var missingAccounts = accountIds.Except(accounts.Select(a => a.Id)).ToList();
                if (missingAccounts.Count > 0)
                {
                    ModelState.AddModelError(nameof(request.Lines), $"بعض الحسابات غير موجودة: {string.Join(", ", missingAccounts)}.");
                }

                var blockedAccounts = accounts.Where(a => !a.CanPostTransactions).Select(a => a.Id).ToList();
                if (blockedAccounts.Count > 0)
                {
                    ModelState.AddModelError(nameof(request.Lines), $"الحسابات التالية غير قابلة للترحيل: {string.Join(", ", blockedAccounts)}.");
                }
            }

            var costCenterIds = request.Lines?
                .Where(l => l.CostCenterId.HasValue)
                .Select(l => l.CostCenterId!.Value)
                .Distinct()
                .ToList() ?? new List<int>();

            if (costCenterIds.Count > 0)
            {
                var existingCostCenters = await _context.CostCenters
                    .Where(c => costCenterIds.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToListAsync();

                var missingCostCenters = costCenterIds.Except(existingCostCenters).ToList();
                if (missingCostCenters.Count > 0)
                {
                    ModelState.AddModelError(nameof(request.Lines), $"مراكز التكلفة التالية غير موجودة: {string.Join(", ", missingCostCenters)}.");
                }
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var description = string.IsNullOrWhiteSpace(request.Description)
                ? "-"
                : request.Description.Trim();

            var entryLines = request.Lines!
                .Select(line => new JournalEntryLine
                {
                    AccountId = line.AccountId,
                    Description = string.IsNullOrWhiteSpace(line.Description) ? description : line.Description,
                    Reference = line.Reference,
                    DebitAmount = Math.Round(line.DebitAmount, 2, MidpointRounding.AwayFromZero),
                    CreditAmount = Math.Round(line.CreditAmount, 2, MidpointRounding.AwayFromZero),
                    CostCenterId = line.CostCenterId
                });

            try
            {
                var entry = await _journalEntryService.CreateJournalEntryAsync(
                    request.Date,
                    description,
                    request.BranchId,
                    userId,
                    entryLines,
                    request.Status,
                    request.Reference,
                    request.Number);

                var response = new JournalEntryApiResponse
                {
                    Id = entry.Id,
                    Number = entry.Number,
                    Date = entry.Date,
                    Description = entry.Description,
                    Reference = entry.Reference,
                    Status = entry.Status,
                    BranchId = entry.BranchId,
                    CashAccountId = branchCashAccountId,
                    TotalDebit = entry.TotalDebit,
                    TotalCredit = entry.TotalCredit,
                    Lines = entry.Lines
                        .Select(l => new JournalEntryLineApiResponse
                        {
                            AccountId = l.AccountId,
                            Description = l.Description,
                            Reference = l.Reference,
                            DebitAmount = l.DebitAmount,
                            CreditAmount = l.CreditAmount,
                            CostCenterId = l.CostCenterId
                        })
                        .ToList()
                };

                var location = Url.Action("Details", "JournalEntries", new { id = entry.Id }, Request.Scheme);
                return Created(location ?? string.Empty, response);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return ValidationProblem(ModelState);
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return ValidationProblem(ModelState);
            }
        }
    }
}
