using DataAccess;
using DataAccess.Entities;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;
using static KanbanLite.Application.Security.AuthorizationHelper;

namespace KanbanLite.Application.Services;

public sealed class BatchSplitRuleService(IDbContextFactory<AppDbContext> dbFactory, ICurrentUser currentUser) : IBatchSplitRuleService
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
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var q = db.BatchSplitRules.AsNoTracking();
            if (query.IsActive is not null)
            {
                var isActive = query.IsActive.Value;
                q = q.Where(x => x.IsActive == isActive);
            }

            var items = await q
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.MinQty)
                .ThenBy(x => x.MaxQty == null ? int.MaxValue : x.MaxQty.Value)
                .Select(x => new BatchSplitRuleDto(
                    x.Id,
                    x.MinQty,
                    x.MaxQty,
                    x.Percent,
                    x.MinBatchSize,
                    x.MaxBatchesPerProject,
                    x.IsActive,
                    x.CreatedAt
                ))
                .ToListAsync(ct);

            return Result<IReadOnlyList<BatchSplitRuleDto>>.Ok(items);
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
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            if (request.IsActive)
            {
                var overlapError = await ValidateNoOverlapAsync(db, excludeId: null, request.MinQty, request.MaxQty, ct);
                if (overlapError is not null)
                {
                    return Result<BatchSplitRuleDto>.Fail(overlapError);
                }
            }

            var now = DateTimeOffset.UtcNow;
            var entity = new BatchSplitRule
            {
                MinQty = request.MinQty,
                MaxQty = request.MaxQty,
                Percent = request.Percent,
                MinBatchSize = request.MinBatchSize,
                MaxBatchesPerProject = request.MaxBatchesPerProject,
                IsActive = request.IsActive,
                CreatedAt = now
            };

            db.BatchSplitRules.Add(entity);
            await db.SaveChangesAsync(ct);

            return Result<BatchSplitRuleDto>.Ok(ToDto(entity));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.Conflict("Nie udało się zapisać reguły dzielenia batchy (naruszenie ograniczeń danych)."));
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
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var entity = await db.BatchSplitRules.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
            {
                return Result<BatchSplitRuleDto>.Fail(AppError.NotFound($"Reguła dzielenia batchy o id={id} nie istnieje."));
            }

            if (request.IsActive)
            {
                var overlapError = await ValidateNoOverlapAsync(db, excludeId: id, request.MinQty, request.MaxQty, ct);
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

            await db.SaveChangesAsync(ct);

            return Result<BatchSplitRuleDto>.Ok(ToDto(entity));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.Conflict("Nie udało się zaktualizować reguły dzielenia batchy (naruszenie ograniczeń danych)."));
        }
        catch (Exception)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas aktualizacji reguły dzielenia batchy."));
        }
    }

    public async Task<Result<BatchSplitRuleDto>> SetActiveAsync(long id, bool isActive, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania regułami dzielenia batchy.");
        if (authError is not null)
        {
            return Result<BatchSplitRuleDto>.Fail(authError);
        }

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var entity = await db.BatchSplitRules.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
            {
                return Result<BatchSplitRuleDto>.Fail(AppError.NotFound($"Reguła dzielenia batchy o id={id} nie istnieje."));
            }

            if (isActive && !entity.IsActive)
            {
                var overlapError = await ValidateNoOverlapAsync(db, excludeId: id, entity.MinQty, entity.MaxQty, ct);
                if (overlapError is not null)
                {
                    return Result<BatchSplitRuleDto>.Fail(overlapError);
                }
            }

            entity.IsActive = isActive;
            await db.SaveChangesAsync(ct);

            return Result<BatchSplitRuleDto>.Ok(ToDto(entity));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.Conflict("Nie udało się zmienić aktywności reguły (naruszenie ograniczeń danych)."));
        }
        catch (Exception)
        {
            return Result<BatchSplitRuleDto>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas zmiany aktywności reguły dzielenia batchy."));
        }
    }

    private static AppError? Validate(UpsertBatchSplitRuleRequest request)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();

        if (request.MinQty < 1)
        {
            errors["minQty"] = ["MinQty musi być >= 1."];
        }

        if (request.MaxQty is not null && request.MaxQty.Value < request.MinQty)
        {
            errors["maxQty"] = ["MaxQty musi być >= MinQty (albo null)."];
        }

        if (request.Percent <= 0 || request.Percent > 100)
        {
            errors["percent"] = ["Percent musi być w zakresie (0..100]."];
        }

        if (request.MinBatchSize < 1)
        {
            errors["minBatchSize"] = ["MinBatchSize musi być >= 1."];
        }

        if (request.MaxBatchesPerProject is not null && request.MaxBatchesPerProject.Value < 1)
        {
            errors["maxBatchesPerProject"] = ["MaxBatchesPerProject musi być >= 1 (albo null)."];
        }

        return errors.Count > 0 ? AppError.ValidationFailed("Nieprawidłowe dane wejściowe.", errors) : null;
    }

    private static async Task<AppError?> ValidateNoOverlapAsync(AppDbContext db, long? excludeId, int minQty, int? maxQty, CancellationToken ct)
    {
        var candidates = db.BatchSplitRules.AsNoTracking().Where(x => x.IsActive);
        if (excludeId is not null)
        {
            var id = excludeId.Value;
            candidates = candidates.Where(x => x.Id != id);
        }

        // Logika nakładania się zakresów: minA <= bMax && minB <= aMax
        // Gdy maxQty jest null, traktujemy jako int.MaxValue
        var maxQtyValue = maxQty ?? int.MaxValue;
        var overlaps = await candidates
            .Where(x => x.MinQty <= maxQtyValue && minQty <= (x.MaxQty ?? int.MaxValue))
            .AnyAsync(ct);

        return overlaps
            ? AppError.ValidationFailed(
                "Zakres reguły nakłada się na inną aktywną regułę.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["minQty"] = ["Aktywne zakresy nie mogą się nakładać."],
                    ["maxQty"] = ["Aktywne zakresy nie mogą się nakładać."]
                })
            : null;
    }

    private static BatchSplitRuleDto ToDto(BatchSplitRule x)
        => new(
            x.Id,
            x.MinQty,
            x.MaxQty,
            x.Percent,
            x.MinBatchSize,
            x.MaxBatchesPerProject,
            x.IsActive,
            x.CreatedAt);
}

