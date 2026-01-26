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
    protected Supabase.Client Supabase { get; private set; } = null!;

    protected E2ETestBase(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Inicjalizacja Supabase Client
        var url = TestConfig.Supabase.Url;
        var key = TestConfig.Supabase.Key;
        var options = new Supabase.SupabaseOptions { AutoConnectRealtime = true };
        Supabase = new Supabase.Client(url, key, options);
        await Supabase.InitializeAsync();

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

        // Zmiana strategii oczekiwania dla Blazor Server:
        // Oczekujemy na URL (sukces) LUB komunikat błędu (porażka logowania).
        var navigationTask = Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = TestConfig.Timeouts.NavigationTimeout
        });

        var errorTask = Page.Locator(".alert-error").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = TestConfig.Timeouts.NavigationTimeout
        });

        var completedTask = await Task.WhenAny(navigationTask, errorTask);

        if (completedTask == errorTask && errorTask.IsCompletedSuccessfully)
        {
            var errorText = await Page.Locator(".alert-error").InnerTextAsync();
            throw new Exception($"Login failed with UI error: {errorText}. Credentials used: Username='{username}', Password='{password}' \nException:{errorTask.Exception}");
        }

        // Jeśli nawigacja wygrała lub nastąpił timeout błędów -> czekaj na nawigację
        try
        {
            await navigationTask;
        }
        catch (TimeoutException)
        {
            // Ostatnie sprawdzenie błędu przed rzuceniem timeoutu
            if (await Page.Locator(".alert-error").IsVisibleAsync())
            {
                var errorText = await Page.Locator(".alert-error").InnerTextAsync();
                throw new Exception($"Login failed: {errorText}. Credentials used: Username='{username}', Password='{password}'");
            }
            throw new Exception($"Login Timeout. Current URL: {Page.Url}. Ensure the database is seeded with test users. Credentials used: Username='{username}', Password='{password}'");
        }

        // Pewniejszy sygnał sukcesu: czekaj na element dostępny tylko po zalogowaniu (Wyloguj)
        await Page.GetByTestId("nav-logout").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = TestConfig.Timeouts.NavigationTimeout
        });
    }

    /// <summary>
    /// Logowanie jako Manager.
    /// </summary>
    protected Task LoginAsManagerAsync() =>
        LoginAsync(TestConfig.ManagerUser.Username, TestConfig.ManagerUser.Password);

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
