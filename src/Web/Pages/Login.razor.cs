using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using KanbanLite.Web.Services;

namespace KanbanLite.Web.Pages;

public partial class Login
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    [Inject] private IAuthService AuthService { get; set; } = null!;

    private bool _hasError;
    private LoginModel _loginModel = new();
    private bool _isLoading;

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
        if (string.IsNullOrWhiteSpace(_loginModel.Username) || string.IsNullOrWhiteSpace(_loginModel.Password))
        {
            _hasError = true;
            StateHasChanged();
            return;
        }

        _isLoading = true;
        _hasError = false;
        StateHasChanged();

        var redirectUrl = await AuthService.LoginAsync(_loginModel.Username, _loginModel.Password);

        _isLoading = false;

        if (!string.IsNullOrEmpty(redirectUrl))
        {
            Navigation.NavigateTo(redirectUrl);
        }
        else
        {
            _hasError = true;
        }
        StateHasChanged();
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
