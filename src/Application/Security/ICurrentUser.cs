namespace KanbanLite.Application.Security;

public interface ICurrentUser
{
    string? UserId { get; }
    bool IsInRole(string roleName);
}

/// <summary>
/// Implementacja <see cref="ICurrentUser"/> oparta o <see cref="System.Security.Claims.ClaimsPrincipal"/>.
/// Wymaga rejestracji <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/> w DI (dla ASP.NET Core/Blazor Server).
/// Alternatywnie: w Blazor Server można użyć <see cref="Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider"/>.
/// </summary>
public sealed class ClaimsPrincipalCurrentUser : ICurrentUser
{
    private readonly System.Security.Claims.ClaimsPrincipal? _principal;

    public ClaimsPrincipalCurrentUser(Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    {
        _principal = httpContextAccessor.HttpContext?.User;
    }

    public string? UserId => _principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                             ?? _principal?.Identity?.Name;

    public bool IsInRole(string roleName)
        => _principal?.IsInRole(roleName) ?? false;
}

