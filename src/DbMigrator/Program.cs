using DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Pobierz ścieżkę do katalogu, w którym znajduje się plik wykonywalny
var basePath = AppContext.BaseDirectory;

// Określ środowisko (domyślnie Production, można ustawić przez zmienną DOTNET_ENVIRONMENT)
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

var configuration = new ConfigurationBuilder()
    .SetBasePath(basePath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Inicjalizacja Supabase Client
var supabaseUrl = configuration["Supabase:Url"] ?? throw new InvalidOperationException("Supabase Url not found.");
var supabaseKey = configuration["Supabase:Key"] ?? throw new InvalidOperationException("Supabase Key not found.");
var supabaseOptions = new Supabase.SupabaseOptions { AutoConnectRealtime = true };
var supabase = new Supabase.Client(supabaseUrl, supabaseKey, supabaseOptions);
await supabase.InitializeAsync();

Console.WriteLine("Applying EF Core migrations...");
Console.WriteLine($"Connection string: {connectionString}");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();

Console.WriteLine("Migrations applied.");
