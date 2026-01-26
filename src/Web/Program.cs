using DataAccess;
using DataAccess.Identity;
using DataAccess.Repositories;
using KanbanLite.Application;
using KanbanLite.Application.Security;
using KanbanLite.Web;
using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Wyłączenie anti-forgery dla MVP: wszystkie operacje przez Blazor Server (SignalR), brak tradycyjnych HTTP POST
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});
// Dodanie obsługi kontrolerów dla AuthController (Cookie Bridge)
builder.Services.AddControllers();

// MudBlazor
builder.Services.AddMudServices();

// Supabase
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? throw new InvalidOperationException("Supabase Url not found.");
var supabaseKey = builder.Configuration["Supabase:Key"] ?? throw new InvalidOperationException("Supabase Key not found.");
// Wyłączamy AutoConnectRealtime w środowisku Production/CI, aby uniknąć problemów z autoryzacją WebSocket
var isDevelopment = builder.Environment.IsDevelopment();
var supabaseOptions = new Supabase.SupabaseOptions { AutoConnectRealtime = isDevelopment };
builder.Services.AddScoped<Supabase.Client>(_ => new Supabase.Client(supabaseUrl, supabaseKey, supabaseOptions));

// DataAccess - AppDbContext z DbContextFactory dla Blazor Server
var connectionString = builder.Configuration.GetConnectionString("KanbanConnectionString") ?? Environment.GetEnvironmentVariable("KANBANLITE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Connection string 'KanbanConnectionString' not found.");

// Używamy DbContextFactory dla Blazor Server - rozwiązuje problem disposed context
// AddDbContextFactory automatycznie rejestruje też AppDbContext jako scoped dla Identity
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString), ServiceLifetime.Scoped);

// Repository Pattern - UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Konfiguracja haseł - zgodnie z seedem (menago/menago, operator/operator)
    options.Password.RequiredLength = 1;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    // Konfiguracja użytkownika
    options.User.RequireUniqueEmail = false;

    // Konfiguracja logowania
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Konfiguracja cookie authentication - ustawienie właściwej ścieżki logowania
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Authorization
// Uwaga: Nie używamy FallbackPolicy, ponieważ blokuje ona dostęp do /login i / przed dotarciem do Blazor Router
// Zamiast tego używamy [Authorize] na konkretnych stronach, a AuthorizeRouteView w App.razor obsługuje przekierowania
builder.Services.AddAuthorization();

// Blazor Server Authentication State Provider
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();

// Session Service - Singleton dla zarządzania sesją użytkownika
builder.Services.AddSingleton<ISessionService, SessionService>();

// Application layer
builder.Services.AddHttpContextAccessor();
// ICurrentUser oparty na SessionService zamiast ClaimsPrincipal
builder.Services.AddScoped<ICurrentUser, SessionCurrentUser>();
builder.Services.AddKanbanLiteApplication();

// Auth Service - używa UserManager + SessionService
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Inicjalizacja Supabase
// Obsługa błędu inicjalizacji - aplikacja może działać bez Realtime w środowisku CI/CD
using (var scope = app.Services.CreateScope())
{
    try
    {
        var supabase = scope.ServiceProvider.GetRequiredService<Supabase.Client>();
        await supabase.InitializeAsync();
    }
    catch (Exception ex)
    {
        // W środowisku Production/CI logujemy błąd, ale nie przerywamy uruchomienia aplikacji
        // Realtime nie jest krytyczne dla podstawowej funkcjonalności aplikacji
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Failed to initialize Supabase Realtime. Application will continue without Realtime support.");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAntiforgery();
// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Mapowanie kontrolerów (AuthController dla Cookie Bridge)
app.MapControllers();

// Wyłączenie anti-forgery dla Razor Components - wszystko działa przez SignalR, CSRF nie dotyczy
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Mapowanie SignalR hub dla Blazor Server
// Konfiguracja SignalR jest w AddServerSideBlazor() powyżej

app.Run();
