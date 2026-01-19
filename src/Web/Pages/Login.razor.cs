using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using MudBlazor;
using KanbanLite.Web.Services;
using DataAccess.Identity;

namespace KanbanLite.Web.Pages;

public partial class Login
{
    [Inject] private IAuthService AuthService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private SignInManager<ApplicationUser> SignInManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private LoginModel _model = new();
    private string? _errorMessage;
    private bool _loading;

    private async Task HandleLogin()
    {
        _loading = true;
        _errorMessage = null;
        // Nie wywołujemy StateHasChanged() przed logowaniem - pozwalamy na zakończenie połączenia SignalR

        try
        {
            // Weryfikacja logowania (bez SignInAsync)
            var result = await AuthService.LoginAsync(_model.Username, _model.Password, isPersistent: false);
            
            if (result.IsSuccess)
            {
                // Opóźnienie do następnego ticka event loop - po zakończeniu połączenia SignalR
                await Task.Delay(1000);
                
                // Logowanie przez SignInManager - wywoływane po zakończeniu połączenia SignalR
                //await SignInManager.SignInAsync(result.Value.User, isPersistent: false);
                
                // Przekierowanie na returnUrl lub domyślnie na /kanban
                // Blazor Server automatycznie odświeży stan autoryzacji po przekierowaniu
                var uri = new Uri(Navigation.Uri);
                var returnUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query)
                    .TryGetValue("returnUrl", out var returnUrlValues) && returnUrlValues.Count > 0
                    ? returnUrlValues[0] ?? "/kanban"
                    : "/kanban";
                
                // Unikaj przekierowania na /login lub / (które przekierowuje na /login)
                if (returnUrl == "/login" || returnUrl == "/")
                {
                    returnUrl = "/kanban";
                }
                
                Navigation.NavigateTo(returnUrl, replace: true);
                Snackbar.Add("Zalogowano pomyślnie", Severity.Success);
            }
            else
            {
                _errorMessage = result.Error?.Message ?? "Nieprawidłowa nazwa użytkownika lub hasło";
            }
        }
        catch (Exception)
        {
            _errorMessage = "Wystąpił błąd podczas logowania. Spróbuj ponownie.";
            // Logowanie błędu (w produkcji użyj ILogger)
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private void ClearError()
    {
        _errorMessage = null;
        StateHasChanged();
    }

    private sealed class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
