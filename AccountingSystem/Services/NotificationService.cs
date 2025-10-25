using AccountingSystem.Data;
using AccountingSystem.Hubs;
using AccountingSystem.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task CreateNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            await _context.Notifications.AddAsync(notification, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await SendNotificationAsync(notification, cancellationToken);
        }

        public async Task CreateNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
        {
            var notificationList = notifications.ToList();
            if (notificationList.Count == 0)
            {
                return;
            }

            await _context.Notifications.AddRangeAsync(notificationList, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var notification in notificationList)
            {
                await SendNotificationAsync(notification, cancellationToken);
            }
        }

        public async Task<IReadOnlyList<Notification>> GetRecentNotificationsAsync(string userId, int count = 5, CancellationToken cancellationToken = default)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
        }

        public async Task MarkAsReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

            if (notification == null || notification.IsRead)
            {
                return;
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);

            await _hubContext.Clients.User(userId).SendAsync("NotificationMarkedAsRead", notification.Id, cancellationToken);
            await NotifyUnreadCountAsync(userId, cancellationToken);
        }

        public async Task MarkWorkflowActionNotificationsAsReadAsync(int workflowActionId, string userId, CancellationToken cancellationToken = default)
        {
            var notifications = await _context.Notifications
                .Where(n => n.WorkflowActionId == workflowActionId && n.UserId == userId && !n.IsRead)
                .ToListAsync(cancellationToken);

            if (notifications.Count == 0)
                return;

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var notification in notifications)
            {
                await _hubContext.Clients.User(userId).SendAsync("NotificationMarkedAsRead", notification.Id, cancellationToken);
            }

            await NotifyUnreadCountAsync(userId, cancellationToken);
        }

        public async Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync(cancellationToken);

            if (notifications.Count == 0)
                return;

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync(cancellationToken);

            await _hubContext.Clients.User(userId).SendAsync("NotificationsCleared", cancellationToken);
            await NotifyUnreadCountAsync(userId, cancellationToken);
        }

        private async Task SendNotificationAsync(Notification notification, CancellationToken cancellationToken)
        {
            var payload = new
            {
                id = notification.Id,
                title = notification.Title,
                message = notification.Message,
                link = notification.Link,
                isRead = notification.IsRead,
                createdAt = notification.CreatedAt,
                icon = notification.Icon,
                workflowActionId = notification.WorkflowActionId
            };

            await _hubContext.Clients.User(notification.UserId).SendAsync("ReceiveNotification", payload, cancellationToken);
            await NotifyUnreadCountAsync(notification.UserId, cancellationToken);
        }

        private async Task NotifyUnreadCountAsync(string userId, CancellationToken cancellationToken)
        {
            var count = await GetUnreadCountAsync(userId, cancellationToken);
            await _hubContext.Clients.User(userId).SendAsync("UnreadCountUpdated", count, cancellationToken);
        }
    }
}
