using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class CashBoxClosuresController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CashBoxClosuresController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Policy = "cashclosures.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return NotFound();

            var account = await _context.Accounts.FindAsync(user.PaymentAccountId);
            var branch = await _context.Branches.FindAsync(user.PaymentBranchId);

            var model = new CashBoxClosureCreateViewModel
            {
                AccountName = account?.NameAr ?? string.Empty,
                BranchName = branch?.NameAr ?? string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "cashclosures.create")]
        public async Task<IActionResult> Create(CashBoxClosureCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return NotFound();

            var account = await _context.Accounts.FindAsync(user.PaymentAccountId);
            if (account == null)
                return NotFound();

            var closure = new CashBoxClosure
            {
                UserId = user.Id,
                AccountId = account.Id,
                BranchId = user.PaymentBranchId.Value,
                CountedAmount = model.CountedAmount,
                OpeningBalance = account.CurrentBalance,
                ClosingBalance = model.CountedAmount,
                Notes = model.Notes,
                Status = CashBoxClosureStatus.Pending,
                CreatedAt = DateTime.Now
            };

            _context.CashBoxClosures.Add(closure);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyClosures));
        }

        [Authorize(Policy = "cashclosures.view")]
        public async Task<IActionResult> MyClosures()
        {
            var userId = _userManager.GetUserId(User);
            var closures = await _context.CashBoxClosures
                .Include(c => c.Account)
                .Include(c => c.Branch)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(closures);
        }

        [Authorize(Policy = "cashclosures.approve")]
        public async Task<IActionResult> Pending()
        {
            var closures = await _context.CashBoxClosures
                .Include(c => c.User)
                .Include(c => c.Account)
                .Include(c => c.Branch)
                .Where(c => c.Status == CashBoxClosureStatus.Pending)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
            return View(closures);
        }

        [HttpPost]
        [Authorize(Policy = "cashclosures.approve")]
        public async Task<IActionResult> Approve(int id, bool matched, string? reason)
        {
            var closure = await _context.CashBoxClosures.FindAsync(id);
            if (closure == null)
                return NotFound();

            var account = await _context.Accounts.FindAsync(closure.AccountId);
            closure.Status = matched ? CashBoxClosureStatus.ApprovedMatched : CashBoxClosureStatus.ApprovedWithDifference;
            closure.Reason = reason;
            closure.ApprovedAt = DateTime.Now;
            closure.ClosingDate = DateTime.Now;
            closure.ClosingBalance = account?.CurrentBalance ?? closure.ClosingBalance;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Pending));
        }

        [HttpPost]
        [Authorize(Policy = "cashclosures.approve")]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var closure = await _context.CashBoxClosures.FindAsync(id);
            if (closure == null)
                return NotFound();

            closure.Status = CashBoxClosureStatus.Rejected;
            closure.Reason = reason;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Pending));
        }
    }
}
