using KanbanLite.Application.Services.SupabaseModels;
using Postgrest;
using static Postgrest.Constants;

namespace KanbanLite.Application.Services;

public class SupabaseBatchAuditRepository(ISupabaseClientAccessor supabaseAccessor) : IBatchAuditRepository
{
    private Supabase.Client supabaseClient => supabaseAccessor.Client;

    public async Task<(IReadOnlyList<SupabaseBatchAuditLog> Items, int TotalCount)> GetForBatchAsync(long batchId, int page, int pageSize, CancellationToken ct = default)
    {
        var builder = supabaseClient.From<SupabaseBatchAuditLog>()
            .Filter("batch_id", Operator.Equals, batchId);

        var countAttempt = await builder.Count(CountType.Exact);
        var total = countAttempt;

        builder = builder.Order("changed_at", Ordering.Descending)
                         .Order("id", Ordering.Descending);

        var from = (page - 1) * pageSize;
        var to = from + pageSize - 1;
        builder = builder.Range(from, to);

        var response = await builder.Get(ct);

        return (response.Models, total);
    }
}
