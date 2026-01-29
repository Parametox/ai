using DataAccess;
using KanbanLite.Application.Security;
using KanbanLite.Application.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace KanbanLite.Application.UnitTests;

public abstract class TestBase : IDisposable
{
    protected readonly AppDbContext DbContext;
    protected readonly IDbContextFactory<AppDbContext> DbFactory;
    protected readonly ICurrentUser CurrentUser;
    protected readonly ISupabaseClientAccessor SupabaseClientAccessor;

    protected TestBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        DbContext = new AppDbContext(options);

        DbFactory = Substitute.For<IDbContextFactory<AppDbContext>>();
        DbFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new AppDbContext(options));
        // Note: Creating a new context each time to simulate factory behavior, 
        // but pointing to same InMemory DB.

        CurrentUser = Substitute.For<ICurrentUser>();
        
        // Mock SupabaseClientAccessor - returns null client, tests need to handle this
        SupabaseClientAccessor = Substitute.For<ISupabaseClientAccessor>();
    }

    public void Dispose()
    {
        DbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
