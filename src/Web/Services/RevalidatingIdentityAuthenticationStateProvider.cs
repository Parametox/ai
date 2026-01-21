using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using System.Security.Claims;

namespace KanbanLite.Web.Services;

/// <summary>
/// Authentication State Provider dla Blazor Server używający SessionService.
/// </summary>
public sealed class RevalidatingIdentityAuthenticationStateProvider<TUser>
    : ServerAuthenticationStateProvider
    where TUser : class
{
    private readonly ISessionService _sessionService;

    public RevalidatingIdentityAuthenticationStateProvider(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
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

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    public void NotifyAuthenticationStateChanged()
    {
        var authState = GetAuthenticationStateAsync();
        SetAuthenticationState(authState);
    }
}
