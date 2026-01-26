namespace KanbanLite.E2E.Tests.CriticalPaths;

using Infrastructure;

/// <summary>
/// Testy logowania i wylogowania użytkowników.
/// Weryfikuje mechanizm kontroli dostępu dla ról Manager i Operator.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public class AuthenticationTests : E2ETestBase
{
    public AuthenticationTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Manager_CanLoginSuccessfully()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act
        await LoginAsManagerAsync();

        // Assert - Manager powinien zobaczyć nawigację z Dashboard
        var dashboardLink = Page.GetByTestId("nav-dashboard");
        await Expect(dashboardLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Operator_CanLoginSuccessfully()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act
        await LoginAsOperatorAsync();

        // Assert - Operator powinien zobaczyć Kanban ale NIE Dashboard
        var kanbanLink = Page.GetByTestId("nav-kanban");
        await Expect(kanbanLink).ToBeVisibleAsync();

        var dashboardLink = Page.GetByTestId("nav-dashboard");
        await Expect(dashboardLink).ToBeHiddenAsync();
    }

    [Fact]
    public async Task InvalidCredentials_ShowsError()
    {
        // Arrange
        await NavigateToAsync("/login");

        // Act
        var usernameInput = Page.GetByTestId("login-username");
        var passwordInput = Page.GetByTestId("login-password");
        var loginButton = Page.GetByTestId("login-submit");

        await usernameInput.FillAsync("invalid_user");
        await passwordInput.FillAsync("invalid_password");
        await loginButton.ClickAsync();

        // Assert - Powinien pozostać na stronie logowania z błędem
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*login.*"));

        // Sprawdź czy jest komunikat o błędzie
        var errorMessage = Page.Locator(".alert-error, .mud-alert-error, [class*='error']");
        await Expect(errorMessage).ToBeVisibleAsync();
    }

    [Fact]
    public async Task User_CanLogout()
    {
        // Arrange
        await LoginAsManagerAsync();

        // Act
        await LogoutAsync();

        // Assert - Powinien być przekierowany na stronę logowania
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*login.*"));
    }

    [Fact]
    public async Task UnauthenticatedUser_IsRedirectedToLogin()
    {
        // Act - wyloguj + próba dostępu do chronionej strony
        await LogoutAsync();
        await NavigateToAsync("/kanban");

        // Assert - Powinien być przekierowany na login
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*login.*"));
    }
}
