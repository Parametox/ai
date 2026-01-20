using DataAccess;
using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using static KanbanLite.Application.Security.AuthorizationHelper;

namespace KanbanLite.Application.Services;

public sealed class ProjectService(AppDbContext db, ICurrentUser currentUser) : IProjectService
{
    public async Task<Result<PagedResult<ProjectListItemDto>>> GetAsync(ProjectQuery query, CancellationToken ct = default)
    {
        if (query is null)
        {
            return Result<PagedResult<ProjectListItemDto>>.Fail(
                AppError.ValidationFailed("Brak parametrów zapytania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["query"] = ["Query jest wymagany."]
                }));
        }

        var authError = EnsureManagerOrOperatorAuthorized(currentUser, "Brak uprawnień do podglądu projektu.");
        if (authError is not null)
        {
            return Result<PagedResult<ProjectListItemDto>>.Fail(authError);
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
            var baseQuery =
                from p in db.Projects.AsNoTracking()
                join o in db.Orders.AsNoTracking() on p.OrderId equals o.Id
                select new { p, o };

            if (query.IsCompleted is not null)
            {
                var isCompleted = query.IsCompleted.Value;
                baseQuery = baseQuery.Where(x => x.p.IsCompleted == isCompleted);
            }

            var total = await baseQuery.LongCountAsync(ct);

            var items = await baseQuery
                .OrderByDescending(x => x.o.DueDate)
                .ThenByDescending(x => x.p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new ProjectListItemDto(
                    x.p.Id,
                    x.p.ProjectNumber,
                    x.o.OrderNumber,
                    x.o.DueDate,
                    x.p.IsCompleted
                ))
                .ToListAsync(ct);

            return Result<PagedResult<ProjectListItemDto>>.Ok(new PagedResult<ProjectListItemDto>(
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
            return Result<PagedResult<ProjectListItemDto>>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania listy projektów."));
        }
    }

    public async Task<Result<ProjectDetailsDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var authError = EnsureManagerOrOperatorAuthorized(currentUser, "Brak uprawnień do podglądu projektu.");
        if (authError is not null)
        {
            return Result<ProjectDetailsDto>.Fail(authError);
        }

        try
        {
            var header = await (
                from p in db.Projects.AsNoTracking()
                join o in db.Orders.AsNoTracking() on p.OrderId equals o.Id
                join pf in db.ProductFormats.AsNoTracking() on o.ProductFormatId equals pf.Id
                where p.Id == id
                select new
                {
                    ProjectId = p.Id,
                    p.ProjectNumber,
                    p.IsCompleted,
                    OrderId = o.Id,
                    o.OrderNumber,
                    o.Quantity,
                    o.DueDate,
                    ProductFormatName = pf.Name
                }
            ).SingleOrDefaultAsync(ct);

            if (header is null)
            {
                return Result<ProjectDetailsDto>.Fail(AppError.NotFound($"Projekt o id={id} nie istnieje."));
            }

            var batches = await db.Batches.AsNoTracking()
                .Where(b => b.ProjectId == id)
                .OrderBy(b => b.BatchNo)
                .Select(b => new ProjectBatchSummaryDto(
                    b.Id,
                    b.BatchNo,
                    b.Quantity,
                    b.Status,
                    b.Stage,
                    ProgressPercentFromStage(b.Stage),
                    b.UpdatedAt
                ))
                .ToListAsync(ct);

            var canShip =
                header.IsCompleted
                || (batches.Count > 0 && batches.All(b => b.Status == BatchStatus.Done && b.Stage == ProductionStage.Ship));

            var reason = canShip
                ? null
                : "Nie wszystkie batche są zakończone (Status=Done) na etapie wysyłki (Stage=Ship).";

            return Result<ProjectDetailsDto>.Ok(new ProjectDetailsDto(
                Id: header.ProjectId,
                ProjectNumber: header.ProjectNumber,
                Order: new ProjectOrderSummaryDto(
                    Id: header.OrderId,
                    OrderNumber: header.OrderNumber,
                    Quantity: header.Quantity,
                    DueDate: header.DueDate,
                    ProductFormatName: header.ProductFormatName
                ),
                IsCompleted: header.IsCompleted,
                Completion: new ProjectCompletionDto(canShip, reason),
                Batches: batches
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<ProjectDetailsDto>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania szczegółów projektu."));
        }
    }

    public async Task<Result<ShipProjectResult>> ShipToCustomerAsync(long projectId, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do wysyłki projektu do klienta.");
        if (authError is not null)
        {
            return Result<ShipProjectResult>.Fail(authError);
        }

        try
        {
            var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == projectId, ct);
            if (project is null)
            {
                return Result<ShipProjectResult>.Fail(AppError.NotFound($"Projekt o id={projectId} nie istnieje."));
            }

            if (project.IsCompleted)
            {
                return Result<ShipProjectResult>.Fail(AppError.Conflict("Projekt jest już oznaczony jako wysłany do klienta."));
            }

            var hasAnyBatches = await db.Batches.AsNoTracking().AnyAsync(x => x.ProjectId == projectId, ct);
            if (!hasAnyBatches)
            {
                return Result<ShipProjectResult>.Fail(
                    AppError.ValidationFailed("Nie można wysłać projektu bez batchy."));
            }

            var hasNotReadyBatches = await db.Batches.AsNoTracking().AnyAsync(
                x => x.ProjectId == projectId && (x.Status != BatchStatus.Done || x.Stage != ProductionStage.Ship),
                ct);

            if (hasNotReadyBatches)
            {
                return Result<ShipProjectResult>.Fail(
                    AppError.ValidationFailed(
                        "Nie można wysłać projektu do klienta — nie wszystkie batche są zakończone na etapie wysyłki.",
                        new Dictionary<string, IReadOnlyList<string>>
                        {
                            ["projectId"] =
                            [
                                "Warunek: wszystkie batche muszą mieć Status=Done oraz Stage=Ship."
                            ]
                        }));
            }

            var now = DateTimeOffset.UtcNow;

            IDbContextTransaction? tx = null;
            if (SupportsTransactions())
            {
                tx = await db.Database.BeginTransactionAsync(ct);
            }

            try
            {
                project.IsCompleted = true;
                project.CompletedAt = now;
                project.CompletedByUserId = currentUser.UserId!;

                await db.SaveChangesAsync(ct);

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return Result<ShipProjectResult>.Ok(new ShipProjectResult(
                    Id: project.Id,
                    IsCompleted: project.IsCompleted,
                    CompletedAt: project.CompletedAt!.Value,
                    CompletedByUserId: project.CompletedByUserId!
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
            return Result<ShipProjectResult>.Fail(
                AppError.Conflict("Nie udało się oznaczyć projektu jako wysłany (konflikt lub naruszenie ograniczeń danych)."));
        }
        catch (Exception)
        {
            return Result<ShipProjectResult>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas wysyłki projektu do klienta."));
        }
    }

    private bool SupportsTransactions()
        => !string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

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
}

