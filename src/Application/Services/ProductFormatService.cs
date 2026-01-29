using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using static KanbanLite.Application.Security.AuthorizationHelper;

namespace KanbanLite.Application.Services;

public sealed class ProductFormatService(
    IProductFormatRepository repository,
    ICurrentUser currentUser) : IProductFormatService
{
    public async Task<Result<PagedResult<ProductFormatDto>>> GetAsync(ProductFormatQuery query, CancellationToken ct = default)
    {
        if (query is null)
        {
            return Result<PagedResult<ProductFormatDto>>.Fail(
                AppError.ValidationFailed("Brak parametrów zapytania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["query"] = ["Query jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania formatami produktów.");
        if (authError is not null)
        {
            return Result<PagedResult<ProductFormatDto>>.Fail(authError);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => query.PageSize
        };

        try
        {
            var (items, total) = await repository.GetAsync(query, page, pageSize, ct);

            var dtos = items.Select(x => new ProductFormatDto(x.Id, x.Name ?? "", x.IsActive, x.CreatedAt)).ToList();

            return Result<PagedResult<ProductFormatDto>>.Ok(new PagedResult<ProductFormatDto>(
                dtos,
                page,
                pageSize,
                total
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Result<PagedResult<ProductFormatDto>>.Fail(
                AppError.Unexpected($"Nieoczekiwany błąd podczas pobierania formatów produktów: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<ProductFormatLookupDto>>> GetActiveLookupAsync(CancellationToken ct = default)
    {
        var authError = EnsureAuthenticated(currentUser);
        if (authError is not null)
        {
            return Result<IReadOnlyList<ProductFormatLookupDto>>.Fail(authError);
        }

        try
        {
            var items = await repository.GetActiveLookupAsync(ct);
            var dtos = items.Select(x => new ProductFormatLookupDto(x.Id, x.Name ?? "")).ToList();

            return Result<IReadOnlyList<ProductFormatLookupDto>>.Ok(dtos);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return Result<IReadOnlyList<ProductFormatLookupDto>>.Fail(
                AppError.Unexpected($"Nieoczekiwany błąd podczas pobierania lookupu formatów produktów: {ex.Message}"));
        }
    }

    public async Task<Result<ProductFormatDto>> CreateAsync(CreateProductFormatRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<ProductFormatDto>.Fail(
                AppError.ValidationFailed("Brak payloadu żądania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["request"] = ["Request jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania formatami produktów.");
        if (authError is not null)
        {
            return Result<ProductFormatDto>.Fail(authError);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return Result<ProductFormatDto>.Fail(AppError.ValidationFailed(
                "Nieprawidłowa nazwa formatu produktu.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["name"] = ["Wymagane; max 200 znaków."]
                }));
        }

        try
        {
            // Note: DB unique constraint handling via Supabase usually returns specific error code 23505

            var now = DateTimeOffset.UtcNow;
            var entity = new SupabaseProductFormat
            {
                Name = name,
                IsActive = request.IsActive,
                CreatedAt = now
            };

            await repository.CreateAsync(entity, ct);

            return Result<ProductFormatDto>.Ok(new ProductFormatDto(entity.Id, entity.Name ?? "", entity.IsActive, entity.CreatedAt));
        }
        catch (Exception ex)
        {
            // Checking for unique constraint violation in Supabase/Postgrest exception is possible but keeping it generic for now or checking message
            if (ex.Message.Contains("23505")) // PostgreSQL unique violation code
            {
                return Result<ProductFormatDto>.Fail(
                   AppError.Conflict("Nie udało się utworzyć formatu (prawdopodobnie nazwa już istnieje)."));
            }

            return Result<ProductFormatDto>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas tworzenia formatu produktu."));
        }
    }

    public async Task<Result<ProductFormatDto>> UpdateAsync(long id, UpdateProductFormatRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<ProductFormatDto>.Fail(
                AppError.ValidationFailed("Brak payloadu żądania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["request"] = ["Request jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania formatami produktów.");
        if (authError is not null)
        {
            return Result<ProductFormatDto>.Fail(authError);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            return Result<ProductFormatDto>.Fail(AppError.ValidationFailed(
                "Nieprawidłowa nazwa formatu produktu.",
                new Dictionary<string, IReadOnlyList<string>>
                {
                    ["name"] = ["Wymagane; max 200 znaków."]
                }));
        }

        try
        {
            var entity = await repository.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Result<ProductFormatDto>.Fail(AppError.NotFound($"Format produktu o id={id} nie istnieje."));
            }

            entity.Name = name;
            entity.IsActive = request.IsActive;

            await repository.UpdateAsync(entity, ct);

            return Result<ProductFormatDto>.Ok(new ProductFormatDto(entity.Id, entity.Name ?? "", entity.IsActive, entity.CreatedAt));
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("23505"))
            {
                return Result<ProductFormatDto>.Fail(
                   AppError.Conflict("Nie udało się zaktualizować formatu (prawdopodobnie nazwa już istnieje)."));
            }
            return Result<ProductFormatDto>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas aktualizacji formatu produktu."));
        }
    }

    public async Task<Result<bool>> DeactivateAsync(long id, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do zarządzania formatami produktów.");
        if (authError is not null)
        {
            return Result<bool>.Fail(authError);
        }

        try
        {
            var entity = await repository.GetByIdAsync(id, ct);
            if (entity is null)
            {
                return Result<bool>.Fail(AppError.NotFound($"Format produktu o id={id} nie istnieje."));
            }

            if (!entity.IsActive)
            {
                return Result<bool>.Ok(false);
            }

            entity.IsActive = false;
            await repository.UpdateAsync(entity, ct);
            return Result<bool>.Ok(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<bool>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas dezaktywacji formatu produktu."));
        }
    }
}
