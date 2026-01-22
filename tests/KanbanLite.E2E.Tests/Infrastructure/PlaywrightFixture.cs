namespace KanbanLite.E2E.Tests.Infrastructure;

/// <summary>
/// Fixture zarządzający instancją Playwright dla całego zestawu testów.
/// Playwright jest uruchamiany raz i współdzielony między testami.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Instalacja przeglądarek jeśli potrzeba (w CI)
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new Exception($"Playwright install failed with exit code {exitCode}");
        }

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = TestConfig.Headless,
            SlowMo = TestConfig.SlowMo
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }
}
