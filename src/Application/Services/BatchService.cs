using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using Postgrest;

namespace KanbanLite.Application.Services;

public sealed class BatchService(IBatchRepository batchRepository, ICurrentUser currentUser) : IBatchService
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
            < 1 => 20,
            > 200 => 200,
            _ => query.PageSize
        };

        try
        {
            var (items, total) = await batchRepository.GetBatchesAsync(query, page, pageSize, ct);

            // Map to DTO
            var resultItems = items.Select(b => new KanbanBatchDto(
                b.Id,
                b.ProjectId,
                b.Project?.ProjectNumber ?? "",
                b.Project?.Order?.OrderNumber ?? "",
                b.Project?.Order != null ? DateOnly.FromDateTime(b.Project.Order.DueDate) : DateOnly.MinValue,
                b.BatchNo,
                b.Quantity,
                Enum.TryParse<BatchStatus>(b.Status, out var status) ? status : BatchStatus.New,
                (ProductionStage)b.Stage,
                ProgressPercentFromStage((ProductionStage)b.Stage),
                b.UpdatedAt
            )).ToList();

            var inProgressCount = await batchRepository.GetInProgressCountAsync(ct);

            var warnings = CreateSoftLimitWarnings(inProgressCount);

            return Result<KanbanBatchesResult>.Ok(new KanbanBatchesResult(
                resultItems,
                inProgressCount,
                warnings,
                page,
                pageSize,
                total
            ));
        }
        catch (Exception ex)
        {
            // Logging would be good here
            return Result<KanbanBatchesResult>.Fail(
                AppError.Unexpected($"Błąd: {ex.Message}"));
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
            var batch = await batchRepository.GetByIdAsync(batchId, ct);
            if (batch is null)
            {
                 return Result<UpdateBatchStatusResult>.Fail(AppError.NotFound($"Batch o id={batchId} nie istnieje."));
            }

            if (!Enum.TryParse<BatchStatus>(batch.Status, out var oldStatus)) oldStatus = BatchStatus.New;
            var newStatus = request.NewStatus;

            var validationError = ValidateStatusTransition(oldStatus, newStatus);
            if (validationError is not null)
            {
                return Result<UpdateBatchStatusResult>.Fail(validationError);
            }

            if (oldStatus == newStatus)
            {
                var count = await batchRepository.GetInProgressCountAsync(ct);
                
                var warnings = CreateSoftLimitWarnings(count);
                return Result<UpdateBatchStatusResult>.Ok(new UpdateBatchStatusResult(
                    BatchId: batch.Id,
                    OldStatus: oldStatus,
                    NewStatus: newStatus,
                    Stage: (ProductionStage)batch.Stage,
                    UpdatedAt: batch.UpdatedAt,
                    InProgressCount: count,
                    Warnings: warnings
                ));
            }

            var now = DateTimeOffset.UtcNow;
            
            await batchRepository.UpdateStatusAsync(batchId, newStatus.ToString(), now, ct);

            var log = new SupabaseBatchAuditLog
            {
                BatchId = batch.Id,
                ChangedAt = now,
                ChangedByUserId = currentUser.UserId!,
                OldStatus = oldStatus.ToString(),
                NewStatus = newStatus.ToString(),
                OldStage = null, 
                NewStage = null
            };
            await batchRepository.AddAuditLogAsync(log, ct);

            // Count
            var inProgressCount = await batchRepository.GetInProgressCountAsync(ct);
            
            var warningsAfter = CreateSoftLimitWarnings(inProgressCount);

            return Result<UpdateBatchStatusResult>.Ok(new UpdateBatchStatusResult(
                BatchId: batch.Id,
                OldStatus: oldStatus,
                NewStatus: newStatus,
                Stage: (ProductionStage)batch.Stage,
                UpdatedAt: now,
                InProgressCount: inProgressCount,
                Warnings: warningsAfter
            ));
        }
        catch (Exception ex)
        {
            return Result<UpdateBatchStatusResult>.Fail(
                AppError.Unexpected($"Błąd: {ex.Message}"));
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
            var batch = await batchRepository.GetByIdAsync(batchId, ct);

            if (batch is null)
            {
                return Result<UpdateBatchStageResult>.Fail(AppError.NotFound($"Batch o id={batchId} nie istnieje."));
            }

            var oldStage = (ProductionStage)batch.Stage;
            var newStage = request.NewStage;

            var validationError = ValidateStageTransition(oldStage, newStage);
            if (validationError is not null)
            {
                return Result<UpdateBatchStageResult>.Fail(validationError);
            }

            if (oldStage == newStage)
            {
                Enum.TryParse<BatchStatus>(batch.Status, out var status);
                return Result<UpdateBatchStageResult>.Ok(new UpdateBatchStageResult(
                    BatchId: batch.Id,
                    OldStage: oldStage,
                    NewStage: newStage,
                    Status: status,
                    ProgressPercent: ProgressPercentFromStage(oldStage),
                    UpdatedAt: batch.UpdatedAt
                ));
            }

            var now = DateTimeOffset.UtcNow;

            await batchRepository.UpdateStageAsync(batchId, (short)newStage, now, ct);

            var log = new SupabaseBatchAuditLog
            {
                BatchId = batch.Id,
                ChangedAt = now,
                ChangedByUserId = currentUser.UserId!,
                OldStatus = null,
                NewStatus = null,
                OldStage = (short)oldStage,
                NewStage = (short)newStage
            };
            await batchRepository.AddAuditLogAsync(log, ct);

            Enum.TryParse<BatchStatus>(batch.Status, out var currentStatus);

            return Result<UpdateBatchStageResult>.Ok(new UpdateBatchStageResult(
                BatchId: batch.Id,
                OldStage: oldStage,
                NewStage: newStage,
                Status: currentStatus,
                ProgressPercent: ProgressPercentFromStage(newStage),
                UpdatedAt: now
            ));
        }
        catch (Exception ex)
        {
            return Result<UpdateBatchStageResult>.Fail(
                AppError.Unexpected($"Błąd: {ex.Message}"));
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
            var count = await batchRepository.GetInProgressCountAsync(ct);

            return Result<int>.Ok(count);
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

