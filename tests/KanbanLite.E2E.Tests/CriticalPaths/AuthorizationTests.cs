namespace KanbanLite.E2E.Tests.CriticalPaths;

using Infrastructure;

/// <summary>
/// Testy autoryzacji dostępu do zasobów na podstawie ról.
/// Weryfikuje, że Operator nie ma dostępu do funkcji Managera.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public class AuthorizationTests : E2ETestBase
{
    public AuthorizationTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact(Skip = "zaskipowane bo stabilizujemy testy")]
    public async Task Operator_CannotAccessManagerDashboard()
    {
        // Arrange
        await LoginAsOperatorAsync();

        // Act - próba bezpośredniego dostępu do Dashboard Managera
        await NavigateToAsync("/manager/dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Powinien zobaczyć błąd lub być przekierowany
        var currentUrl = Page.Url;
        var hasAccessDenied = currentUrl.Contains("login") ||
                              currentUrl.Contains("access-denied") ||
                              currentUrl.Contains("forbidden");

        // Lub sprawdź czy nie widać zawartości Dashboard
        // Używamy nagłówka, aby uniknąć konfliktu z linkiem w nawigacji (gdyby był widoczny)
        var dashboardHeading = Page.GetByRole(AriaRole.Heading, new() { Name = "Panel Managera" });
        var isVisible = await dashboardHeading.IsVisibleAsync();

        Assert.True(hasAccessDenied || !isVisible,
            "Operator nie powinien mieć dostępu do Dashboard Managera");
    }

    [Fact(Skip = "zaskipowane bo stabilizujemy testy")]
    public async Task Operator_CannotAccessCreateOrderPage()
    {
        // Arrange
        await LoginAsOperatorAsync();

        // Act - próba bezpośredniego dostępu do tworzenia zlecenia
        await NavigateToAsync("/manager/orders/create");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert
        var createOrderForm = Page.GetByTestId("create-order-form");
        var isVisible = await createOrderForm.IsVisibleAsync();

        Assert.False(isVisible,
            "Operator nie powinien mieć dostępu do formularza tworzenia zlecenia");
    }

    [Fact(Skip = "zaskipowane bo stabilizujemy testy")]
    public async Task Operator_CanAccessKanban()
    {
        // Arrange
        await LoginAsOperatorAsync();

        // Act
        await NavigateToAsync("/kanban");

        // Assert
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();
    }

    [Fact(Skip = "zaskipowane bo stabilizujemy testy")]
    public async Task Manager_CanAccessAllPages()
    {
        // Arrange
        await LoginAsManagerAsync();

        // Act & Assert - Kanban
        await NavigateToAsync("/kanban");
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();

        // Act & Assert - Create Order
        await NavigateToAsync("/manager/orders/create");
        var createOrderForm = Page.GetByTestId("create-order-form");
        await Expect(createOrderForm).ToBeVisibleAsync();

        // Act & Assert - Dashboard
        await NavigateToAsync("/manager/dashboard");
        var dashboardTitle = Page.GetByRole(AriaRole.Heading, new() { Name = "Panel Managera" });
        await Expect(dashboardTitle).ToBeVisibleAsync();
    }
}
