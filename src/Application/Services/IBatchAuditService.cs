using KanbanLite.Application.Common;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IBatchAuditService
{
    Task<Result<PagedResult<BatchAuditEventDto>>> GetForBatchAsync(
        long batchId,
        PageQuery page,
        CancellationToken ct = default);
}

