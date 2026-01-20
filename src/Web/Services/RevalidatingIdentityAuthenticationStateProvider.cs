using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace KanbanLite.Web.Services;

/// <summary>
/// Authentication State Provider dla Blazor Server używający HttpContext.
/// </summary>
public sealed class RevalidatingIdentityAuthenticationStateProvider<TUser>
    : ServerAuthenticationStateProvider
    where TUser : class
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RevalidatingIdentityAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User is not null)
        {
            return Task.FromResult(new AuthenticationState(httpContext.User));
        }

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    public void NotifyAuthenticationStateChanged()
    {
        var authState = GetAuthenticationStateAsync();
        SetAuthenticationState(authState);
    }
}
