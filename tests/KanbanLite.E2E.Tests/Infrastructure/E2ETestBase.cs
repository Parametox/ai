namespace KanbanLite.E2E.Tests.Infrastructure;

/// <summary>
/// Bazowa klasa dla wszystkich testów E2E.
/// Dostarcza wspólną konfigurację i pomocnicze metody.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly PlaywrightFixture _fixture;
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected E2ETestBase(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // KAŻDY test otrzymuje nowy, czysty kontekst przeglądarki.
        // Jest to odpowiednik trybu Incognito - sesje są w pełni izolowane (brak współdzielonych cookies/storage).
        Context = await _fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            IgnoreHTTPSErrors = true
        });

        Context.SetDefaultTimeout(TestConfig.Timeouts.DefaultTimeout);
        Context.SetDefaultNavigationTimeout(TestConfig.Timeouts.NavigationTimeout);

        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Page.CloseAsync();
        await Context.CloseAsync();
    }

    /// <summary>
    /// Logowanie użytkownika przez UI.
    /// </summary>
    protected async Task LoginAsync(string username, string password)
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Znajdź pola logowania
        var usernameInput = Page.GetByTestId("login-username");
        var passwordInput = Page.GetByTestId("login-password");
        var loginButton = Page.GetByTestId("login-submit");

        await usernameInput.FillAsync(username);
        await passwordInput.FillAsync(password);
        await loginButton.ClickAsync();

        // Poczekaj na zalogowanie (przekierowanie z /login)
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions
        {
            Timeout = TestConfig.Timeouts.NavigationTimeout
        });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Logowanie jako Manager.
    /// </summary>
    protected Task LoginAsManagerAsync() => 
        LoginAsync(TestConfig.ManagerUser.Username, TestConfig.ManagerUser.Password);

    /// <summary>
    /// Logowanie jako Operator.
    /// </summary>
    protected Task LoginAsOperatorAsync() => 
        LoginAsync(TestConfig.OperatorUser.Username, TestConfig.OperatorUser.Password);

    /// <summary>
    /// Wylogowanie użytkownika.
    /// </summary>
    protected async Task LogoutAsync()
    {
        var logoutButton = Page.GetByTestId("nav-logout");
        if (await logoutButton.IsVisibleAsync())
        {
            await logoutButton.ClickAsync();
            await Page.WaitForURLAsync("**/login**");
        }
    }

    /// <summary>
    /// Nawigacja do strony z oczekiwaniem na załadowanie.
    /// </summary>
    protected async Task NavigateToAsync(string path)
    {
        await Page.GotoAsync($"{TestConfig.BaseUrl}{path}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Generuje unikalny numer zlecenia dla testów.
    /// </summary>
    protected static string GenerateOrderNumber() => 
        $"E2E-{DateTime.Now:yyyyMMdd-HHmmss}-{Random.Shared.Next(1000, 9999)}";
}
