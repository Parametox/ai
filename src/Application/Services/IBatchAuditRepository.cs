using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IBatchAuditRepository
{
    Task<(IReadOnlyList<SupabaseBatchAuditLog> Items, int TotalCount)> GetForBatchAsync(long batchId, int page, int pageSize, CancellationToken ct = default);
}
