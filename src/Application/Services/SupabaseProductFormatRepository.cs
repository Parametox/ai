using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using Postgrest;
using static Postgrest.Constants;

namespace KanbanLite.Application.Services;

public class SupabaseProductFormatRepository(ISupabaseClientAccessor supabaseAccessor) : IProductFormatRepository
{
    private Supabase.Client supabaseClient => supabaseAccessor.Client;

    public async Task<(IReadOnlyList<SupabaseProductFormat> Items, int TotalCount)> GetAsync(ProductFormatQuery query, int page, int pageSize, CancellationToken ct = default)
    {
        // 1. Count Builder
        var countBuilder = supabaseClient.From<SupabaseProductFormat>()
            .Select("*");

        if (query.IsActive.HasValue)
        {
            countBuilder = countBuilder.Filter("is_active", Operator.Equals, query.IsActive.Value.ToString().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            countBuilder = countBuilder.Filter("name", Operator.ILike, $"%{query.Q}%");
        }

        var total = await countBuilder.Count(CountType.Exact);

        // 2. Data Builder
        var dataBuilder = supabaseClient.From<SupabaseProductFormat>()
            .Select("*");

        if (query.IsActive.HasValue)
        {
            dataBuilder = dataBuilder.Filter("is_active", Operator.Equals, query.IsActive.Value.ToString().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            dataBuilder = dataBuilder.Filter("name", Operator.ILike, $"%{query.Q}%");
        }

        dataBuilder = dataBuilder.Order("name", Ordering.Ascending);

        var from = (page - 1) * pageSize;
        var to = from + pageSize - 1;
        dataBuilder = dataBuilder.Range(from, to);

        var response = await dataBuilder.Get(ct);

        return (response.Models, total);
    }

    public async Task<SupabaseProductFormat?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await supabaseClient.From<SupabaseProductFormat>()
            .Filter("id", Operator.Equals, id.ToString())
            .Single(ct);
    }

    public async Task<IReadOnlyList<SupabaseProductFormat>> GetActiveLookupAsync(CancellationToken ct = default)
    {
        var response = await supabaseClient.From<SupabaseProductFormat>()
           .Select("id, name")
           .Filter("is_active", Operator.Equals, "true")
           .Order("name", Ordering.Ascending)
           .Get(ct);
        return response.Models;
    }

    public async Task CreateAsync(SupabaseProductFormat format, CancellationToken ct = default)
    {
        // Insert and select representation to get ID if needed, 
        // but void is okay if we use Supabase client which updates the model ID usually.
        await supabaseClient.From<SupabaseProductFormat>().Insert(format);
    }

    public async Task UpdateAsync(SupabaseProductFormat format, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseProductFormat>().Update(format);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseProductFormat>()
           .Filter("id", Operator.Equals, id.ToString())
           .Delete(new Postgrest.QueryOptions(), ct);
    }
}
