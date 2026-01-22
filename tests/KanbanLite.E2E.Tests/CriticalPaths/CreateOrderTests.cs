namespace KanbanLite.E2E.Tests.CriticalPaths;

using Infrastructure;

/// <summary>
/// Testy tworzenia zleceń przez Managera.
/// Weryfikuje poprawność formularza i procesu tworzenia zlecenia.
/// </summary>
[Collection(nameof(PlaywrightCollection))]
public class CreateOrderTests : E2ETestBase
{
    public CreateOrderTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Manager_CanCreateNewOrder()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/manager/orders/create");

        var orderNumber = GenerateOrderNumber();
        var quantity = "500";
        var dueDate = DateTime.Now.AddDays(14);

        // Act
        // Wypełnij numer zlecenia
        var orderNumberInput = Page.GetByTestId("order-number-input").Locator("input");
        await orderNumberInput.FillAsync(orderNumber);

        // Wypełnij ilość
        var quantityInput = Page.GetByTestId("order-quantity-input").Locator("input");
        await quantityInput.FillAsync(quantity);

        // Wybierz format produktu (pierwszy dostępny)
        var formatSelect = Page.GetByTestId("order-format-select");
        await formatSelect.ClickAsync();
        await Page.WaitForTimeoutAsync(300); // Poczekaj na otwarcie dropdown
        var firstOption = Page.Locator(".mud-popover-open .mud-list-item").First;
        await firstOption.ClickAsync();

        // Wybierz datę (MudDatePicker)
        var datePicker = Page.GetByTestId("order-duedate-picker");
        await datePicker.ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        
        // Kliknij w picker i wybierz datę przez wpisanie
        var dateInput = datePicker.Locator("input");
        await dateInput.FillAsync(dueDate.ToString("dd.MM.yyyy"));
        await Page.Keyboard.PressAsync("Escape"); // Zamknij picker

        // Wyślij formularz
        var submitButton = Page.GetByTestId("order-submit-button");
        await submitButton.ClickAsync();

        // Assert - Poczekaj na przekierowanie lub komunikat sukcesu
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        // Sprawdź czy pojawił się snackbar z sukcesem lub przekierowano na kanban
        var successIndicator = Page.Locator(".mud-snackbar-success, text=utworzon, text=Kanban");
        await Expect(successIndicator.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Manager_CannotCreateOrderWithEmptyFields()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/manager/orders/create");

        // Act - kliknij submit bez wypełniania pól
        var submitButton = Page.GetByTestId("order-submit-button");
        await submitButton.ClickAsync();

        // Assert - Powinny pojawić się błędy walidacji
        await Page.WaitForTimeoutAsync(500);
        var validationErrors = Page.Locator(".mud-input-error, .validation-message, text=wymagany");
        var errorCount = await validationErrors.CountAsync();
        
        Assert.True(errorCount > 0, "Powinny pojawić się błędy walidacji dla pustych pól");
    }

    [Fact]
    public async Task Manager_SeesCreateOrderNavLink()
    {
        // Arrange
        await LoginAsManagerAsync();

        // Act & Assert
        var createOrderLink = Page.GetByTestId("nav-create-order");
        await Expect(createOrderLink).ToBeVisibleAsync();
        
        // Kliknij i sprawdź nawigację
        await createOrderLink.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var createOrderForm = Page.GetByTestId("create-order-form");
        await Expect(createOrderForm).ToBeVisibleAsync();
    }
}
