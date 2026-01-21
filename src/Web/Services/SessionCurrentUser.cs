using KanbanLite.Application.Security;

namespace KanbanLite.Web.Services;

/// <summary>
/// Implementacja ICurrentUser oparta na SessionService.
/// </summary>
public sealed class SessionCurrentUser : ICurrentUser
{
    private readonly ISessionService _sessionService;

    public SessionCurrentUser(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public string? UserId => _sessionService.UserId;

    public bool IsInRole(string roleName)
        => _sessionService.IsInRole(roleName);
}
