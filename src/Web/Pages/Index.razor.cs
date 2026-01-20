using Microsoft.AspNetCore.Components;

namespace KanbanLite.Web.Pages;

public partial class Index
{
    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();

        try
        {
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                // Po HTTP redirect z kontrolera używamy forceLoad: true, aby wymusić pełne przeładowanie
                // i odświeżenie stanu autoryzacji w Blazor Server
                Navigation.NavigateTo("/kanban", forceLoad: true);
            }
            else
            {
                Navigation.NavigateTo("/login", forceLoad: false);
            }
        }
        catch (NavigationException)
        {
            // Musisz to rzucić dalej! 
            // Dzięki temu Blazor wie, że ma faktycznie zmienić stronę.
            throw;
        }
        catch (Exception ex)
        {
            // Tutaj łapiesz PRAWDZIWE błędy, np. błąd bazy danych
            Console.WriteLine($"Wystąpił błąd: {ex.Message}");
        }
    }
}
