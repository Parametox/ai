using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace KanbanLite.Web.Pages;

public partial class Login
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private bool _hasError;

    protected override void OnInitialized()
    {
        // Sprawdzenie parametru query string 'error'
        var uri = new Uri(Navigation.Uri);
        var query = QueryHelpers.ParseQuery(uri.Query);
        _hasError = query.TryGetValue("error", out var errorValues) &&
                   errorValues.Count > 0 &&
                   errorValues[0] == "true";
    }

    private void ClearError()
    {
        _hasError = false;
        StateHasChanged();
    }
}
