using DataAccess;
using Microsoft.EntityFrameworkCore;

var connectionString =
    Environment.GetEnvironmentVariable("KANBANLITE_CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=kanbanlite;Username=kanbanlite;Password=kanbanlite";

Console.WriteLine("Applying EF Core migrations...");
Console.WriteLine($"Connection string: {connectionString}");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();

Console.WriteLine("Migrations applied.");
