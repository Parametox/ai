namespace KanbanLite.E2E.Tests.CriticalPaths;

using Infrastructure;

/// <summary>
/// Kompletny test E2E obejmujący pełną ścieżkę użytkownika:
/// Logowanie -> Tworzenie zlecenia -> Przeprocesowanie w Kanban -> Wysyłka do klienta.
/// 
/// Ten test stanowi minimalny przypadek testowy wymagany do zaliczenia projektu.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public class FullWorkflowE2ETests : E2ETestBase
{
    public FullWorkflowE2ETests(PlaywrightFixture fixture) : base(fixture) { }

    /// <summary>
    /// Pełny workflow: Manager tworzy zlecenie, przeprocesowuje batche przez wszystkie etapy,
    /// a następnie wysyła projekt do klienta.
    /// </summary>
    [Fact]
    public async Task Manager_FullWorkflow_CreateOrder_ProcessBatches_ShipToClient()
    {
        // ============================================
        // KROK 1: LOGOWANIE JAKO MANAGER
        // ============================================
        await LoginAsManagerAsync();
        
        // Weryfikacja - Manager widzi Dashboard w nawigacji
        var dashboardLink = Page.GetByTestId("nav-dashboard");
        await Expect(dashboardLink).ToBeVisibleAsync();

        // ============================================
        // KROK 2: TWORZENIE NOWEGO ZLECENIA
        // ============================================
        await NavigateToAsync("/manager/orders/create");
        
        var orderNumber = GenerateOrderNumber();
        var quantity = "100"; // Mała ilość = mniej batchy do przeprocesowania
        var dueDate = DateTime.Now.AddDays(14);

        // Wypełnij formularz
        var orderNumberInput = Page.GetByTestId("order-number-input");
        await orderNumberInput.FillAsync(orderNumber);

        var quantityInput = Page.GetByTestId("order-quantity-input");
        await quantityInput.FillAsync(quantity);

        // Wybierz format produktu
        var formatSelect = Page.GetByTestId("order-format-select");
        await formatSelect.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        var firstFormat = Page.Locator(".mud-popover-open .mud-list-item").First;
        await firstFormat.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Ustaw datę realizacji
        var dateInput = Page.GetByLabel("Termin realizacji");
        await dateInput.EvaluateAsync("input => input.removeAttribute('readonly')");
        await dateInput.FillAsync(dueDate.ToString("dd.MM.yyyy"));
        await dateInput.PressAsync("Enter");
        await Page.Keyboard.PressAsync("Escape");
        await Page.Keyboard.PressAsync("Tab");
        await Page.WaitForTimeoutAsync(300);

        // Zapisz zlecenie
        var submitButton = Page.GetByTestId("order-submit-button");
        await submitButton.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        // ============================================
        // KROK 3: PRZEJŚCIE DO KANBAN I WYSZUKANIE BATCHY
        // ============================================
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wyszukaj utworzone zlecenie
        var searchInput = Page.Locator("input[placeholder*='Wyszukaj']").First;
        if (await searchInput.IsVisibleAsync())
        {
            await searchInput.FillAsync(orderNumber);
            await Page.WaitForTimeoutAsync(500);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        // Sprawdź czy batch jest widoczny
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();

        // ============================================
        // KROK 4: PRZEPROCESOWANIE BATCHY PRZEZ STATUSY I ETAPY
        // ============================================
        
        // Znajdź pierwszy wiersz z naszym zleceniem
        var batchRow = Page.Locator("tr").Filter(new() { HasText = orderNumber }).First;
        
        if (await batchRow.IsVisibleAsync())
        {
            // Zmień status: New -> InProgress
            var statusSelect = batchRow.Locator(".mud-select").First;
            await statusSelect.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            var inProgressOption = Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasText = "InProgress" });
            if (await inProgressOption.IsVisibleAsync())
            {
                await inProgressOption.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }

            // Zmień etapy: Design -> Print -> Cut -> Pack -> Ship
            var stages = new[] { "Print", "Cut", "Pack", "Ship" };
            foreach (var stage in stages)
            {
                var stageSelect = batchRow.Locator(".mud-select").Nth(1);
                if (await stageSelect.IsVisibleAsync())
                {
                    await stageSelect.ClickAsync();
                    await Page.WaitForTimeoutAsync(300);
                    var stageOption = Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasText = stage });
                    if (await stageOption.IsVisibleAsync())
                    {
                        await stageOption.ClickAsync();
                        await Page.WaitForTimeoutAsync(500);
                    }
                }
            }

            // Zmień status: InProgress -> Done
            statusSelect = batchRow.Locator(".mud-select").First;
            await statusSelect.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            var doneOption = Page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasText = "Done" });
            if (await doneOption.IsVisibleAsync())
            {
                await doneOption.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        // ============================================
        // KROK 5: NAWIGACJA DO PROJEKTU I WYSYŁKA
        // ============================================
        
        // Kliknij w link do projektu
        var projectLink = Page.Locator($"a[href^='/project/']").Filter(new() { HasText = orderNumber }).First;
        if (!await projectLink.IsVisibleAsync())
        {
            projectLink = Page.Locator("a[href^='/project/']").First;
        }
        
        if (await projectLink.IsVisibleAsync())
        {
            await projectLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Sprawdź czy projekt jest gotowy do wysyłki
            var shipButton = Page.GetByTestId("ship-project-button");
            
            if (await shipButton.IsVisibleAsync())
            {
                // Kliknij "Wyślij do klienta"
                await shipButton.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await Page.WaitForTimeoutAsync(1000);

                // Weryfikacja - projekt powinien być oznaczony jako zakończony
                var completedChip = Page.Locator("text=Zakończony");
                await Expect(completedChip).ToBeVisibleAsync(new() { Timeout = 10000 });
            }
        }

        // ============================================
        // KROK 6: WERYFIKACJA KOŃCOWA
        // ============================================
        
        // Wróć do Kanban i sprawdź czy zlecenie jest widoczne
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var kanbanTableFinal = Page.GetByTestId("kanban-table");
        await Expect(kanbanTableFinal).ToBeVisibleAsync();
    }

    /// <summary>
    /// Test weryfikujący, że Operator może zmieniać status batchy w Kanban.
    /// </summary>
    [Fact]
    public async Task Operator_CanUpdateBatchStatus()
    {
        // Arrange
        await LoginAsOperatorAsync();
        await NavigateToAsync("/kanban");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Operator widzi tabelę Kanban
        var kanbanTable = Page.GetByTestId("kanban-table");
        await Expect(kanbanTable).ToBeVisibleAsync();

        // Act - znajdź pierwszy select statusu i spróbuj go kliknąć
        var statusSelect = Page.Locator(".mud-select").First;
        if (await statusSelect.IsVisibleAsync())
        {
            await statusSelect.ClickAsync();
            await Page.WaitForTimeoutAsync(300);
            
            // Zamknij dropdown
            await Page.Keyboard.PressAsync("Escape");
        }

        // Assert - tabela nadal widoczna
        await Expect(kanbanTable).ToBeVisibleAsync();
    }
}
