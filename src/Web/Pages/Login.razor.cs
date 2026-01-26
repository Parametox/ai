using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace KanbanLite.Web.Pages;

public partial class Login
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IAuthService AuthService { get; set; } = null!;

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
            var result = await AuthService.LoginAsync(_loginModel.Username, _loginModel.Password);

            if (!string.IsNullOrEmpty(result))
            {
                // Przekierowanie na kanban
                Navigation.NavigateTo(result, forceLoad: false);
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
}
