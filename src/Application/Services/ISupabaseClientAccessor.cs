namespace KanbanLite.Application.Services;

/// <summary>
/// Provides access to the Supabase client for dependency injection and testability.
/// </summary>
public interface ISupabaseClientAccessor
{
    Supabase.Client Client { get; }
}

/// <summary>
/// Default implementation that holds a Supabase client instance.
/// </summary>
public class SupabaseClientAccessor(Supabase.Client client) : ISupabaseClientAccessor
{
    public Supabase.Client Client { get; } = client;
}
