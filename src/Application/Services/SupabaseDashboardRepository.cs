using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using Postgrest;
using static Postgrest.Constants;

namespace KanbanLite.Application.Services;

public class SupabaseDashboardRepository(ISupabaseClientAccessor supabaseAccessor) : IDashboardRepository
{
    private Supabase.Client supabaseClient => supabaseAccessor.Client;

    public async Task<Dictionary<string, int>> GetStatusCountsForActiveProjectsAsync(CancellationToken ct = default)
    {
        // Supabase/Postgrest doesn't support advanced aggregation (GROUP BY) well via the client yet.
        // We will fetch batches for active projects and aggregate in memory or use an RPC (preferred if possible, but avoiding SQL for now as per instructions to use client).
        // Since we want to avoid fetching ALL batches, let's try to filter by active projects.
        // Complex query: Batches where Project.IsCompleted = false.

        // Using "Select" with join to filter is tricky for bulk filtering.
        // Alternative: Fetch active project Ids, then fetch batches for those IDs.
        // Or if RPC is not an option, just fetch required columns status, stage for active projects.

        // !inner join to filter parents
        var response = await supabaseClient.From<SupabaseBatch>()
            .Select("status, Project:projects!inner(is_completed)")
            .Filter("projects.is_completed", Operator.Equals, "false")
            .Get(ct);

        var models = response.Models;

        // Aggregate
        var dict = new Dictionary<string, int>();
        foreach (var m in models)
        {
            var status = m.Status ?? "";
            if (!dict.ContainsKey(status)) dict[status] = 0;
            dict[status]++;
        }
        return dict;
    }

    public async Task<Dictionary<short, int>> GetStageCountsForActiveProjectsAsync(CancellationToken ct = default)
    {
        // Similar to above
        var response = await supabaseClient.From<SupabaseBatch>()
            .Select("stage, Project:projects!inner(is_completed)")
            .Filter("projects.is_completed", Operator.Equals, "false")
            .Get(ct);

        var models = response.Models;

        var dict = new Dictionary<short, int>();
        foreach (var m in models)
        {
            var stage = m.Stage;
            if (!dict.ContainsKey(stage)) dict[stage] = 0;
            dict[stage]++;
        }
        return dict;
    }

    public async Task<IReadOnlyList<DashboardUrgentOrderDto>> GetUrgentOrdersAsync(DateTime thresholdDate, CancellationToken ct = default)
    {
        // Projects not completed, Order due date < thresholdDate
        // Join Project -> Order

        var response = await supabaseClient.From<SupabaseProject>()
            .Select("id, project_number, Order:orders!inner(id, order_number, due_date)")
            .Filter("is_completed", Operator.Equals, "false")
            .Filter("orders.due_date", Operator.LessThan, thresholdDate.ToString("yyyy-MM-dd"))
            .Filter("orders.due_date", Operator.GreaterThanOrEqual, DateTime.UtcNow.ToString("yyyy-MM-dd"))
            // Sorting by foreign column via client is causing parsing errors. Sorting in memory instead.
            .Limit(50)
            .Get(ct);

        return response.Models.Select(p => new DashboardUrgentOrderDto(
            p.OrderId,
            p.Order?.OrderNumber ?? "",
            DateOnly.FromDateTime(p.Order?.DueDate ?? DateTime.MinValue),
            p.Id,
            p.ProjectNumber ?? ""
        ))
        .OrderBy(x => x.DueDate)
        .ToList();
    }
}
