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
            ?? "Host=localhost;Port=5432;Database=kanbanlite;Username=kanbanlite;Password=kanbanlite";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}

