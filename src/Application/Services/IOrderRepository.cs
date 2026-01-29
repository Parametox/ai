using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IOrderRepository
{
    Task<(IReadOnlyList<SupabaseOrder> Items, int TotalCount)> GetOrdersAsync(OrderQuery query, int page, int pageSize, CancellationToken ct = default);
    Task<SupabaseOrder?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<SupabaseOrder> CreateAsync(SupabaseOrder order, CancellationToken ct = default);
}
