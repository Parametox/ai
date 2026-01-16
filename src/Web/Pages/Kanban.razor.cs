using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Services;
using KanbanLite.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace KanbanLite.Web.Pages;

public partial class Kanban : ComponentBase, IDisposable
{
    [Inject] private IBatchService BatchService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private ILogger<Kanban> Logger { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private KanbanQuery _query = new();
    private KanbanBatchesResult? _batches;
    private int _inProgressCount;
    private bool _loading;
    private Timer? _refreshTimer;
    private CancellationTokenSource? _searchCts;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
        
        // Timer odświeżania licznika co 30 sekund
        _refreshTimer = new Timer(async _ => await RefreshInProgressCount(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _loading = true;
            StateHasChanged();

            var result = await BatchService.GetKanbanAsync(_query);
            
            if (result.IsSuccess)
            {
                _batches = result.Value;
                _inProgressCount = result.Value?.InProgressCount ?? 0;
                
                // Wyświetlenie ostrzeżeń jeśli są
                if (result.Value?.Warnings.Any() == true)
                {
                    foreach (var warning in result.Value.Warnings)
                    {
                        Snackbar.Add(warning.Message, Severity.Warning);
                    }
                }
            }
            else
            {
                HandleError(result.Error!);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas ładowania danych Kanban");
            HandleError(AppError.Unexpected("Wystąpił nieoczekiwany błąd podczas ładowania danych."));
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task RefreshInProgressCount()
    {
        try
        {
            var result = await BatchService.GetInProgressCountAsync();
            if (result.IsSuccess)
            {
                _inProgressCount = result.Value;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas odświeżania licznika InProgress");
        }
    }

    private async Task OnStatusFilterChanged(BatchStatus? status)
    {
        _query = _query with { Status = status, Page = 1 };
        await LoadDataAsync();
    }

    private async Task OnStageFilterChanged(ProductionStage? stage)
    {
        _query = _query with { Stage = stage, Page = 1 };
        await LoadDataAsync();
    }

    private async Task OnSearchTextChanged(string? searchText)
    {
        // Anulowanie poprzedniego zapytania
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();

        try
        {
            // Debouncing 300ms
            await Task.Delay(300, _searchCts.Token);
            
            _query = _query with { Q = string.IsNullOrWhiteSpace(searchText) ? null : searchText.Trim(), Page = 1 };
            await LoadDataAsync();
        }
        catch (OperationCanceledException)
        {
            // Anulowanie jest oczekiwane podczas debouncingu
        }
    }

    private void OnClearFilters()
    {
        _query = new KanbanQuery();
        _ = LoadDataAsync();
    }

    private async Task OnStatusChanged((long BatchId, BatchStatus Status) args)
    {
        try
        {
            _loading = true;
            StateHasChanged();

            var request = new UpdateBatchStatusRequest(args.Status);
            var result = await BatchService.UpdateStatusAsync(args.BatchId, request);

            if (result.IsSuccess)
            {
                _inProgressCount = result.Value!.InProgressCount;
                
                // Wyświetlenie ostrzeżeń jeśli są
                if (result.Value.Warnings.Any())
                {
                    foreach (var warning in result.Value.Warnings)
                    {
                        Snackbar.Add(warning.Message, Severity.Warning);
                    }
                }
                else
                {
                    Snackbar.Add("Status batcha został zaktualizowany", Severity.Success);
                }

                // Odświeżenie tabeli
                await LoadDataAsync();
            }
            else
            {
                HandleError(result.Error!);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas zmiany statusu batcha {BatchId}", args.BatchId);
            HandleError(AppError.Unexpected("Wystąpił nieoczekiwany błąd podczas zmiany statusu."));
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task OnStageChanged((long BatchId, ProductionStage Stage) args)
    {
        try
        {
            _loading = true;
            StateHasChanged();

            var request = new UpdateBatchStageRequest(args.Stage);
            var result = await BatchService.UpdateStageAsync(args.BatchId, request);

            if (result.IsSuccess)
            {
                Snackbar.Add("Etap batcha został zaktualizowany", Severity.Success);
                
                // Odświeżenie tabeli
                await LoadDataAsync();
            }
            else
            {
                HandleError(result.Error!);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Błąd podczas zmiany etapu batcha {BatchId}", args.BatchId);
            HandleError(AppError.Unexpected("Wystąpił nieoczekiwany błąd podczas zmiany etapu."));
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private void OnQueryChanged(KanbanQuery newQuery)
    {
        _query = newQuery;
        _ = LoadDataAsync();
    }

    private void HandleError(AppError error)
    {
        var severity = error.Code switch
        {
            "ValidationFailed" => Severity.Error,
            "NotFound" => Severity.Error,
            "Conflict" => Severity.Error,
            "Unauthorized" => Severity.Warning,
            "Forbidden" => Severity.Warning,
            _ => Severity.Error
        };

        Snackbar.Add(error.Message, severity);

        var logLevel = error.Code switch
        {
            "ValidationFailed" => LogLevel.Warning,
            "NotFound" => LogLevel.Warning,
            "Conflict" => LogLevel.Warning,
            "Unauthorized" => LogLevel.Warning,
            "Forbidden" => LogLevel.Warning,
            _ => LogLevel.Error
        };

        Logger.Log(logLevel, "Błąd w Kanban: {Code} - {Message}", error.Code, error.Message);
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
