using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IDashboardRepository
{
    Task<Dictionary<string, int>> GetStatusCountsForActiveProjectsAsync(CancellationToken ct = default);
    Task<Dictionary<short, int>> GetStageCountsForActiveProjectsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DashboardUrgentOrderDto>> GetUrgentOrdersAsync(DateTime thresholdDate, CancellationToken ct = default);
}
