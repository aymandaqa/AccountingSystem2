using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AccountingSystem.Services
{
    public class UserSessionService : IUserSessionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserSessionService> _logger;

        public UserSessionService(ApplicationDbContext context, ILogger<UserSessionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserSession> CreateSessionAsync(User user, HttpContext httpContext, SessionCreationOptions? options = null, CancellationToken cancellationToken = default)
        {
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
            var deviceType = ResolveDeviceType(userAgent);
            var operatingSystem = ResolveOperatingSystem(userAgent);
            var deviceName = ResolveDeviceName(userAgent, operatingSystem);
            var browserName = !string.IsNullOrWhiteSpace(options?.BrowserName)
                ? options!.BrowserName!.Trim()
                : ResolveBrowserName(userAgent);
            var browserIcon = !string.IsNullOrWhiteSpace(options?.BrowserIcon)
                ? options!.BrowserIcon!.Trim()
                : ResolveBrowserIcon(browserName);

            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                SessionId = Guid.NewGuid().ToString("N"),
                DeviceType = deviceType,
                DeviceName = deviceName,
                OperatingSystem = operatingSystem,
                IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = userAgent,
                BrowserName = browserName,
                BrowserIcon = browserIcon,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                IsActive = true
            };

            if (options != null && options.LocationConsent && options.Latitude.HasValue && options.Longitude.HasValue)
            {
                session.Latitude = options.Latitude;
                session.Longitude = options.Longitude;
                session.LocationAccuracy = options.LocationAccuracy;
                session.LocationCapturedAt = options.LocationTimestamp;
            }

            await _context.UserSessions.AddAsync(session, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created session {SessionId} for user {UserId} from {Ip}", session.SessionId, user.Id, session.IpAddress);
            return session;
        }

        public async Task<IReadOnlyList<UserSession>> InvalidateOtherSessionsAsync(string userId, string activeSessionId, CancellationToken cancellationToken = default)
        {
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive && s.SessionId != activeSessionId)
                .ToListAsync(cancellationToken);

            if (sessions.Count == 0)
            {
                return Array.Empty<UserSession>();
            }

            foreach (var session in sessions)
            {
                session.IsActive = false;
                session.EndedAt = DateTime.UtcNow;
                session.EndedReason = "تم تسجيل الدخول من جهاز آخر";
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Invalidated {Count} other sessions for user {UserId}", sessions.Count, userId);
            return sessions;
        }

        public async Task<bool> IsSessionActiveAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
            {
                return false;
            }

            return await _context.UserSessions
                .AnyAsync(s => s.UserId == userId && s.SessionId == sessionId && s.IsActive, cancellationToken);
        }

        public async Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            var session = await _context.UserSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

            if (session == null)
            {
                return;
            }

            session.LastActivityAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserSession>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.LastActivityAt ?? s.CreatedAt)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserSession>> GetRecentSessionsAsync(string userId, int count = 10, CancellationToken cancellationToken = default)
        {
            return await _context.UserSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(count)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<UserSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        }

        public async Task<bool> InvalidateSessionAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default)
        {
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (session == null)
            {
                return false;
            }

            if (!session.IsActive)
            {
                return true;
            }

            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            session.EndedReason = reason;

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Session {SessionId} invalidated for user {UserId}", session.SessionId, session.UserId);
            return true;
        }

        public async Task<bool> InvalidateSessionByIdentifierAsync(string sessionId, string? reason = null, CancellationToken cancellationToken = default)
        {
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);
            if (session == null)
            {
                return false;
            }

            if (!session.IsActive)
            {
                return true;
            }

            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
            session.EndedReason = reason;

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Session {SessionId} invalidated by identifier", session.SessionId);
            return true;
        }

        private static string ResolveDeviceType(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "غير معروف";
            }

            if (Regex.IsMatch(userAgent, "(mobile|android|iphone|ipad|tablet)", RegexOptions.IgnoreCase))
            {
                return "جوال";
            }

            if (Regex.IsMatch(userAgent, "(smart-tv|tv)", RegexOptions.IgnoreCase))
            {
                return "شاشة";
            }

            return "كمبيوتر";
        }

        private static string ResolveOperatingSystem(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "غير معروف";
            }

            if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows";
            }

            if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase))
            {
                return "macOS";
            }

            if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            {
                return "Android";
            }

            if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            {
                return "iOS (iPhone)";
            }

            if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            {
                return "iOS (iPad)";
            }

            if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            {
                return "Linux";
            }

            return "غير معروف";
        }

        private static string ResolveBrowserName(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "غير معروف";
            }

            if (userAgent.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            {
                return "Microsoft Edge";
            }

            if (userAgent.Contains("OPR", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
            {
                return "Opera";
            }

            if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            {
                return "Mozilla Firefox";
            }

            if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("CriOS", StringComparison.OrdinalIgnoreCase))
            {
                return "Google Chrome";
            }

            if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            {
                return "Safari";
            }

            if (userAgent.Contains("MSIE", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Trident", StringComparison.OrdinalIgnoreCase))
            {
                return "Internet Explorer";
            }

            return "غير معروف";
        }

        private static string ResolveBrowserIcon(string? browserName)
        {
            return browserName switch
            {
                "Microsoft Edge" => "fa-brands fa-edge",
                "Opera" => "fa-brands fa-opera",
                "Mozilla Firefox" => "fa-brands fa-firefox-browser",
                "Google Chrome" => "fa-brands fa-chrome",
                "Safari" => "fa-solid fa-compass",
                "Internet Explorer" => "fa-brands fa-internet-explorer",
                _ => "fa-solid fa-globe"
            };
        }

        private static string ResolveDeviceName(string userAgent, string operatingSystem)
        {
            if (!string.IsNullOrWhiteSpace(operatingSystem) && operatingSystem != "غير معروف")
            {
                return operatingSystem;
            }

            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return "غير معروف";
            }

            return userAgent.Length > 60 ? userAgent[..60] + "..." : userAgent;
        }
    }
}
