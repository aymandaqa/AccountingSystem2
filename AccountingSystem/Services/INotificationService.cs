using AccountingSystem.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
        Task CreateNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);
        Task MarkWorkflowActionNotificationsAsReadAsync(int workflowActionId, string userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Notification>> GetRecentNotificationsAsync(string userId, int count = 5, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Notification>> GetUserNotificationsAsync(string userId, CancellationToken cancellationToken = default);
        Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
        Task MarkAsReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default);
        Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
    }
}
