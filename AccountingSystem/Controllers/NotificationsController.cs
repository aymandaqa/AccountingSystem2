using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<User> _userManager;

        public NotificationsController(INotificationService notificationService, UserManager<User> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var notifications = await _notificationService.GetUserNotificationsAsync(user.Id);
            var workflowNotifications = notifications.Where(n => n.WorkflowActionId.HasValue).ToList();
            var loginNotifications = notifications.Where(n => !n.WorkflowActionId.HasValue).ToList();
            var unread = await _notificationService.GetUnreadCountAsync(user.Id);

            var model = new NotificationsIndexViewModel
            {
                Notifications = notifications,
                WorkflowNotifications = workflowNotifications,
                LoginNotifications = loginNotifications,
                UnreadCount = unread,
                WorkflowUnreadCount = workflowNotifications.Count(n => !n.IsRead),
                LoginUnreadCount = loginNotifications.Count(n => !n.IsRead)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            await _notificationService.MarkAsReadAsync(id, user.Id);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            await _notificationService.MarkAllAsReadAsync(user.Id);
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }

            return RedirectToAction("Index", "Dashboard");
        }
    }
}
