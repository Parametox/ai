using DataAccess;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;

namespace KanbanLite.Application.Services;

public sealed class BatchAuditService(AppDbContext db, ICurrentUser currentUser) : IBatchAuditService
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
            var batchExists = await db.Batches.AsNoTracking().AnyAsync(x => x.Id == batchId, ct);
            if (!batchExists)
            {
                return Result<PagedResult<BatchAuditEventDto>>.Fail(AppError.NotFound($"Batch o id={batchId} nie istnieje."));
            }

            var q = db.BatchAuditLog.AsNoTracking().Where(x => x.BatchId == batchId);
            var total = await q.LongCountAsync(ct);

            var items = await q
                .OrderByDescending(x => x.ChangedAt)
                .ThenByDescending(x => x.Id)
                .Skip((pageNo - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new BatchAuditEventDto(
                    x.Id,
                    x.BatchId,
                    x.ChangedAt,
                    x.ChangedByUserId,
                    x.OldStatus,
                    x.NewStatus,
                    x.OldStage,
                    x.NewStage
                ))
                .ToListAsync(ct);

            return Result<PagedResult<BatchAuditEventDto>>.Ok(new PagedResult<BatchAuditEventDto>(
                items,
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

        return AppError.Forbidden("Brak uprawnień do podglądu audytu batcha.");
    }
}

