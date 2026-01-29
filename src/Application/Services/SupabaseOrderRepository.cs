using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using Postgrest;
using static Postgrest.Constants;

namespace KanbanLite.Application.Services;

public class SupabaseOrderRepository(ISupabaseClientAccessor supabaseAccessor) : IOrderRepository
{
    private Supabase.Client supabaseClient => supabaseAccessor.Client;

    public async Task<(IReadOnlyList<SupabaseOrder> Items, int TotalCount)> GetOrdersAsync(OrderQuery query, int page, int pageSize, CancellationToken ct = default)
    {
        // 1. Count Builder
        var countBuilder = supabaseClient.From<SupabaseOrder>()
           .Select("*, ProductFormat:product_formats(*)");

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            countBuilder = countBuilder.Filter("order_number", Operator.ILike, $"%{query.Q}%");
        }

        if (query.DueFrom.HasValue)
        {
            countBuilder = countBuilder.Filter("due_date", Operator.GreaterThanOrEqual, query.DueFrom.Value.ToString("yyyy-MM-dd"));
        }

        if (query.DueTo.HasValue)
        {
            countBuilder = countBuilder.Filter("due_date", Operator.LessThanOrEqual, query.DueTo.Value.ToString("yyyy-MM-dd"));
        }

        var total = await countBuilder.Count(CountType.Exact);

        // 2. Data Builder
        var dataBuilder = supabaseClient.From<SupabaseOrder>()
           .Select("*, ProductFormat:product_formats(*)");

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            dataBuilder = dataBuilder.Filter("order_number", Operator.ILike, $"%{query.Q}%");
        }

        if (query.DueFrom.HasValue)
        {
            dataBuilder = dataBuilder.Filter("due_date", Operator.GreaterThanOrEqual, query.DueFrom.Value.ToString("yyyy-MM-dd"));
        }

        if (query.DueTo.HasValue)
        {
            dataBuilder = dataBuilder.Filter("due_date", Operator.LessThanOrEqual, query.DueTo.Value.ToString("yyyy-MM-dd"));
        }

        dataBuilder = dataBuilder.Order("due_date", Ordering.Ascending)
                         .Order("order_number", Ordering.Ascending);

        var from = (page - 1) * pageSize;
        var to = from + pageSize - 1;
        dataBuilder = dataBuilder.Range(from, to);

        var response = await dataBuilder.Get(ct);

        return (response.Models, total);
    }

    public async Task<SupabaseOrder?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await supabaseClient.From<SupabaseOrder>()
            .Select("*, ProductFormat:product_formats(*)")
            .Filter("id", Operator.Equals, id.ToString())
            .Single(ct);
    }

    public async Task<SupabaseOrder> CreateAsync(SupabaseOrder order, CancellationToken ct = default)
    {
        var response = await supabaseClient.From<SupabaseOrder>()
            .Insert(order, new Postgrest.QueryOptions { Returning = Postgrest.QueryOptions.ReturnType.Representation });
        return response.Models.First();
    }
}
