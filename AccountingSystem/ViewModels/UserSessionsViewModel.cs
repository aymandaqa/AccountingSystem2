using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class UserSessionsViewModel
    {
        public List<UserSessionItemViewModel> ActiveSessions { get; set; } = new();
        public List<UserSessionItemViewModel> RecentSessions { get; set; } = new();
        public string? CurrentSessionId { get; set; }
    }

    public class UserSessionItemViewModel
    {
        public Guid Id { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string? DeviceType { get; set; }
        public string? DeviceName { get; set; }
        public string? OperatingSystem { get; set; }
        public string? IpAddress { get; set; }
        public string? BrowserName { get; set; }
        public string? BrowserIcon { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? LocationAccuracy { get; set; }
        public DateTimeOffset? LocationCapturedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
