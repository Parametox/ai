using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;
using System.Text.Json;

namespace KanbanLite.Web.Pages;

public partial class Login
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;
    [Inject] private ISessionService SessionService { get; set; } = null!;

    private bool _hasError;
    private bool _isLoading;
    private LoginModel _loginModel = new();

    protected override void OnInitialized()
    {
        // Sprawdzenie parametru query string 'error'
        var uri = new Uri(Navigation.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);
        _hasError = query.TryGetValue("error", out var errorValues) &&
                   errorValues.Count > 0 &&
                   errorValues[0] == "true";
    }

    private async Task HandleLogin()
    {
        _isLoading = true;
        _hasError = false;
        StateHasChanged();

        try
        {
            // Wywołanie Cookie Bridge API przez JS Interop
            var result = await JS.InvokeAsync<JsLoginResult>("auth.login", _loginModel.Username, _loginModel.Password);

            if (result.Success && result.Data != null)
            {
                // Deserializuj odpowiedź i ustaw SessionService
                SessionService.SetSession(result.Data.UserId, result.Data.Username, result.Data.Roles);

                // forceLoad: true wymusza przeładowanie strony i poprawne wczytanie ciasteczka
                Navigation.NavigateTo("/kanban", forceLoad: true);
            }
            else
            {
                _hasError = true;
            }
        }
        catch (Exception)
        {
            _hasError = true;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void ClearError()
    {
        _hasError = false;
        StateHasChanged();
    }

    private class LoginModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private class JsLoginResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public LoginResponse? Data { get; set; }
    }

    private record LoginResponse(string UserId, string Username, List<string> Roles);
}
