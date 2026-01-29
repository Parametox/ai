using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;

namespace KanbanLite.Application.Services;

public sealed class BatchAuditService(
    IBatchAuditRepository auditRepository,
    IBatchRepository batchRepository,
    ICurrentUser currentUser) : IBatchAuditService
{
    private static readonly string[] AllowedRoles = ["Manager", "Operator"];

    public async Task<Result<PagedResult<BatchAuditEventDto>>> GetForBatchAsync(
        long batchId,
        PageQuery page,
        CancellationToken ct = default)
    {
        if (page is null)
        {
            return Result<PagedResult<BatchAuditEventDto>>.Fail(
                AppError.ValidationFailed("Brak parametrów stronicowania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["page"] = ["PageQuery jest wymagany."]
                }));
        }

        var authError = EnsureAuthorized();
        if (authError is not null)
        {
            return Result<PagedResult<BatchAuditEventDto>>.Fail(authError);
        }

        var pageNo = page.Page < 1 ? 1 : page.Page;
        var pageSize = page.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => page.PageSize
        };

        try
        {
            var batch = await batchRepository.GetByIdAsync(batchId, ct);
            if (batch is null)
            {
                return Result<PagedResult<BatchAuditEventDto>>.Fail(AppError.NotFound($"Batch o id={batchId} nie istnieje."));
            }

            var (items, total) = await auditRepository.GetForBatchAsync(batchId, pageNo, pageSize, ct);

            var mappedItems = items.Select(x => new BatchAuditEventDto(
                x.Id,
                x.BatchId,
                x.ChangedAt,
                x.ChangedByUserId ?? "",
                Enum.TryParse<BatchStatus>(x.OldStatus, out var oldStatus) ? oldStatus : null,
                Enum.TryParse<BatchStatus>(x.NewStatus, out var newStatus) ? newStatus : null,
                x.OldStage.HasValue ? (ProductionStage)x.OldStage.Value : null,
                x.NewStage.HasValue ? (ProductionStage)x.NewStage.Value : null
            )).ToList();

            return Result<PagedResult<BatchAuditEventDto>>.Ok(new PagedResult<BatchAuditEventDto>(
                mappedItems,
                pageNo,
                pageSize,
                total
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<PagedResult<BatchAuditEventDto>>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania audytu batcha."));
        }
    }

    private AppError? EnsureAuthorized()
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return AppError.Unauthorized("Użytkownik nie jest zalogowany.");
        }

        foreach (var role in AllowedRoles)
        {
            if (currentUser.IsInRole(role))
            {
                return null;
            }
        }

        return AppError.Forbidden("Brak wymaganych uprawnień (Manager lub Operator).");
    }
}

