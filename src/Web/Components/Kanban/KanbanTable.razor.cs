using DataAccess.Enums;
using KanbanLite.Contracts;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace KanbanLite.Web.Components.Kanban;

public partial class KanbanTable : ComponentBase
{
    [Parameter] public KanbanBatchesResult? Batches { get; set; }
    [Parameter] public KanbanQuery Query { get; set; } = new();
    [Parameter] public int InProgressCount { get; set; }
    [Parameter] public EventCallback<(long BatchId, BatchStatus Status)> OnStatusChanged { get; set; }
    [Parameter] public EventCallback<(long BatchId, ProductionStage Stage)> OnStageChanged { get; set; }
    [Parameter] public EventCallback<KanbanQuery> OnQueryChanged { get; set; }

    private async Task OnPageChanged(int page)
    {
        var newQuery = Query with { Page = page };
        await OnQueryChanged.InvokeAsync(newQuery);
    }

    private async Task OnPageSizeChanged(int pageSize)
    {
        var newQuery = Query with { PageSize = pageSize, Page = 1 };
        await OnQueryChanged.InvokeAsync(newQuery);
    }
}
