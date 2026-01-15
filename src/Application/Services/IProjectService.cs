using KanbanLite.Application.Common;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IProjectService
{
    Task<Result<PagedResult<ProjectListItemDto>>> GetAsync(ProjectQuery query, CancellationToken ct = default);
    Task<Result<ProjectDetailsDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Result<ShipProjectResult>> ShipToCustomerAsync(long projectId, CancellationToken ct = default);
}

