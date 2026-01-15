using KanbanLite.Application.Services;
using KanbanLite.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanLite.Application;

/// <summary>
/// Rejestracja serwisów warstwy Application.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Rejestruje wszystkie serwisy Application (BatchService, ProjectService, OrderService, itd.).
    /// </summary>
    /// <param name="services">Kontener DI.</param>
    /// <returns>Ten sam kontener dla fluent API.</returns>
    /// <remarks>
    /// <para>
    /// Wymaga rejestracji <see cref="ICurrentUser"/> w hoście (np. Blazor Server):
    /// </para>
    /// <code>
    /// services.AddHttpContextAccessor(); // dla ASP.NET Core/Blazor Server
    /// services.AddScoped&lt;ICurrentUser, ClaimsPrincipalCurrentUser&gt;();
    /// services.AddKanbanLiteApplication();
    /// </code>
    /// <para>
    /// Alternatywnie w Blazor Server można użyć <see cref="Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider"/>
    /// do implementacji <see cref="ICurrentUser"/>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddKanbanLiteApplication(this IServiceCollection services)
    {
        services.AddScoped<IBatchService, BatchService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IBatchAuditService, BatchAuditService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductFormatService, ProductFormatService>();
        services.AddScoped<IBatchSplitRuleService, BatchSplitRuleService>();

        return services;
    }
}

