using DataAccess.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace KanbanLite.Web.Components.Kanban;

public partial class BatchStatusSelect : ComponentBase
{
    [Parameter] public long BatchId { get; set; }
    [Parameter] public BatchStatus CurrentStatus { get; set; }
    [Parameter] public EventCallback<long, BatchStatus> OnStatusChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }

    private bool _saving;

    private bool IsOptionDisabled(BatchStatus status)
    {
        // Gdy status to Done, ukryj New i InProgress
        if (CurrentStatus == BatchStatus.Done)
        {
            return status != BatchStatus.Done;
        }
        
        // Gdy status to InProgress, ukryj New
        if (CurrentStatus == BatchStatus.InProgress)
        {
            return status == BatchStatus.New;
        }
        
        // Gdy status to New, wszystkie opcje dostępne (walidacja serwisu blokuje nieprawidłowe przejścia)
        return false;
    }

    private async Task OnSelectionChanged(BatchStatus newStatus)
    {
        if (newStatus == CurrentStatus)
            return;

        try
        {
            _saving = true;
            StateHasChanged();

            await OnStatusChanged.InvokeAsync(BatchId, newStatus);
        }
        finally
        {
            _saving = false;
            StateHasChanged();
        }
    }
}
