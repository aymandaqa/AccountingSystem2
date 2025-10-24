using System;

namespace AccountingSystem.Services
{
    public class SessionCreationOptions
    {
        public bool LocationConsent { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? LocationAccuracy { get; set; }
        public DateTimeOffset? LocationTimestamp { get; set; }
        public string? BrowserName { get; set; }
        public string? BrowserIcon { get; set; }
    }
}
