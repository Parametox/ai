using KanbanLite.Application.Common;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IProductFormatService
{
    Task<Result<PagedResult<ProductFormatDto>>> GetAsync(ProductFormatQuery query, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProductFormatLookupDto>>> GetActiveLookupAsync(CancellationToken ct = default);
    Task<Result<ProductFormatDto>> CreateAsync(CreateProductFormatRequest request, CancellationToken ct = default);
    Task<Result<ProductFormatDto>> UpdateAsync(long id, UpdateProductFormatRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeactivateAsync(long id, CancellationToken ct = default);
}

