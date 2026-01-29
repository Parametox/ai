using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using System.Security.Claims;

namespace KanbanLite.Web.Services;

/// <summary>
/// Authentication State Provider dla Blazor Server używający SessionService.
/// Automatycznie przywraca sesję z ciasteczka po odświeżeniu strony (F5).
/// </summary>
public sealed class RevalidatingIdentityAuthenticationStateProvider<TUser>
    : ServerAuthenticationStateProvider
    where TUser : class
{
    private readonly ISessionService _sessionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RevalidatingIdentityAuthenticationStateProvider(
        ISessionService sessionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _sessionService = sessionService;
        _httpContextAccessor = httpContextAccessor;
        
        // Próba przywrócenia sesji przy starcie komponentu (zakłada dostępność HttpContext)
        InitializeSessionFromHttpContext();
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Jeśli sesja nie jest uwierzytelniona, spróbuj przywrócić ją ponownie (dla pewności)
        if (!_sessionService.IsAuthenticated)
        {
            InitializeSessionFromHttpContext();
        }

        // Jeśli sesja jest zainicjalizowana, zwróć AuthenticationState
        if (_sessionService.IsAuthenticated)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _sessionService.UserId!),
                new Claim(ClaimTypes.Name, _sessionService.Username!)
            };

            // Dodaj role
            foreach (var role in _sessionService.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, "SessionAuth");
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(principal));
        }

        // Brak sesji - zwróć pusty stan
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private void InitializeSessionFromHttpContext()
    {
        try 
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                _sessionService.InitializeFromClaims(user);
            }
        }
        catch
        {
            // Ignorujemy błędy dostępu do HttpContext (może być niedostępny w niektórych fazach Blazor)
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        var authState = GetAuthenticationStateAsync();
        SetAuthenticationState(authState);
    }
}
