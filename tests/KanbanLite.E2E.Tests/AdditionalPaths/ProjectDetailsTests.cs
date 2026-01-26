namespace KanbanLite.E2E.Tests.AdditionalPaths;

using Infrastructure;

/// <summary>
/// Testy strony szczegółów projektu.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public class ProjectDetailsTests : E2ETestBase
{
    public ProjectDetailsTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Manager_CanViewProjectDetails()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - Kliknij pierwszy link do projektu
        var projectLink = Page.Locator("a[href^='/project/']").First;

        if (await projectLink.IsVisibleAsync())
        {
            await projectLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Sprawdź elementy strony projektu
            var projectTitle = Page.Locator("text=Projekt");
            await Expect(projectTitle).ToBeVisibleAsync();

            var orderDetails = Page.Locator("text=Szczegóły zlecenia");
            await Expect(orderDetails).ToBeVisibleAsync();

            var shipmentStatus = Page.Locator("text=Status wysyłki");
            await Expect(shipmentStatus).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task ProjectDetails_ShowsBatchesTable()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act
        var projectLink = Page.Locator("a[href^='/project/']").First;

        if (await projectLink.IsVisibleAsync())
        {
            await projectLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Sprawdź czy jest tabela batchy
            var batchesSection = Page.Locator("text=Batche projektu, text=Batch");
            await Expect(batchesSection.First).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task Operator_CanViewProjectDetails()
    {
        // Arrange
        await LoginAsOperatorAsync();
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act
        var projectLink = Page.Locator("a[href^='/project/']").First;

        if (await projectLink.IsVisibleAsync())
        {
            await projectLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Operator widzi szczegóły projektu
            var projectTitle = Page.Locator("text=Projekt");
            await Expect(projectTitle).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task Operator_CannotSeeShipButton()
    {
        // Arrange
        await LoginAsOperatorAsync();
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act
        var projectLink = Page.Locator("a[href^='/project/']").First;

        if (await projectLink.IsVisibleAsync())
        {
            await projectLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - Operator NIE widzi przycisku wysyłki
            var shipButton = Page.GetByTestId("ship-project-button");
            await Expect(shipButton).ToBeHiddenAsync();
        }
    }
}
