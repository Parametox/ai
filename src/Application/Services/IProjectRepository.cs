using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IProjectRepository
{
    Task<(IReadOnlyList<SupabaseProject> Items, int TotalCount)> GetProjectsAsync(ProjectQuery query, int page, int pageSize, CancellationToken ct = default);
    Task<SupabaseProject?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<SupabaseProject?> GetByOrderIdAsync(long orderId, CancellationToken ct = default);
    Task<SupabaseProject> CreateAsync(SupabaseProject project, CancellationToken ct = default);
    Task UpdateAsync(SupabaseProject project, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
