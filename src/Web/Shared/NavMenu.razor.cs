using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace KanbanLite.Web.Shared;

public partial class NavMenu
{
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISessionService SessionService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = null!;

    private async Task HandleLogout()
    {
        try
        {
            // Wywołanie Cookie Bridge API logout przez JS Interop
            await JS.InvokeVoidAsync("auth.logout");

            // Wyczyść SessionService
            SessionService.ClearSession();

            // Powiadom o zmianie stanu autentykacji
            if (AuthStateProvider is RevalidatingIdentityAuthenticationStateProvider<DataAccess.Identity.ApplicationUser> provider)
            {
                provider.NotifyAuthenticationStateChanged();
            }
        }
        catch (Exception)
        {
            // Ignoruj błędy - i tak przekierowujemy
        }

        NavigationManager.NavigateTo("/login", forceLoad: true);
    }
}
