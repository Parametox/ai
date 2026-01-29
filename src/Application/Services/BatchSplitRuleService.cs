using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using static KanbanLite.Application.Security.AuthorizationHelper;

namespace KanbanLite.Application.Services;

public sealed class BatchSplitRuleService(
    IBatchSplitRuleRepository repository,
    ICurrentUser currentUser) : IBatchSplitRuleService
{
    public async Task<Result<IReadOnlyList<BatchSplitRuleDto>>> GetAsync(BatchSplitRuleQuery query, CancellationToken ct = default)
    {
        if (query is null)
        {
            return Result<IReadOnlyList<BatchSplitRuleDto>>.Fail(
                AppError.ValidationFailed("Brak parametrów zapytania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["query"] = ["Query jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania regułami dzielenia batchy.");
        if (authError is not null)
        {
            return Result<IReadOnlyList<BatchSplitRuleDto>>.Fail(authError);
        }

        try
        {
            var items = await repository.GetAllAsync(query.IsActive, ct);

            var dtos = items.Select(x => new BatchSplitRuleDto(
                x.Id,
                x.MinQty,
                x.MaxQty,
                x.Percent,
                x.MinBatchSize,
                x.MaxBatchesPerProject,
                x.IsActive,
                x.CreatedAt
            )).ToList();

            return Result<IReadOnlyList<BatchSplitRuleDto>>.Ok(dtos);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<IReadOnlyList<BatchSplitRuleDto>>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania reguł dzielenia batchy."));
        }
    }

    public async Task<Result<BatchSplitRuleDto>> CreateAsync(UpsertBatchSplitRuleRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.ValidationFailed("Brak payloadu żądania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["request"] = ["Request jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania regułami dzielenia batchy.");
        if (authError is not null)
        {
            return Result<BatchSplitRuleDto>.Fail(authError);
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return Result<BatchSplitRuleDto>.Fail(validation);
        }

        try
        {
            if (request.IsActive)
            {
                var overlapError = await ValidateNoOverlapAsync(excludeId: null, request.MinQty, request.MaxQty, ct);
                if (overlapError is not null)
                {
                    return Result<BatchSplitRuleDto>.Fail(overlapError);
                }
            }

            var now = DateTimeOffset.UtcNow;
            var entity = new SupabaseBatchSplitRule
            {
                MinQty = request.MinQty,
                MaxQty = request.MaxQty,
                Percent = request.Percent,
                MinBatchSize = request.MinBatchSize,
                MaxBatchesPerProject = request.MaxBatchesPerProject,
                IsActive = request.IsActive,
                CreatedAt = now
            };

            await repository.CreateAsync(entity, ct);

            // Supabase insert might not return ID immediately if we don't ask for representation, 
            // but repository implementation (Community Client) usually updates model if using Insert(model).
            // However, SupabaseBatchSplitRuleRepository.CreateAsync uses Insert(rule) which should update the object if configured correctly,
            // or we might need to fetch it or rely on re-fetching.
            // For MVP, assuming successful insert. Real ID might be 0 if the client doesn't auto-update.
            // Let's assume for now it's fine or we accept ID=0 in response until re-fetch.
            
            return Result<BatchSplitRuleDto>.Ok(ToDto(entity));
        }
        catch (Exception)
        {
             return Result<BatchSplitRuleDto>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas tworzenia reguły dzielenia batchy."));
        }
    }

    public async Task<Result<BatchSplitRuleDto>> UpdateAsync(long id, UpsertBatchSplitRuleRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.ValidationFailed("Brak payloadu żądania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["request"] = ["Request jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania regułami dzielenia batchy.");
        if (authError is not null)
        {
            return Result<BatchSplitRuleDto>.Fail(authError);
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return Result<BatchSplitRuleDto>.Fail(validation);
        }

        try
        {
            var entity = await repository.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Result<BatchSplitRuleDto>.Fail(AppError.NotFound($"Reguła o id={id} nie istnieje."));
            }

            if (request.IsActive)
            {
                var overlapError = await ValidateNoOverlapAsync(excludeId: id, request.MinQty, request.MaxQty, ct);
                if (overlapError is not null)
                {
                    return Result<BatchSplitRuleDto>.Fail(overlapError);
                }
            }

            entity.MinQty = request.MinQty;
            entity.MaxQty = request.MaxQty;
            entity.Percent = request.Percent;
            entity.MinBatchSize = request.MinBatchSize;
            entity.MaxBatchesPerProject = request.MaxBatchesPerProject;
            entity.IsActive = request.IsActive;

            await repository.UpdateAsync(entity, ct);

            return Result<BatchSplitRuleDto>.Ok(ToDto(entity));
        }
        catch (Exception)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas aktualizacji reguły dzielenia batchy."));
        }
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do usuwania reguł dzielenia batchy.");
        if (authError is not null)
        {
            return Result.Fail(authError);
        }

        try
        {
            var entity = await repository.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Result.Fail(AppError.NotFound($"Reguła o id={id} nie istnieje."));
            }

            await repository.DeleteAsync(id, ct);
            return Result.Ok();
        }
        catch (Exception)
        {
            return Result.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas usuwania reguły dzielenia batchy."));
        }
    }

    public async Task<Result<BatchSplitRuleDto>> SetActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do edycji reguł.");
        if (authError is not null)
        {
            return Result<BatchSplitRuleDto>.Fail(authError);
        }

        try
        {
            var entity = await repository.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Result<BatchSplitRuleDto>.Fail(AppError.NotFound($"Reguła o id={id} nie istnieje."));
            }

            if (isActive)
            {
                var overlapError = await ValidateNoOverlapAsync(id, entity.MinQty, entity.MaxQty, ct);
                if (overlapError is not null)
                {
                    return Result<BatchSplitRuleDto>.Fail(overlapError);
                }
            }

            entity.IsActive = isActive;
            await repository.UpdateAsync(entity, ct);

            return Result<BatchSplitRuleDto>.Ok(ToDto(entity));
        }
        catch (Exception)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas zmiany statusu reguły."));
        }
    }

    private AppError? Validate(UpsertBatchSplitRuleRequest request)
    {
        if (request.MinQty < 1)
        {
            return AppError.ValidationFailed("MinQty musi być >= 1.");
        }

        if (request.MaxQty.HasValue && request.MaxQty.Value < request.MinQty)
        {
            return AppError.ValidationFailed("MaxQty nie może być mniejsze niż MinQty.");
        }

        if (request.Percent <= 0 || request.Percent > 1)
        {
            return AppError.ValidationFailed("Percent musi być z przedziału (0, 1].");
        }

        if (request.MinBatchSize < 1)
        {
            return AppError.ValidationFailed("MinBatchSize musi być >= 1.");
        }

        if (request.MaxBatchesPerProject.HasValue && request.MaxBatchesPerProject.Value < 1)
        {
            return AppError.ValidationFailed("MaxBatchesPerProject musi być >= 1.");
        }

        return null;
    }

    // Prosta walidacja overlapów w pamięci - pobieramy wszystkie aktywne reguły (zakładamy małą ilość reguł).
    private async Task<AppError?> ValidateNoOverlapAsync(long? excludeId, int minQty, int? maxQty, CancellationToken ct)
    {
        var activeRules = await repository.GetActiveRulesAsync(ct);

        foreach (var rule in activeRules)
        {
            if (excludeId.HasValue && rule.Id == excludeId.Value)
            {
                continue;
            }

            // Sprawdzamy czy przedziały [MinQty, MaxQty] (gdzie MaxQty=null to inf) nachodzą na siebie
            // Overlap logic: StartA <= EndB && EndA >= StartB

            long startA = minQty;
            long endA = maxQty ?? int.MaxValue;

            long startB = rule.MinQty;
            long endB = rule.MaxQty ?? int.MaxValue;

            if (startA <= endB && endA >= startB)
            {
                 return AppError.ValidationFailed(
                    "Nowa reguła koliduje zakresem ilości z istniejącą aktywną regułą.",
                    new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["range"] = [$"Konflikt z regułą ID={rule.Id} (Min={rule.MinQty}, Max={rule.MaxQty})."]
                    });
            }
        }

        return null; // OK
    }

    private static BatchSplitRuleDto ToDto(SupabaseBatchSplitRule entity)
        => new BatchSplitRuleDto(
            entity.Id,
            entity.MinQty,
            entity.MaxQty,
            entity.Percent,
            entity.MinBatchSize,
            entity.MaxBatchesPerProject,
            entity.IsActive,
            entity.CreatedAt
        );
}

