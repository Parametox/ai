using Microsoft.AspNetCore.Components;

namespace KanbanLite.Web.Pages;

public partial class Index
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    protected override void OnInitialized()
    {
        // Przekierowanie na główny widok - autoryzacja przez [Authorize] attribute
        Navigation.NavigateTo("/kanban", forceLoad: false);
    }
}
