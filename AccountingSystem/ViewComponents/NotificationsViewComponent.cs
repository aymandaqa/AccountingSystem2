using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AccountingSystem.ViewComponents
{
    public class NotificationsViewComponent : ViewComponent
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<User> _userManager;

        public NotificationsViewComponent(INotificationService notificationService, UserManager<User> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return View(new NotificationViewModel());
            }

            var user = await _userManager.GetUserAsync((System.Security.Claims.ClaimsPrincipal)User);
            if (user == null)
            {
                return View(new NotificationViewModel());
            }

            var notifications = await _notificationService.GetRecentNotificationsAsync(user.Id, 5);
            var unread = await _notificationService.GetUnreadCountAsync(user.Id);
            var model = new NotificationViewModel
            {
                Notifications = notifications,
                UnreadCount = unread
            };
            return View(model);
        }
    }

    public class NotificationViewModel
    {
        public IReadOnlyList<Notification> Notifications { get; set; } = Array.Empty<Notification>();
        public int UnreadCount { get; set; }
    }
}
