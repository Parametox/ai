using System.Security.Claims;

namespace KanbanLite.Web.Services;

public interface ISessionService
{
    bool IsAuthenticated { get; }
    string? Username { get; }
    string? UserId { get; }
    IReadOnlyList<string> Roles { get; }

    void SetSession(string userId, string username, IEnumerable<string> roles);
    void ClearSession();
    bool IsInRole(string roleName);
    
    /// <summary>
    /// Inicjalizuje sesję z claimów ciasteczka (hydration po F5).
    /// </summary>
    void InitializeFromClaims(ClaimsPrincipal principal);
}
