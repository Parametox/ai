using DataAccess.Enums;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace KanbanLite.Web.Components.Kanban;

public partial class BatchStageSelect : ComponentBase
{
    [Parameter] public long BatchId { get; set; }
    [Parameter] public ProductionStage CurrentStage { get; set; }
    [Parameter] public EventCallback<(long BatchId, ProductionStage Stage)> OnStageChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }

    private bool _saving;

    private bool IsOptionDisabled(ProductionStage stage)
    {
        // Ukryj opcje mniejsze niż aktualny etap (tylko do przodu)
        return (int)stage < (int)CurrentStage;
    }

    private string GetStageName(ProductionStage stage)
    {
        return stage switch
        {
            ProductionStage.Design => "Design (1)",
            ProductionStage.Print => "Print (2)",
            ProductionStage.Cut => "Cut (3)",
            ProductionStage.Pack => "Pack (4)",
            ProductionStage.Ship => "Ship (5)",
            _ => stage.ToString()
        };
    }

    private async Task OnSelectionChanged(ProductionStage newStage)
    {
        if (newStage == CurrentStage)
            return;

        try
        {
            _saving = true;
            StateHasChanged();

            await OnStageChanged.InvokeAsync((BatchId, newStage));
        }
        finally
        {
            _saving = false;
            StateHasChanged();
        }
    }
}
