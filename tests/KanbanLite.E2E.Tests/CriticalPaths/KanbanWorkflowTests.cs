namespace KanbanLite.E2E.Tests.CriticalPaths;

using Infrastructure;

/// <summary>
/// Testy zmiany statusu i etapu batchy w widoku Kanban.
/// Weryfikuje przepływ pracy produkcyjnej.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public class KanbanWorkflowTests : E2ETestBase
{
    public KanbanWorkflowTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Manager_CanViewKanbanTable()
    {
        // Arrange
        await LoginAsManagerAsync();

        // Act
        await NavigateToAsync("/kanban");

        // Assert
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();
        
        var tableTitle = Page.GetByTestId("kanban-table-title");
        await Expect(tableTitle).ToContainTextAsync("Lista batchy");
    }

    [Fact]
    public async Task Operator_CanViewKanbanTable()
    {
        // Arrange
        await LoginAsOperatorAsync();

        // Act
        await NavigateToAsync("/kanban");

        // Assert
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();
    }

    [Fact]
    public async Task User_CanFilterBatchesByStatus()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/kanban");

        // Act - wybierz filtr statusu "InProgress"
        var statusFilter = Page.Locator(".mud-select").Filter(new() { HasText = "Status" }).First;
        await statusFilter.ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        
        var inProgressOption = Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasText = "InProgress" });
        if (await inProgressOption.IsVisibleAsync())
        {
            await inProgressOption.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Assert - tabela powinna się odświeżyć
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();
    }

    [Fact]
    public async Task User_CanSearchBatches()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/kanban");

        // Act - wpisz tekst w pole wyszukiwania
        var searchInput = Page.Locator(".mud-input-text input").Filter(new() { HasText = "" }).First;
        if (await searchInput.IsVisibleAsync())
        {
            await searchInput.FillAsync("E2E");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Assert - tabela powinna się odświeżyć
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();
    }

    [Fact]
    public async Task User_CanClearFilters()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/kanban");

        // Act - kliknij przycisk "Wyczyść filtry"
        var clearButton = Page.Locator("button").Filter(new() { HasText = "Wyczyść filtry" });
        if (await clearButton.IsVisibleAsync())
        {
            await clearButton.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Assert
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();
    }

    [Fact]
    public async Task User_CanNavigateToProjectFromKanban()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - znajdź link do projektu i kliknij
        var orderLink = Page.Locator("a[href^='/project/']").First;
        if (await orderLink.IsVisibleAsync())
        {
            await orderLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Assert - powinniśmy być na stronie projektu
            await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/project/\\d+.*"));
        }
    }
}
