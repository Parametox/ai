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
        var orderNumberInput = Page.GetByTestId("order-number-input");
        await orderNumberInput.FillAsync(orderNumber);

        // Wypełnij ilość
        var quantityInput = Page.GetByTestId("order-quantity-input");
        await quantityInput.FillAsync(quantity);

        // Wybierz format produktu (pierwszy dostępny). MudSelect w CI może renderować opcje z opóźnieniem.
        var formatSelect = Page.GetByTestId("order-format-select");
        await formatSelect.ClickAsync();
        // Czekaj na pojawienie się listy (różne wersje MudBlazor: .mud-list-item lub role=option)
        var dropdownOption = Page.Locator(".mud-popover-open .mud-list-item, [role='option']").First;
        await dropdownOption.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await dropdownOption.ClickAsync();

        // Wybierz datę (MudDatePicker)
        // Trying GetByLabel which is more robust for MudBlazor inputs if id/for match
        var dateInput = Page.GetByLabel("Termin realizacji");

        // Remove readonly if present just in case
        await dateInput.EvaluateAsync("input => input.removeAttribute('readonly')");
        await dateInput.FillAsync(dueDate.ToString("dd.MM.yyyy"));
        await dateInput.PressAsync("Enter");
        await Page.Keyboard.PressAsync("Escape");

        // Wyślij formularz
        var submitButton = Page.GetByTestId("order-submit-button");
        await submitButton.ClickAsync();

        // Assert - Poczekaj na przekierowanie lub komunikat sukcesu
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Sprawdź czy pojawił się snackbar z sukcesem lub przekierowano na kanban
        var successIndicator = Page.Locator(".mud-snackbar-success")
            .Or(Page.Locator("text=utworzon"))
            .Or(Page.Locator("text=Kanban"));
        await Expect(successIndicator.First).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task Manager_CannotCreateOrderWithEmptyFields()
    {
        // Arrange
        await LoginAsManagerAsync();
        await NavigateToAsync("/manager/orders/create");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Act - kliknij submit bez wypełniania pól
        var submitButton = Page.GetByTestId("order-submit-button");
        await submitButton.ClickAsync();

        // Assert - Powinny pojawić się błędy walidacji
        // Check for specific validation messages
        var orderNumberError = Page.Locator("text=Numer zlecenia jest wymagany");
        var quantityError = Page.Locator("text=zakresu 1-100,000");

        // Wait for at least one error to be visible
        await Expect(orderNumberError.Or(quantityError).First).ToBeVisibleAsync(new() { Timeout = 5000 });
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
