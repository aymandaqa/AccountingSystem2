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

        public JournalEntriesApiController(
            ApplicationDbContext context,
            IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateEntry([FromBody] CreateJournalEntryApiRequest request)
        {
            if (request == null)
            {
                return BadRequest("لم يتم تقديم بيانات صحيحة للقيد.");
            }
            var confif = await _context.SystemSettings.FirstOrDefaultAsync(t => t.Key == "X-API-Key");
            if (string.IsNullOrWhiteSpace(confif?.Value))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, "لم يتم إعداد مفتاح الـ API للقيد.");
            }

            if (!Request.Headers.TryGetValue("X-API-Key", out var providedKey) ||
                !string.Equals(providedKey, confif.Value, StringComparison.Ordinal))
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(confif.Value))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "لم يتم إعداد المستخدم المرتبط بمفتاح الـ API.");
            }

            var userId = confif.Value;
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "المستخدم المرتبط بمفتاح الـ API غير موجود.");
            }


            var branchExists = await _context.Branches.FirstOrDefaultAsync(b => b.Code == request.BranchId);
            if (branchExists != null)
            {
                ModelState.AddModelError(nameof(request.BranchId), "الفرع المحدد غير موجود.");
            }

            var branchCashAccountId = await _context.Users
                .Where(u => u.PaymentBranchId == branchExists.Id && u.PaymentAccountId != null)
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.PaymentAccountId)
                .FirstOrDefaultAsync();


            try
            {
                var bussAccount = await _context.CusomerMappingAccounts
                    .FirstOrDefaultAsync(a => a.CustomerId == request.BussId);

                var lines = new List<JournalEntryLine>();
                lines.Add(new JournalEntryLine
                {
                    AccountId = branchCashAccountId.Value,
                    DebitAmount = request.Amount,
                    CreditAmount = 0,
                    Description = request.Description
                });

                lines.Add(new JournalEntryLine
                {
                    AccountId = Convert.ToInt32(bussAccount.AccountId),
                    DebitAmount = 0,
                    CreditAmount = request.Amount,
                    Description = request.Description
                });

                var entry = await _journalEntryService.CreateJournalEntryAsync(
                  DateTime.Now,
                   request.Description,
                   branchExists.Id,
                   userId,
                   lines,
                   JournalEntryStatus.Posted,
                   request.Reference);


                return Ok(entry.Number);
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
