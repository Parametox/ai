using Microsoft.AspNetCore.Components;
using MudBlazor;
using KanbanLite.Application.Services;
using KanbanLite.Application.Security;
using KanbanLite.Web.Services;
using DataAccess.Enums;
using KanbanLite.Contracts;

namespace KanbanLite.Web.Pages;

public partial class Project
{
    [Parameter] public long Id { get; set; }

    [Inject] private IProjectService ProjectService { get; set; } = null!;
    [Inject] private ISessionService SessionService { get; set; } = null!;
    [Inject] private ICurrentUser CurrentUser { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private ProjectDetailsDto? _project = null;
    private bool _loading = true;
    private bool _shipping = false;
    private string? _error = null;
    private bool _isManager = false;

    protected override async Task OnInitializedAsync()
    {
        if (!SessionService.IsAuthenticated)
        {
            Navigation.NavigateTo("/login", replace: true);
            return;
        }
        
        await LoadProjectAsync();
        _isManager = CurrentUser.IsInRole("Manager");
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_project?.Id != Id)
        {
            await LoadProjectAsync();
        }
    }

    private async Task LoadProjectAsync()
    {
        _loading = true;
        _error = null;
        _project = null;
        StateHasChanged();

        try
        {
            var result = await ProjectService.GetByIdAsync(Id);
            
            if (result.IsSuccess)
            {
                _project = result.Value;
            }
            else
            {
                _error = result.Error?.Message ?? "Nieoczekiwany błąd podczas ładowania projektu.";
            }
        }
        catch (Exception ex)
        {
            _error = $"Błąd podczas ładowania projektu: {ex.Message}";
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task OnShipProject()
    {
        if (_project == null || _shipping)
            return;

        _shipping = true;
        StateHasChanged();

        try
        {
            var result = await ProjectService.ShipToCustomerAsync(_project.Id);
            
            if (result.IsSuccess)
            {
                Snackbar.Add("Projekt został wysłany do klienta.", Severity.Success);
                // Odśwież dane projektu
                await LoadProjectAsync();
            }
            else
            {
                var errorMessage = result.Error?.Message ?? "Nieoczekiwany błąd podczas wysyłki projektu.";
                Snackbar.Add(errorMessage, Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Błąd podczas wysyłki projektu: {ex.Message}", Severity.Error);
        }
        finally
        {
            _shipping = false;
            StateHasChanged();
        }
    }

    private Color GetStatusColor(BatchStatus status)
    {
        return status switch
        {
            BatchStatus.New => Color.Info,
            BatchStatus.InProgress => Color.Warning,
            BatchStatus.Done => Color.Success,
            _ => Color.Default
        };
    }

    private Color GetStageColor(ProductionStage stage)
    {
        return stage switch
        {
            ProductionStage.Design => Color.Secondary,
            ProductionStage.Print => Color.Primary,
            ProductionStage.Cut => Color.Warning,
            ProductionStage.Pack => Color.Info,
            ProductionStage.Ship => Color.Success,
            _ => Color.Default
        };
    }
}