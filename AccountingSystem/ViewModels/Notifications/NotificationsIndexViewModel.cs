using AccountingSystem.Models;
using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels.Notifications
{
    public class NotificationsIndexViewModel
    {
        public IReadOnlyList<Notification> Notifications { get; set; } = Array.Empty<Notification>();
        public int UnreadCount { get; set; }
    }
}
