using KanbanLite.Application.Services.SupabaseModels;

namespace KanbanLite.Application.Services;

public interface IBatchSplitRuleRepository
{
    Task<IReadOnlyList<SupabaseBatchSplitRule>> GetActiveRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SupabaseBatchSplitRule>> GetAllAsync(bool? isActive = null, CancellationToken ct = default);
    Task<SupabaseBatchSplitRule?> GetByIdAsync(long id, CancellationToken ct = default);
    Task CreateAsync(SupabaseBatchSplitRule rule, CancellationToken ct = default);
    Task UpdateAsync(SupabaseBatchSplitRule rule, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
