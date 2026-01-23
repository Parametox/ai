using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Components;

namespace KanbanLite.Web.Pages;

public partial class Index
{
    [Inject] private ISessionService SessionService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    protected override void OnInitialized()
    {
        if (SessionService.IsAuthenticated)
        {
            Navigation.NavigateTo("/kanban", forceLoad: true);
        }
        else
        {
            Navigation.NavigateTo("/login", forceLoad: false);
        }
    }
}
