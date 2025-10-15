using AccountingSystem.Data;
using AccountingSystem.Models.DynamicScreens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.ViewComponents
{
    public class DynamicScreensMenuViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorizationService;

        public DynamicScreensMenuViewComponent(ApplicationDbContext context, IAuthorizationService authorizationService)
        {
            _context = context;
            _authorizationService = authorizationService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var screens = await _context.DynamicScreenDefinitions
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.MenuOrder)
                .ThenBy(s => s.DisplayName)
                .ToListAsync();

            var accessible = new List<DynamicScreenDefinition>();
            foreach (var screen in screens)
            {
                if (string.IsNullOrWhiteSpace(screen.PermissionName))
                    continue;

                var authorized = await _authorizationService.AuthorizeAsync(UserClaimsPrincipal, null, screen.PermissionName);
                if (authorized.Succeeded)
                {
                    accessible.Add(screen);
                }
            }

            return View(accessible);
        }
    }
}
