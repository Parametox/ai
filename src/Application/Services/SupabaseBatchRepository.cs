using DataAccess.Enums;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using Postgrest;
using static Postgrest.Constants;

namespace KanbanLite.Application.Services;

public class SupabaseBatchRepository(ISupabaseClientAccessor supabaseAccessor) : IBatchRepository
{
    private Supabase.Client supabaseClient => supabaseAccessor.Client;

    public async Task<(IReadOnlyList<SupabaseBatch> Items, int TotalCount)> GetBatchesAsync(KanbanQuery query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();

        // 1. Build Count Query
        var countBuilder = supabaseClient.From<SupabaseBatch>()
            .Select("*, Project:projects!inner(*, Order:orders(*))");

        countBuilder = countBuilder.Filter("projects.is_completed", Operator.Equals, false.ToString().ToLower());

        if (query.Status.HasValue)
        {
            countBuilder = countBuilder.Filter("status", Operator.Equals, query.Status.Value.ToString());
        }

        if (query.Stage.HasValue)
        {
            countBuilder = countBuilder.Filter("stage", Operator.Equals, (int)query.Stage.Value);
        }

        if (q is not null)
        {
            countBuilder = countBuilder.Filter("projects.project_number", Operator.ILike, $"%{q}%");
        }

        // Get total count
        var countAttempt = await countBuilder.Count(CountType.Exact);
        var total = countAttempt;

        // 2. Build Data Query
        var dataBuilder = supabaseClient.From<SupabaseBatch>()
            .Select("*, Project:projects!inner(*, Order:orders(*))");

        dataBuilder = dataBuilder.Filter("projects.is_completed", Operator.Equals, false.ToString().ToLower());

        if (query.Status.HasValue)
        {
            dataBuilder = dataBuilder.Filter("status", Operator.Equals, query.Status.Value.ToString());
        }

        if (query.Stage.HasValue)
        {
            dataBuilder = dataBuilder.Filter("stage", Operator.Equals, (int)query.Stage.Value);
        }

        if (q is not null)
        {
            dataBuilder = dataBuilder.Filter("projects.project_number", Operator.ILike, $"%{q}%");
        }

        // Sorting
        var sort = query.Sort ?? KanbanSort.UpdatedAtDesc;
        switch (sort)
        {
            case KanbanSort.DueDateAsc:
                dataBuilder = dataBuilder.Order("projects.orders.due_date", Ordering.Ascending)
                                 .Order("updated_at", Ordering.Descending);
                break;
            case KanbanSort.DueDateDesc:
                dataBuilder = dataBuilder.Order("projects.orders.due_date", Ordering.Descending)
                                 .Order("updated_at", Ordering.Descending);
                break;
            case KanbanSort.UpdatedAtDesc:
                dataBuilder = dataBuilder.Order("updated_at", Ordering.Descending);
                break;
            default:
                dataBuilder = dataBuilder.Order("updated_at", Ordering.Descending);
                break;
        }

        // Pagination
        var from = (page - 1) * pageSize;
        var to = from + pageSize - 1;

        dataBuilder = dataBuilder.Range(from, to);

        // Execute Data Query
        var response = await dataBuilder.Get(ct);

        return (response.Models, total);
    }

    public async Task<int> GetInProgressCountAsync(CancellationToken ct = default)
    {
        return await supabaseClient.From<SupabaseBatch>()
            .Filter("status", Operator.Equals, BatchStatus.InProgress.ToString())
            .Count(CountType.Exact);
    }

    public async Task<SupabaseBatch?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await supabaseClient.From<SupabaseBatch>()
           .Select("*")
           .Filter("id", Operator.Equals, id.ToString())
           .Single(ct);
    }

    public async Task CreateRangeAsync(IEnumerable<SupabaseBatch> batches, CancellationToken ct = default)
    {
        if (!batches.Any()) return;
        await supabaseClient.From<SupabaseBatch>().Insert(batches.ToList());
    }

    public async Task UpdateStatusAsync(long id, string status, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseBatch>()
            .Where(x => x.Id == id)
            .Set(x => x.Status, status)
            .Set(x => x.UpdatedAt, updatedAt)
            .Update(cancellationToken: ct);
    }

    public async Task UpdateStageAsync(long id, short stage, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseBatch>()
            .Where(x => x.Id == id)
            .Set(x => x.Stage, stage)
            .Set(x => x.UpdatedAt, updatedAt)
            .Update(cancellationToken: ct);
    }

    public async Task AddAuditLogAsync(SupabaseBatchAuditLog log, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseBatchAuditLog>().Insert(log, cancellationToken: ct);
    }

    public async Task DeleteByProjectIdAsync(long projectId, CancellationToken ct = default)
    {
        // Get batch IDs first to delete dependent audit logs
        var batchesResponse = await supabaseClient.From<SupabaseBatch>()
           .Select("id")
           .Filter("project_id", Operator.Equals, projectId.ToString())
           .Get(ct);

        var batchIds = batchesResponse.Models.Select(b => b.Id).ToList();

        if (batchIds.Count > 0)
        {
            // Delete dependent audit logs 
            await supabaseClient.From<SupabaseBatchAuditLog>()
                .Filter("batch_id", Operator.In, batchIds.Select(id => (object)id).ToList())
                .Delete(cancellationToken: ct);
        }

        await supabaseClient.From<SupabaseBatch>()
           .Filter("project_id", Operator.Equals, projectId.ToString())
           .Delete(cancellationToken: ct);
    }
}
