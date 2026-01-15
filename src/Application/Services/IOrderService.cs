using KanbanLite.Application.Common;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IOrderService
{
    Task<Result<CreateOrderResult>> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<Result<PagedResult<OrderListItemDto>>> GetAsync(OrderQuery query, CancellationToken ct = default);
    Task<Result<OrderDetailsDto>> GetByIdAsync(long id, CancellationToken ct = default);
}

