using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using static KanbanLite.Application.Security.AuthorizationHelper;

namespace KanbanLite.Application.Services;

public sealed class DashboardService(
    IDashboardRepository dashboardRepository,
    ICurrentUser currentUser) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do podglądu dashboardu.");
        if (authError is not null)
        {
            return Result<DashboardDto>.Fail(authError);
        }

        try
        {
            var statusCounts = await dashboardRepository.GetStatusCountsForActiveProjectsAsync(ct);
            var stageCounts = await dashboardRepository.GetStageCountsForActiveProjectsAsync(ct);

            var countsByStatus = Enum.GetValues<BatchStatus>().ToDictionary(x => x, x => statusCounts.GetValueOrDefault(x.ToString(), 0));
            var countsByStage = Enum.GetValues<ProductionStage>().ToDictionary(x => x, x => stageCounts.GetValueOrDefault((short)x, 0));

            var inProgressCount = countsByStatus.GetValueOrDefault(BatchStatus.InProgress, 0);
            var warnings = CreateSoftLimitWarnings(inProgressCount);

            var urgentThreshold = DateTime.UtcNow.Date.AddDays(7);
            var urgent = await dashboardRepository.GetUrgentOrdersAsync(urgentThreshold, ct);

            return Result<DashboardDto>.Ok(new DashboardDto(
                countsByStatus,
                countsByStage,
                urgent,
                warnings
            ));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    private static IReadOnlyList<WarningDto> CreateSoftLimitWarnings(int inProgressCount)
        => inProgressCount > 20
            ? [new WarningDto("InProgressSoftLimitExceeded", "Przekroczono soft limit 20 batchy w statusie InProgress.")]
            : [];
}

