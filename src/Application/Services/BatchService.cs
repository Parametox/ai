using DataAccess;
using DataAccess.Entities;
using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;

namespace KanbanLite.Application.Services;

public sealed class BatchService(AppDbContext db, ICurrentUser currentUser) : IBatchService
{
    private static readonly string[] AllowedRoles = ["Manager", "Operator"];

    public async Task<Result<KanbanBatchesResult>> GetKanbanAsync(KanbanQuery query, CancellationToken ct = default)
    {
        if (query is null)
        {
            return Result<KanbanBatchesResult>.Fail(
                AppError.ValidationFailed("Brak parametrów zapytania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["query"] = ["Query jest wymagany."]
                }));
        }

        var authError = EnsureAuthorized();
        if (authError is not null)
        {
            return Result<KanbanBatchesResult>.Fail(authError);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => query.PageSize
        };

        var q = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();

        try
        {
            // Projekcja join: batches + projects + orders (bez N+1).
            var baseQuery =
                from b in db.Batches.AsNoTracking()
                join p in db.Projects.AsNoTracking() on b.ProjectId equals p.Id
                join o in db.Orders.AsNoTracking() on p.OrderId equals o.Id
                where !p.IsCompleted
                select new { b, p, o };

            if (query.Status is not null)
            {
                var status = query.Status.Value;
                baseQuery = baseQuery.Where(x => x.b.Status == status);
            }

            if (query.Stage is not null)
            {
                var stage = query.Stage.Value;
                baseQuery = baseQuery.Where(x => x.b.Stage == stage);
            }

            if (q is not null)
            {
                // Minimalna wyszukiwarka: po orderNumber i projectNumber (contains).
                baseQuery = baseQuery.Where(x => x.o.OrderNumber.Contains(q) || x.p.ProjectNumber.Contains(q));
            }

            baseQuery = (query.Sort ?? KanbanSort.UpdatedAtDesc) switch
            {
                KanbanSort.DueDateAsc => baseQuery.OrderBy(x => x.o.DueDate).ThenByDescending(x => x.b.UpdatedAt),
                KanbanSort.DueDateDesc => baseQuery.OrderByDescending(x => x.o.DueDate).ThenByDescending(x => x.b.UpdatedAt),
                KanbanSort.UpdatedAtDesc => baseQuery.OrderByDescending(x => x.b.UpdatedAt),
                _ => baseQuery.OrderByDescending(x => x.b.UpdatedAt)
            };

            var total = await baseQuery.LongCountAsync(ct);

            var items = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new KanbanBatchDto(
                    x.b.Id,
                    x.p.Id,
                    x.p.ProjectNumber,
                    x.o.OrderNumber,
                    x.o.DueDate,
                    x.b.BatchNo,
                    x.b.Quantity,
                    x.b.Status,
                    x.b.Stage,
                    ProgressPercentFromStage(x.b.Stage),
                    x.b.UpdatedAt
                ))
                .ToListAsync(ct);

            var inProgressCount = await db.Batches.AsNoTracking().CountAsync(x => x.Status == BatchStatus.InProgress, ct);
            var warnings = CreateSoftLimitWarnings(inProgressCount);

            return Result<KanbanBatchesResult>.Ok(new KanbanBatchesResult(
                items,
                inProgressCount,
                warnings,
                page,
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
            return Result<KanbanBatchesResult>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania Kanbanu."));
        }
    }

    public async Task<Result<UpdateBatchStatusResult>> UpdateStatusAsync(
        long batchId,
        UpdateBatchStatusRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<UpdateBatchStatusResult>.Fail(
                AppError.ValidationFailed("Brak payloadu żądania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["request"] = ["Request jest wymagany."]
                }));
        }

        var authError = EnsureAuthorized();
        if (authError is not null)
        {
            return Result<UpdateBatchStatusResult>.Fail(authError);
        }

        try
        {
            var batch = await db.Batches.SingleOrDefaultAsync(x => x.Id == batchId, ct);
            if (batch is null)
            {
                return Result<UpdateBatchStatusResult>.Fail(AppError.NotFound($"Batch o id={batchId} nie istnieje."));
            }

            var oldStatus = batch.Status;
            var newStatus = request.NewStatus;

            var validationError = ValidateStatusTransition(oldStatus, newStatus);
            if (validationError is not null)
            {
                return Result<UpdateBatchStatusResult>.Fail(validationError);
            }

            if (oldStatus == newStatus)
            {
                // No-op (ale zwracamy spójny wynik: stan + soft-limit).
                var count = await db.Batches.CountAsync(x => x.Status == BatchStatus.InProgress, ct);
                var warnings = CreateSoftLimitWarnings(count);
                return Result<UpdateBatchStatusResult>.Ok(new UpdateBatchStatusResult(
                    BatchId: batch.Id,
                    OldStatus: oldStatus,
                    NewStatus: newStatus,
                    Stage: batch.Stage,
                    UpdatedAt: batch.UpdatedAt,
                    InProgressCount: count,
                    Warnings: warnings
                ));
            }

            var now = DateTimeOffset.UtcNow;

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            if (SupportsTransactions())
            {
                // InMemory provider nie wspiera transakcji; w runtime używamy Npgsql, więc transakcja zadziała.
                tx = await db.Database.BeginTransactionAsync(ct);
            }

            try
            {
            batch.Status = newStatus;
            batch.UpdatedAt = now;

            db.BatchAuditLog.Add(new BatchAuditLog
            {
                BatchId = batch.Id,
                ChangedAt = now,
                ChangedByUserId = currentUser.UserId!,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                OldStage = null,
                NewStage = null
            });

            await db.SaveChangesAsync(ct);

            var inProgressCount = await db.Batches.CountAsync(x => x.Status == BatchStatus.InProgress, ct);
            var warningsAfter = CreateSoftLimitWarnings(inProgressCount);

            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            return Result<UpdateBatchStatusResult>.Ok(new UpdateBatchStatusResult(
                BatchId: batch.Id,
                OldStatus: oldStatus,
                NewStatus: newStatus,
                Stage: batch.Stage,
                UpdatedAt: batch.UpdatedAt,
                InProgressCount: inProgressCount,
                Warnings: warningsAfter
            ));
            }
            finally
            {
                if (tx is not null)
                {
                    await tx.DisposeAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return Result<UpdateBatchStatusResult>.Fail(
                AppError.Conflict("Nie udało się zapisać zmiany statusu (konflikt lub naruszenie ograniczeń danych)."));
        }
        catch (Exception)
        {
            return Result<UpdateBatchStatusResult>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas zmiany statusu batcha."));
        }
    }

    public async Task<Result<UpdateBatchStageResult>> UpdateStageAsync(
        long batchId,
        UpdateBatchStageRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<UpdateBatchStageResult>.Fail(
                AppError.ValidationFailed("Brak payloadu żądania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["request"] = ["Request jest wymagany."]
                }));
        }

        var authError = EnsureAuthorized();
        if (authError is not null)
        {
            return Result<UpdateBatchStageResult>.Fail(authError);
        }

        try
        {
            var batch = await db.Batches.SingleOrDefaultAsync(x => x.Id == batchId, ct);
            if (batch is null)
            {
                return Result<UpdateBatchStageResult>.Fail(AppError.NotFound($"Batch o id={batchId} nie istnieje."));
            }

            var oldStage = batch.Stage;
            var newStage = request.NewStage;

            var validationError = ValidateStageTransition(oldStage, newStage);
            if (validationError is not null)
            {
                return Result<UpdateBatchStageResult>.Fail(validationError);
            }

            if (oldStage == newStage)
            {
                return Result<UpdateBatchStageResult>.Ok(new UpdateBatchStageResult(
                    BatchId: batch.Id,
                    OldStage: oldStage,
                    NewStage: newStage,
                    Status: batch.Status,
                    ProgressPercent: ProgressPercentFromStage(batch.Stage),
                    UpdatedAt: batch.UpdatedAt
                ));
            }

            var now = DateTimeOffset.UtcNow;

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            if (SupportsTransactions())
            {
                tx = await db.Database.BeginTransactionAsync(ct);
            }

            try
            {
            batch.Stage = newStage;
            batch.UpdatedAt = now;

            db.BatchAuditLog.Add(new BatchAuditLog
            {
                BatchId = batch.Id,
                ChangedAt = now,
                ChangedByUserId = currentUser.UserId!,
                OldStatus = null,
                NewStatus = null,
                OldStage = oldStage,
                NewStage = newStage
            });

            await db.SaveChangesAsync(ct);
            if (tx is not null)
            {
                await tx.CommitAsync(ct);
            }

            return Result<UpdateBatchStageResult>.Ok(new UpdateBatchStageResult(
                BatchId: batch.Id,
                OldStage: oldStage,
                NewStage: newStage,
                Status: batch.Status,
                ProgressPercent: ProgressPercentFromStage(batch.Stage),
                UpdatedAt: batch.UpdatedAt
            ));
            }
            finally
            {
                if (tx is not null)
                {
                    await tx.DisposeAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return Result<UpdateBatchStageResult>.Fail(
                AppError.Conflict("Nie udało się zapisać zmiany etapu (konflikt lub naruszenie ograniczeń danych)."));
        }
        catch (Exception)
        {
            return Result<UpdateBatchStageResult>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas zmiany etapu batcha."));
        }
    }

    public async Task<Result<int>> GetInProgressCountAsync(CancellationToken ct = default)
    {
        var authError = EnsureAuthorized();
        if (authError is not null)
        {
            return Result<int>.Fail(authError);
        }

        try
        {
            var count = await db.Batches.AsNoTracking().CountAsync(x => x.Status == BatchStatus.InProgress, ct);
            return Result<int>.Ok(count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<int>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas liczenia batchy InProgress."));
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

        return AppError.Forbidden("Brak uprawnień do operacji na batchach.");
    }

    private bool SupportsTransactions()
        => !string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

    private static AppError? ValidateStatusTransition(BatchStatus oldStatus, BatchStatus newStatus)
    {
        if (oldStatus == newStatus)
        {
            return null;
        }

        var isAllowed =
            (oldStatus == BatchStatus.New && newStatus == BatchStatus.InProgress)
            || (oldStatus == BatchStatus.InProgress && newStatus == BatchStatus.Done);

        if (isAllowed)
        {
            return null;
        }

        return AppError.ValidationFailed(
            "Niedozwolone przejście statusu batcha.",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["newStatus"] = [$"Dozwolone przejścia: New→InProgress→Done. Aktualne: {oldStatus}→{newStatus}."]
            });
    }

    private static AppError? ValidateStageTransition(ProductionStage oldStage, ProductionStage newStage)
    {
        if (oldStage == newStage)
        {
            return null;
        }

        if (newStage < ProductionStage.Design || newStage > ProductionStage.Ship)
        {
            return AppError.ValidationFailed(
                "Nieprawidłowy etap produkcji.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["newStage"] = ["Etap musi być w zakresie 1..5."]
                });
        }

        if (newStage < oldStage)
        {
            return AppError.ValidationFailed(
                "Niedozwolone przejście etapu batcha (cofanie jest zabronione).",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["newStage"] = [$"Dozwolone przejście: tylko do przodu. Aktualne: {oldStage}→{newStage}."]
                });
        }

        return null;
    }

    private static int ProgressPercentFromStage(ProductionStage stage)
        => stage switch
        {
            ProductionStage.Design => 20,
            ProductionStage.Print => 40,
            ProductionStage.Cut => 60,
            ProductionStage.Pack => 80,
            ProductionStage.Ship => 100,
            _ => 0
        };

    private static IReadOnlyList<WarningDto> CreateSoftLimitWarnings(int inProgressCount)
        => inProgressCount > 20
            ? [new WarningDto("InProgressSoftLimitExceeded", "Przekroczono soft limit 20 batchy w statusie InProgress.")]
            : [];
}

