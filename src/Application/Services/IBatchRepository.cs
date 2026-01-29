using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IBatchRepository
{
    Task<(IReadOnlyList<SupabaseBatch> Items, int TotalCount)> GetBatchesAsync(KanbanQuery query, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetInProgressCountAsync(CancellationToken ct = default);
    Task<SupabaseBatch?> GetByIdAsync(long id, CancellationToken ct = default);
    Task CreateRangeAsync(IEnumerable<SupabaseBatch> batches, CancellationToken ct = default);
    Task UpdateStatusAsync(long id, string status, DateTimeOffset updatedAt, CancellationToken ct = default);
    Task UpdateStageAsync(long id, short stage, DateTimeOffset updatedAt, CancellationToken ct = default);
    Task AddAuditLogAsync(SupabaseBatchAuditLog log, CancellationToken ct = default);
    Task DeleteByProjectIdAsync(long projectId, CancellationToken ct = default);
}
