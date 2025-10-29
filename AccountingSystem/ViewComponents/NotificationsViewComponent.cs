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

        public async Task<IViewComponentResult> InvokeAsync(string category = "workflow")
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return View(NotificationViewModel.Create(category, Array.Empty<Notification>(), 0));
            }

            var user = await _userManager.GetUserAsync((System.Security.Claims.ClaimsPrincipal)User);
            if (user == null)
            {
                return View(NotificationViewModel.Create(category, Array.Empty<Notification>(), 0));
            }

            var notifications = category switch
            {
                NotificationCategories.Workflow => await _notificationService.GetRecentWorkflowNotificationsAsync(user.Id, 5),
                NotificationCategories.Login => await _notificationService.GetRecentLoginNotificationsAsync(user.Id, 5),
                _ => await _notificationService.GetRecentNotificationsAsync(user.Id, 5)
            };

            var unread = category switch
            {
                NotificationCategories.Workflow => await _notificationService.GetUnreadWorkflowCountAsync(user.Id),
                NotificationCategories.Login => await _notificationService.GetUnreadLoginCountAsync(user.Id),
                _ => await _notificationService.GetUnreadCountAsync(user.Id)
            };

            var model = NotificationViewModel.Create(category, notifications, unread);
            return View(model);
        }
    }

    public class NotificationViewModel
    {
        public IReadOnlyList<Notification> Notifications { get; set; } = Array.Empty<Notification>();
        public int UnreadCount { get; set; }
        public string Category { get; set; } = NotificationCategories.Workflow;
        public string Title { get; set; } = "الإشعارات";
        public string EmptyMessage { get; set; } = "لا توجد إشعارات";
        public string IconClass { get; set; } = "fa-bell";

        public string WrapperId => $"notificationsDropdownWrapper-{Category}";
        public string DropdownId => $"notificationsDropdown-{Category}";
        public string BadgeId => $"notificationsBadge-{Category}";
        public string ListId => $"notificationsList-{Category}";
        public string EmptyMessageId => $"notificationsEmptyMessage-{Category}";

        public static NotificationViewModel Create(string category, IReadOnlyList<Notification> notifications, int unread)
        {
            var normalizedCategory = NotificationCategories.Normalize(category);
            var (title, emptyMessage, icon) = normalizedCategory switch
            {
                NotificationCategories.Workflow => ("إشعارات سير العمل", "لا توجد إشعارات لسير العمل", "fa-bell"),
                NotificationCategories.Login => ("إشعارات تسجيل الدخول", "لا توجد إشعارات تسجيل الدخول", "fa-right-to-bracket"),
                _ => ("الإشعارات", "لا توجد إشعارات", "fa-bell")
            };

            return new NotificationViewModel
            {
                Category = normalizedCategory,
                Notifications = notifications,
                UnreadCount = unread,
                Title = title,
                EmptyMessage = emptyMessage,
                IconClass = icon
            };
        }
    }

    public static class NotificationCategories
    {
        public const string Workflow = "workflow";
        public const string Login = "login";
        public const string All = "all";

        public static string Normalize(string category)
        {
            return category?.ToLowerInvariant() switch
            {
                Login => Login,
                All => All,
                _ => Workflow
            };
        }
    }
}
