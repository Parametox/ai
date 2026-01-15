using KanbanLite.Application.Common;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IBatchService
{
    Task<Result<KanbanBatchesResult>> GetKanbanAsync(
        KanbanQuery query,
        CancellationToken ct = default);

    Task<Result<UpdateBatchStatusResult>> UpdateStatusAsync(
        long batchId,
        UpdateBatchStatusRequest request,
        CancellationToken ct = default);

    Task<Result<UpdateBatchStageResult>> UpdateStageAsync(
        long batchId,
        UpdateBatchStageRequest request,
        CancellationToken ct = default);

    Task<Result<int>> GetInProgressCountAsync(CancellationToken ct = default);
}

