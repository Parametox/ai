using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataAccess;

/// <summary>
/// Fabryka używana przez EF Core tools (migracje) poza hostem aplikacji.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("KANBANLITE_CONNECTION_STRING")
            ?? "Host=db.fntdzqdxfbddesaijpdr.supabase.co;Port=6543;Database=postgres;Username=postgres;Password=ytkYBbGznj4NqWcfRvY6;Pooling=true;Trust Server Certificate=true;";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}

