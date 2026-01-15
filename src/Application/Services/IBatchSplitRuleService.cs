using KanbanLite.Application.Common;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IBatchSplitRuleService
{
    Task<Result<IReadOnlyList<BatchSplitRuleDto>>> GetAsync(BatchSplitRuleQuery query, CancellationToken ct = default);
    Task<Result<BatchSplitRuleDto>> CreateAsync(UpsertBatchSplitRuleRequest request, CancellationToken ct = default);
    Task<Result<BatchSplitRuleDto>> UpdateAsync(long id, UpsertBatchSplitRuleRequest request, CancellationToken ct = default);
    Task<Result<BatchSplitRuleDto>> SetActiveAsync(long id, bool isActive, CancellationToken ct = default);
}

