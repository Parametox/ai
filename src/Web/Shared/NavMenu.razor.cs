using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace KanbanLite.Web.Shared;

public partial class NavMenu
{
    [Inject] private IAuthService AuthService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    private async Task HandleLogout()
    {
        await AuthService.LogoutAsync();

        // Powiadom o zmianie stanu autentykacji
        if (AuthStateProvider is RevalidatingIdentityAuthenticationStateProvider<DataAccess.Identity.ApplicationUser> provider)
        {
            provider.NotifyAuthenticationStateChanged();
        }

        NavigationManager.NavigateTo("/login", forceLoad: false);
    }
}
