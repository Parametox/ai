using DataAccess;
using DataAccess.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace DbSeed.IntegrationTests;

public sealed class IdentitySeedTests
{
    [Fact]
    [Trait("Category", "Seed")]
    public async Task Seed_users_and_roles_for_local_dev()
    {
        if (!IsEnabled())
        {
            // To jest jednorazowy seed bazy. Ustaw env var KANBANLITE_RUN_DB_SEED_TEST=1 aby uruchomić.
            return;
        }

        var connectionString =
            Environment.GetEnvironmentVariable("KANBANLITE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kanbanlite;Username=postgres;Password=postgres";

        // Inicjalizacja Supabase Client (opcjonalnie, jeśli chcesz używać go zamiast EF Core)
        var supabaseUrl = "https://fntdzqdxfbddesaijpdr.supabase.co";
        var supabaseKey = "sb_publishable_j9ivqerfdqVOXkgTun7sLg__jZgxnaq"; 
        var supabaseOptions = new Supabase.SupabaseOptions { AutoConnectRealtime = true };
        var supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey, supabaseOptions);
        await supabaseClient.InitializeAsync();

        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Seedowe hasła z README: menago/menago, operator/operator
                // Nie chcemy tu walidacji złożoności.
                options.Password.RequiredLength = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        const string managerRoleName = "Manager";
        const string operatorRoleName = "Operator";

        await EnsureRoleExists(roleManager, managerRoleName);
        await EnsureRoleExists(roleManager, operatorRoleName);

        await EnsureUserInRole(userManager, userName: "menago", password: "menago", roleName: managerRoleName);
        await EnsureUserInRole(userManager, userName: "operator", password: "operator", roleName: operatorRoleName);

        // Minimalna asercja: seed nie kończy się no-opem na pustej bazie.
        Assert.NotNull(await userManager.FindByNameAsync("menago"));
        Assert.NotNull(await userManager.FindByNameAsync("operator"));
    }

    private static bool IsEnabled()
    {
        var flag = Environment.GetEnvironmentVariable("KANBANLITE_RUN_DB_SEED_TEST");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureRoleExists(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Nie udało się utworzyć roli '{roleName}': {Format(result)}");
        }
    }

    private static async Task EnsureUserInRole(
        UserManager<ApplicationUser> userManager,
        string userName,
        string password,
        string roleName)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = userName,
                Email = $"{userName}@local",
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Nie udało się utworzyć usera '{userName}': {Format(createResult)}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var addToRoleResult = await userManager.AddToRoleAsync(user, roleName);
            if (!addToRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Nie udało się przypisać roli '{roleName}' do usera '{userName}': {Format(addToRoleResult)}");
            }
        }
    }

    private static string Format(IdentityResult result)
        => string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
