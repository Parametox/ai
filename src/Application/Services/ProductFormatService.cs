using DataAccess;
using DataAccess.Entities;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;

namespace KanbanLite.Application.Services;

public sealed class ProductFormatService(AppDbContext db, ICurrentUser currentUser) : IProductFormatService
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

        var authError = EnsureManagerAuthorized();
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

        var q = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();

        try
        {
            var formats = db.ProductFormats.AsNoTracking();

            if (query.IsActive is not null)
            {
                var isActive = query.IsActive.Value;
                formats = formats.Where(x => x.IsActive == isActive);
            }

            if (q is not null)
            {
                formats = formats.Where(x => x.Name.Contains(q));
            }

            var total = await formats.LongCountAsync(ct);

            var items = await formats
                .OrderBy(x => x.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProductFormatDto(x.Id, x.Name, x.IsActive, x.CreatedAt))
                .ToListAsync(ct);

            return Result<PagedResult<ProductFormatDto>>.Ok(new PagedResult<ProductFormatDto>(
                items,
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
            return Result<PagedResult<ProductFormatDto>>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania formatów produktów."));
        }
    }

    public async Task<Result<IReadOnlyList<ProductFormatLookupDto>>> GetActiveLookupAsync(CancellationToken ct = default)
    {
        var authError = EnsureAuthenticated();
        if (authError is not null)
        {
            return Result<IReadOnlyList<ProductFormatLookupDto>>.Fail(authError);
        }

        try
        {
            var items = await db.ProductFormats.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ProductFormatLookupDto(x.Id, x.Name))
                .ToListAsync(ct);

            return Result<IReadOnlyList<ProductFormatLookupDto>>.Ok(items);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<IReadOnlyList<ProductFormatLookupDto>>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania lookupu formatów produktów."));
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

        var authError = EnsureManagerAuthorized();
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
            var now = DateTimeOffset.UtcNow;
            var entity = new ProductFormat
            {
                Name = name,
                IsActive = request.IsActive,
                CreatedAt = now
            };

            db.ProductFormats.Add(entity);
            await db.SaveChangesAsync(ct);

            return Result<ProductFormatDto>.Ok(new ProductFormatDto(entity.Id, entity.Name, entity.IsActive, entity.CreatedAt));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return Result<ProductFormatDto>.Fail(
                AppError.Conflict("Nie udało się utworzyć formatu (prawdopodobnie nazwa już istnieje)."));
        }
        catch (Exception)
        {
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

        var authError = EnsureManagerAuthorized();
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
            var entity = await db.ProductFormats.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
            {
                return Result<ProductFormatDto>.Fail(AppError.NotFound($"Format produktu o id={id} nie istnieje."));
            }

            entity.Name = name;
            entity.IsActive = request.IsActive;

            await db.SaveChangesAsync(ct);

            return Result<ProductFormatDto>.Ok(new ProductFormatDto(entity.Id, entity.Name, entity.IsActive, entity.CreatedAt));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            return Result<ProductFormatDto>.Fail(
                AppError.Conflict("Nie udało się zaktualizować formatu (prawdopodobnie nazwa już istnieje)."));
        }
        catch (Exception)
        {
            return Result<ProductFormatDto>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas aktualizacji formatu produktu."));
        }
    }

    public async Task<Result<bool>> DeactivateAsync(long id, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized();
        if (authError is not null)
        {
            return Result<bool>.Fail(authError);
        }

        try
        {
            var entity = await db.ProductFormats.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
            {
                return Result<bool>.Fail(AppError.NotFound($"Format produktu o id={id} nie istnieje."));
            }

            if (!entity.IsActive)
            {
                return Result<bool>.Ok(false);
            }

            entity.IsActive = false;
            await db.SaveChangesAsync(ct);
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

    private AppError? EnsureManagerAuthorized()
    {
        var authError = EnsureAuthenticated();
        if (authError is not null)
        {
            return authError;
        }

        return currentUser.IsInRole("Manager")
            ? null
            : AppError.Forbidden("Brak uprawnień do zarządzania formatami produktów.");
    }

    private AppError? EnsureAuthenticated()
        => string.IsNullOrWhiteSpace(currentUser.UserId)
            ? AppError.Unauthorized("Użytkownik nie jest zalogowany.")
            : null;
}

