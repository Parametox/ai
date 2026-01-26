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
            ?? throw new InvalidOperationException("Connection string 'KANBANLITE_CONNECTION_STRING' not found in environment variables.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}

