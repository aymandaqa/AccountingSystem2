using AccountingSystem.Models;
using Microsoft.AspNetCore.Http;

namespace AccountingSystem.Services
{
    public interface IUserSessionService
    {
        Task<UserSession> CreateSessionAsync(User user, HttpContext httpContext, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSession>> InvalidateOtherSessionsAsync(string userId, string activeSessionId, CancellationToken cancellationToken = default);
        Task<bool> IsSessionActiveAsync(string userId, string sessionId, CancellationToken cancellationToken = default);
        Task UpdateSessionActivityAsync(string sessionId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSession>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserSession>> GetRecentSessionsAsync(string userId, int count = 10, CancellationToken cancellationToken = default);
        Task<UserSession?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> InvalidateSessionAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
        Task<bool> InvalidateSessionByIdentifierAsync(string sessionId, string? reason = null, CancellationToken cancellationToken = default);
    }
}
