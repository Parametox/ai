using DataAccess;
using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;

namespace KanbanLite.Application.Services;

public sealed class DashboardService(AppDbContext db, ICurrentUser currentUser) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized();
        if (authError is not null)
        {
            return Result<DashboardDto>.Fail(authError);
        }

        try
        {
            // Dashboard operacyjny: liczymy na projektach aktywnych (is_completed=false), żeby nie zawyżać metryk.
            var activeProjectIds = db.Projects.AsNoTracking().Where(p => !p.IsCompleted).Select(p => p.Id);

            var countsByStatus = Enum.GetValues<BatchStatus>().ToDictionary(x => x, _ => 0);
            var statusAgg = await db.Batches.AsNoTracking()
                .Where(b => activeProjectIds.Contains(b.ProjectId))
                .GroupBy(b => b.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            foreach (var x in statusAgg)
            {
                countsByStatus[x.Status] = x.Count;
            }

            var countsByStage = Enum.GetValues<ProductionStage>().ToDictionary(x => x, _ => 0);
            var stageAgg = await db.Batches.AsNoTracking()
                .Where(b => activeProjectIds.Contains(b.ProjectId))
                .GroupBy(b => b.Stage)
                .Select(g => new { Stage = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            foreach (var x in stageAgg)
            {
                countsByStage[x.Stage] = x.Count;
            }

            var inProgressCount = countsByStatus.GetValueOrDefault(BatchStatus.InProgress, 0);
            var warnings = CreateSoftLimitWarnings(inProgressCount);

            var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            var urgentThreshold = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7));

            var urgent = await (
                from p in db.Projects.AsNoTracking()
                join o in db.Orders.AsNoTracking() on p.OrderId equals o.Id
                where !p.IsCompleted
                where o.DueDate < urgentThreshold && o.DueDate >= today
                orderby o.DueDate, o.Id
                select new DashboardUrgentOrderDto(
                    o.Id,
                    o.OrderNumber,
                    o.DueDate,
                    p.Id,
                    p.ProjectNumber
                )
            )
            .Take(50)
            .ToListAsync(ct);

            return Result<DashboardDto>.Ok(new DashboardDto(
                countsByStatus,
                countsByStage,
                urgent,
                warnings
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<DashboardDto>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas pobierania dashboardu."));
        }
    }

    private AppError? EnsureManagerAuthorized()
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return AppError.Unauthorized("Użytkownik nie jest zalogowany.");
        }

        return currentUser.IsInRole("Manager")
            ? null
            : AppError.Forbidden("Brak uprawnień do podglądu dashboardu.");
    }

    private static IReadOnlyList<WarningDto> CreateSoftLimitWarnings(int inProgressCount)
        => inProgressCount > 20
            ? [new WarningDto("InProgressSoftLimitExceeded", "Przekroczono soft limit 20 batchy w statusie InProgress.")]
            : [];
}

