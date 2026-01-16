using DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("KanbanConnectionString")
    ?? throw new InvalidOperationException("Connection string 'KanbanConnectionString' not found.");

Console.WriteLine("Applying EF Core migrations...");
Console.WriteLine($"Connection string: {connectionString}");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();

Console.WriteLine("Migrations applied.");
