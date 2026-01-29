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
