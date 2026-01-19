using DataAccess;
using DataAccess.Identity;
using KanbanLite.Application;
using KanbanLite.Application.Security;
using KanbanLite.Web;
using KanbanLite.Web.Data;
using KanbanLite.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Wyłączenie anti-forgery dla MVP: wszystkie operacje przez Blazor Server (SignalR), brak tradycyjnych HTTP POST
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
});
builder.Services.AddSingleton<WeatherForecastService>();

// MudBlazor
builder.Services.AddMudServices();

// DataAccess - AppDbContext
var connectionString = builder.Configuration.GetConnectionString("KanbanConnectionString") ?? Environment.GetEnvironmentVariable("KANBANLITE_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Connection string 'KanbanConnectionString' not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

// Application layer
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, ClaimsPrincipalCurrentUser>();
builder.Services.AddKanbanLiteApplication();

// Auth Service
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAntiforgery();
// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Wyłączenie anti-forgery dla Razor Components - wszystko działa przez SignalR, CSRF nie dotyczy
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Mapowanie SignalR hub dla Blazor Server
// Konfiguracja SignalR jest w AddServerSideBlazor() powyżej

app.Run();
