using Microsoft.AspNetCore.Components;
using MudBlazor;
using KanbanLite.Contracts;
using KanbanLite.Application.Services;
using KanbanLite.Web.Services;
using KanbanLite.Web.Components;

namespace KanbanLite.Web.Pages.Manager;

public partial class Dashboard
{
    [Inject] private ISessionService SessionService { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    
    private DashboardDto? dashboardData;
    private bool loadingDashboard = true;
    private bool loadingOrders = false;
    private bool loadingFormats = false;
    private bool loadingRules = false;
    private bool loadingProjects = false;
    
    private MudTable<OrderListItemDto>? ordersTable;
    private MudTable<ProductFormatDto>? formatsTable;
    private MudTable<BatchSplitRuleDto>? rulesTable;
    private MudTable<ProjectListItemDto>? projectsTable;
    
    // Filtry historii zleceń
    private string orderSearchQuery = string.Empty;
    private DateTime? orderDueFrom;
    private DateTime? orderDueTo;
    
    // Filtry formatów produktów
    private bool showInactiveFormats = false;
    
    // Filtry projektów
    private bool? projectFilterCompleted = null;

    protected override async Task OnInitializedAsync()
    {
        if (!SessionService.IsAuthenticated)
        {
            Navigation.NavigateTo("/login", replace: true);
            return;
        }
        
        if (!SessionService.IsInRole("Manager"))
        {
            Navigation.NavigateTo("/kanban", replace: true);
            return;
        }
        
        await LoadDashboardData();
    }

    private async Task LoadDashboardData()
    {
        try
        {
            loadingDashboard = true;
            var result = await DashboardService.GetAsync();
            
            if (result.IsSuccess)
            {
                dashboardData = result.Value;
            }
            else
            {
                Snackbar.Add($"Błąd podczas ładowania danych dashboardu: {result.Error.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
        }
        finally
        {
            loadingDashboard = false;
        }
    }

    private async Task<TableData<OrderListItemDto>> LoadOrdersData(TableState state, CancellationToken cancellationToken = default)
    {
        try
        {
            loadingOrders = true;
            
            var query = new OrderQuery
            {
                Q = string.IsNullOrWhiteSpace(orderSearchQuery) ? null : orderSearchQuery.Trim(),
                DueFrom = orderDueFrom.ToDateOnly(),
                DueTo = orderDueTo.ToDateOnly(),
                Page = state.Page + 1, // MudTable używa 0-based indexing
                PageSize = state.PageSize
            };

            var result = await OrderService.GetAsync(query);
            
            if (result.IsSuccess)
            {
                return new TableData<OrderListItemDto>
                {
                    TotalItems = (int)result.Value.Total,
                    Items = result.Value.Items
                };
            }
            else
            {
                Snackbar.Add($"Błąd podczas ładowania zleceń: {result.Error.Message}", Severity.Error);
                return new TableData<OrderListItemDto> { TotalItems = 0, Items = Array.Empty<OrderListItemDto>() };
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
            return new TableData<OrderListItemDto> { TotalItems = 0, Items = Array.Empty<OrderListItemDto>() };
        }
        finally
        {
            loadingOrders = false;
        }
    }

    private async Task LoadOrders()
    {
        if (ordersTable != null)
        {
            await ordersTable.ReloadServerData();
        }
    }

    private async Task ClearOrderFilters()
    {
        orderSearchQuery = string.Empty;
        orderDueFrom = null;
        orderDueTo = null;
        await LoadOrders();
    }

    // Product Formats CRUD Methods
    private async Task<TableData<ProductFormatDto>> LoadFormatsData(TableState state, CancellationToken cancellationToken = default)
    {
        try
        {
            loadingFormats = true;
            
            var query = new ProductFormatQuery
            {
                IsActive = showInactiveFormats ? null : true,
                Page = state.Page + 1,
                PageSize = state.PageSize
            };

            var result = await ProductFormatService.GetAsync(query);
            
            if (result.IsSuccess)
            {
                return new TableData<ProductFormatDto>
                {
                    TotalItems = (int)result.Value.Total,
                    Items = result.Value.Items
                };
            }
            else
            {
                Snackbar.Add($"Błąd podczas ładowania formatów: {result.Error.Message}", Severity.Error);
                return new TableData<ProductFormatDto> { TotalItems = 0, Items = Array.Empty<ProductFormatDto>() };
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
            return new TableData<ProductFormatDto> { TotalItems = 0, Items = Array.Empty<ProductFormatDto>() };
        }
        finally
        {
            loadingFormats = false;
        }
    }

    private async Task LoadFormats()
    {
        if (formatsTable != null)
        {
            await formatsTable.ReloadServerData();
        }
    }

    private async Task OpenCreateProductFormatDialog()
    {
        var parameters = new DialogParameters();
        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        
        var dialog = await DialogService.ShowAsync<ProductFormatDialog>("Dodaj nowy format produktu", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            await LoadFormats();
        }
    }

    private async Task OpenEditProductFormatDialog(ProductFormatDto format)
    {
        var parameters = new DialogParameters 
        { 
            ["ProductFormat"] = format,
            ["IsEdit"] = true
        };
        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        
        var dialog = await DialogService.ShowAsync<ProductFormatDialog>("Edytuj format produktu", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            await LoadFormats();
        }
    }

    private async Task DeactivateProductFormat(long formatId)
    {
        bool? confirm = await DialogService.ShowMessageBox(
            "Potwierdzenie",
            "Czy na pewno chcesz dezaktywować ten format produktu?",
            yesText: "Tak", cancelText: "Anuluj");

        if (confirm == true)
        {
            try
            {
                var result = await ProductFormatService.DeactivateAsync(formatId);
                if (result.IsSuccess)
                {
                    Snackbar.Add("Format produktu został dezaktywowany", Severity.Success);
                    await LoadFormats();
                }
                else
                {
                    Snackbar.Add($"Błąd podczas dezaktywacji: {result.Error.Message}", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
            }
        }
    }

    // Batch Split Rules CRUD Methods
    private async Task<TableData<BatchSplitRuleDto>> LoadRulesData(TableState state, CancellationToken cancellationToken = default)
    {
        try
        {
            loadingRules = true;
            
            var query = new BatchSplitRuleQuery();
            var result = await BatchSplitRuleService.GetAsync(query);
            
            if (result.IsSuccess)
            {
                // Client-side pagination for rules (usually small dataset)
                var pagedItems = result.Value
                    .Skip(state.Page * state.PageSize)
                    .Take(state.PageSize)
                    .ToList();

                return new TableData<BatchSplitRuleDto>
                {
                    TotalItems = result.Value.Count,
                    Items = pagedItems
                };
            }
            else
            {
                Snackbar.Add($"Błąd podczas ładowania reguł: {result.Error.Message}", Severity.Error);
                return new TableData<BatchSplitRuleDto> { TotalItems = 0, Items = Array.Empty<BatchSplitRuleDto>() };
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
            return new TableData<BatchSplitRuleDto> { TotalItems = 0, Items = Array.Empty<BatchSplitRuleDto>() };
        }
        finally
        {
            loadingRules = false;
        }
    }

    private async Task LoadRules()
    {
        if (rulesTable != null)
        {
            await rulesTable.ReloadServerData();
        }
    }

    private async Task OpenCreateBatchRuleDialog()
    {
        var parameters = new DialogParameters();
        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        
        var dialog = await DialogService.ShowAsync<BatchSplitRuleDialog>("Dodaj nową regułę podziału", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            await LoadRules();
        }
    }

    private async Task OpenEditBatchRuleDialog(BatchSplitRuleDto rule)
    {
        var parameters = new DialogParameters 
        { 
            ["BatchSplitRule"] = rule,
            ["IsEdit"] = true
        };
        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Medium, FullWidth = true };
        
        var dialog = await DialogService.ShowAsync<BatchSplitRuleDialog>("Edytuj regułę podziału", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled)
        {
            await LoadRules();
        }
    }

    private async Task DeactivateBatchRule(long ruleId)
    {
        bool? confirm = await DialogService.ShowMessageBox(
            "Potwierdzenie",
            "Czy na pewno chcesz dezaktywować tę regułę podziału?",
            yesText: "Tak", cancelText: "Anuluj");

        if (confirm == true)
        {
            await ToggleBatchRuleStatus(ruleId, false);
        }
    }

    private async Task ActivateBatchRule(long ruleId)
    {
        await ToggleBatchRuleStatus(ruleId, true);
    }

    private async Task ToggleBatchRuleStatus(long ruleId, bool isActive)
    {
        try
        {
            var result = await BatchSplitRuleService.SetActiveAsync(ruleId, isActive);
            if (result.IsSuccess)
            {
                var action = isActive ? "aktywowana" : "dezaktywowana";
                Snackbar.Add($"Reguła została {action}", Severity.Success);
                await LoadRules();
            }
            else
            {
                Snackbar.Add($"Błąd podczas zmiany statusu: {result.Error.Message}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
        }
    }

    // Projects CRUD Methods
    private async Task<TableData<ProjectListItemDto>> LoadProjectsData(TableState state, CancellationToken cancellationToken = default)
    {
        try
        {
            loadingProjects = true;
            
            var query = new ProjectQuery
            {
                IsCompleted = projectFilterCompleted,
                Page = state.Page + 1,
                PageSize = state.PageSize
            };

            var result = await ProjectService.GetAsync(query);
            
            if (result.IsSuccess)
            {
                return new TableData<ProjectListItemDto>
                {
                    TotalItems = (int)result.Value.Total,
                    Items = result.Value.Items
                };
            }
            else
            {
                Snackbar.Add($"Błąd podczas ładowania projektów: {result.Error.Message}", Severity.Error);
                return new TableData<ProjectListItemDto> { TotalItems = 0, Items = Array.Empty<ProjectListItemDto>() };
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
            return new TableData<ProjectListItemDto> { TotalItems = 0, Items = Array.Empty<ProjectListItemDto>() };
        }
        finally
        {
            loadingProjects = false;
        }
    }

    private async Task LoadProjects()
    {
        if (projectsTable != null)
        {
            await projectsTable.ReloadServerData();
        }
    }

    private async Task OnProjectFilterChanged(bool? value)
    {
        projectFilterCompleted = value;
        await LoadProjects();
    }

    private async Task ClearProjectFilters()
    {
        projectFilterCompleted = null;
        await LoadProjects();
    }

    private async Task DeleteProject(long projectId)
    {
        bool? confirm = await DialogService.ShowMessageBox(
            "Potwierdzenie usunięcia",
            "Czy na pewno chcesz usunąć ten projekt? Ta operacja jest nieodwracalna.",
            yesText: "Usuń", cancelText: "Anuluj");

        if (confirm == true)
        {
            try
            {
                var result = await ProjectService.DeleteAsync(projectId);
                if (result.IsSuccess)
                {
                    Snackbar.Add("Projekt został usunięty", Severity.Success);
                    await LoadProjects();
                }
                else
                {
                    Snackbar.Add($"Błąd podczas usuwania projektu: {result.Error.Message}", Severity.Error);
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Wystąpił błąd: {ex.Message}", Severity.Error);
            }
        }
    }
}

// Extension methods for DateTime conversion
public static class DateTimeExtensions
{
    public static DateOnly? ToDateOnly(this DateTime? dateTime)
    {
        return dateTime.HasValue ? DateOnly.FromDateTime(dateTime.Value) : null;
    }
    
    public static DateOnly ToDateOnly(this DateTime dateTime)
    {
        return DateOnly.FromDateTime(dateTime);
    }
}