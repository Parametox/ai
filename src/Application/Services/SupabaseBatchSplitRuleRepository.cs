using KanbanLite.Application.Services.SupabaseModels;
using Postgrest;
using static Postgrest.Constants;

namespace KanbanLite.Application.Services;

public class SupabaseBatchSplitRuleRepository(ISupabaseClientAccessor supabaseAccessor) : IBatchSplitRuleRepository
{
    private Supabase.Client supabaseClient => supabaseAccessor.Client;

    public async Task<IReadOnlyList<SupabaseBatchSplitRule>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        var response = await supabaseClient.From<SupabaseBatchSplitRule>()
            .Filter("is_active", Operator.Equals, true)
            .Get(ct);
        
        return response.Models;
    }

    public async Task<IReadOnlyList<SupabaseBatchSplitRule>> GetAllAsync(bool? isActive = null, CancellationToken ct = default)
    {
        var builder = supabaseClient.From<SupabaseBatchSplitRule>()
            .Select("*");

        if (isActive.HasValue)
        {
            builder = builder.Filter("is_active", Operator.Equals, isActive.Value);
        }

        builder = builder.Order("is_active", Ordering.Descending)
                         .Order("min_qty", Ordering.Ascending);

        var response = await builder.Get(ct);
        return response.Models;
    }

    public async Task<SupabaseBatchSplitRule?> GetByIdAsync(long id, CancellationToken ct = default)
    {
         return await supabaseClient.From<SupabaseBatchSplitRule>()
            .Filter("id", Operator.Equals, id)
            .Single(ct);
    }

    public async Task CreateAsync(SupabaseBatchSplitRule rule, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseBatchSplitRule>().Insert(rule);
    }

    public async Task UpdateAsync(SupabaseBatchSplitRule rule, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseBatchSplitRule>().Update(rule);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await supabaseClient.From<SupabaseBatchSplitRule>().Delete(new SupabaseBatchSplitRule { Id = id });
    }
}
