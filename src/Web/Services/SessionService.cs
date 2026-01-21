namespace KanbanLite.Web.Services;

public sealed class SessionService : ISessionService
{
    private string? _userId;
    private string? _username;
    private List<string> _roles = new();

    public bool IsAuthenticated => !string.IsNullOrEmpty(_userId);
    
    public string? Username => _username;
    
    public string? UserId => _userId;
    
    public IReadOnlyList<string> Roles => _roles.AsReadOnly();

    public void SetSession(string userId, string username, IEnumerable<string> roles)
    {
        _userId = userId;
        _username = username;
        _roles = roles.ToList();
    }

    public void ClearSession()
    {
        _userId = null;
        _username = null;
        _roles.Clear();
    }
    
    public bool IsInRole(string roleName)
        => _roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);
}
