namespace KanbanLite.E2E.Tests.AdditionalPaths;

using Infrastructure;

/// <summary>
/// Testy Dashboard Managera - metryki operacyjne, historia zleceń, formaty produktów.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public class ManagerDashboardTests : E2ETestBase
{
    public ManagerDashboardTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Manager_CanAccessDashboard()
    {
        // Arrange
        await LoginAsManagerAsync();

        // Act
        var dashboardLink = Page.GetByTestId("nav-dashboard");
        await dashboardLink.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        var dashboardTitle = Page.GetByRole(AriaRole.Heading, new() { Name = "Panel Managera" });
        await Expect(dashboardTitle).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsOperationalMetrics()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/manager/dashboard");

        // Assert - Sprawdź czy widoczne są metryki
        var statusesCard = Page.Locator("text=Statusy batchy");
        await Expect(statusesCard).ToBeVisibleAsync();

        var stagesCard = Page.Locator("text=Etapy produkcji");
        await Expect(stagesCard).ToBeVisibleAsync();

        var warningsCard = Page.Locator("text=Ostrzeżenia");
        await Expect(warningsCard).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsUrgentProjects()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/manager/dashboard");

        // Assert - Sprawdź sekcję pilnych projektów
        var urgentSection = Page.Locator("text=Pilne projekty");
        await Expect(urgentSection).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dashboard_HasOrderHistoryTab()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/manager/dashboard");

        // Act - Przejdź do zakładki Historia zleceń
        var historyTab = Page.Locator("text=Historia zleceń").First;
        await historyTab.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        var historyTitle = Page.Locator("text=Historia zleceń").Nth(1);
        await Expect(historyTitle).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Dashboard_HasProductFormatsTab()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/manager/dashboard");

        // Act - Przejdź do zakładki Formaty produktów
        var formatsTab = Page.Locator("text=Formaty produktów").First;
        await formatsTab.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        var addButton = Page.Locator("text=Dodaj nowy format");
        await Expect(addButton).ToBeVisibleAsync();
    }
}
