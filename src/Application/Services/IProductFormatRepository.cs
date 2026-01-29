using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public interface IProductFormatRepository
{
    Task<(IReadOnlyList<SupabaseProductFormat> Items, int TotalCount)> GetAsync(ProductFormatQuery query, int page, int pageSize, CancellationToken ct = default);
    Task<SupabaseProductFormat?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<SupabaseProductFormat>> GetActiveLookupAsync(CancellationToken ct = default);
    Task CreateAsync(SupabaseProductFormat format, CancellationToken ct = default);
    Task UpdateAsync(SupabaseProductFormat format, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
