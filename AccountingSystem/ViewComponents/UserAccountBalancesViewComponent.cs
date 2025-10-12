using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.ViewComponents
{
    public class UserAccountBalancesViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IAuthorizationService _authorizationService;

        public UserAccountBalancesViewComponent(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IAuthorizationService authorizationService)
        {
            _context = context;
            _userManager = userManager;
            _authorizationService = authorizationService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Content(string.Empty);
            }

            var authorizationResult = await _authorizationService.AuthorizeAsync(UserClaimsPrincipal, "userbalances.view");
            if (!authorizationResult.Succeeded)
            {
                return Content(string.Empty);
            }

            var user = await _userManager.GetUserAsync(UserClaimsPrincipal);
            if (user == null)
            {
                return Content(string.Empty);
            }

            var accounts = await _context.UserPaymentAccounts
                .AsNoTracking()
                .Where(upa => upa.UserId == user.Id)
                .Include(upa => upa.Account)
                    .ThenInclude(a => a.Currency)
                .ToListAsync();

            var items = accounts
                .Where(upa => upa.Account != null && upa.Account.Currency != null)
                .Select(upa => new UserAccountBalanceViewModel
                {
                    AccountId = upa.AccountId,
                    AccountCode = upa.Account.Code,
                    AccountName = upa.Account.NameAr,
                    CurrencyCode = upa.Account.Currency.Code,
                    CurrentBalance = upa.Account.CurrentBalance,
                    IsAgentAccount = false
                })
                .ToList();

            if (user.AgentId.HasValue)
            {
                var agent = await _context.Agents
                    .AsNoTracking()
                    .Include(a => a.Account)
                        .ThenInclude(a => a!.Currency)
                    .FirstOrDefaultAsync(a => a.Id == user.AgentId.Value);

                if (agent?.Account != null && agent.Account.Currency != null)
                {
                    var alreadyExists = items.Any(i => i.AccountId == agent.Account.Id);
                    if (!alreadyExists)
                    {
                        items.Add(new UserAccountBalanceViewModel
                        {
                            AccountId = agent.Account.Id,
                            AccountCode = agent.Account.Code,
                            AccountName = agent.Account.NameAr,
                            CurrencyCode = agent.Account.Currency.Code,
                            CurrentBalance = agent.Account.CurrentBalance,
                            IsAgentAccount = true
                        });
                    }
                }
            }

            items = items
                .OrderBy(i => i.IsAgentAccount)
                .ThenBy(i => i.AccountCode)
                .ToList();

            return View(items);
        }
    }
}
