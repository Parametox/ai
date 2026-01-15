using System.Globalization;
using DataAccess;
using DataAccess.Entities;
using DataAccess.Enums;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KanbanLite.Application.Services;

public sealed class OrderService(AppDbContext db, ICurrentUser currentUser) : IOrderService
{
    public async Task<Result<CreateOrderResult>> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return Result<CreateOrderResult>.Fail(
                AppError.ValidationFailed("Brak payloadu żądania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["request"] = ["Request jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized();
        if (authError is not null)
        {
            return Result<CreateOrderResult>.Fail(authError);
        }

        var normalized = Normalize(request);
        var validation = Validate(normalized);
        if (validation is not null)
        {
            return Result<CreateOrderResult>.Fail(validation);
        }

        try
        {
            var productFormat = await db.ProductFormats.SingleOrDefaultAsync(x => x.Id == normalized.ProductFormatId, ct);
            if (productFormat is null)
            {
                return Result<CreateOrderResult>.Fail(AppError.ValidationFailed(
                    "Wybrany format produktu nie istnieje.",
                    new Dictionary<string, IReadOnlyList<string>> { ["productFormatId"] = ["Nieprawidłowa wartość."] }));
            }

            if (!productFormat.IsActive)
            {
                return Result<CreateOrderResult>.Fail(AppError.ValidationFailed(
                    "Wybrany format produktu jest nieaktywny.",
                    new Dictionary<string, IReadOnlyList<string>> { ["productFormatId"] = ["Wybierz aktywny format."] }));
            }

            var splitRule = await FindActiveSplitRuleForQuantityAsync(normalized.Quantity, ct);
            if (splitRule is null)
            {
                return Result<CreateOrderResult>.Fail(AppError.ValidationFailed(
                    "Brak aktywnej reguły dzielenia batchy dla podanej ilości.",
                    new Dictionary<string, IReadOnlyList<string>>
                    {
                        ["quantity"] = ["Skonfiguruj aktywne reguły dzielenia batchy (BatchSplitRule)."]
                    }));
            }

            var now = DateTimeOffset.UtcNow;

            var order = new Order
            {
                OrderNumber = normalized.OrderNumber,
                Quantity = normalized.Quantity,
                ProductFormatId = normalized.ProductFormatId,
                ProductFormat = productFormat,
                DueDate = normalized.DueDate,
                CreatedAt = now
            };

            var project = new Project
            {
                Order = order,
                ProjectNumber = CreateProjectNumberFromOrderNumber(order.OrderNumber),
                IsCompleted = false,
                CreatedAt = now
            };

            var batches = CreateBatches(project, orderQty: order.Quantity, splitRule, now);

            IDbContextTransaction? tx = null;
            if (SupportsTransactions())
            {
                tx = await db.Database.BeginTransactionAsync(ct);
            }

            try
            {
                db.Orders.Add(order);
                db.Projects.Add(project);
                db.Batches.AddRange(batches);

                await db.SaveChangesAsync(ct);

                if (tx is not null)
                {
                    await tx.CommitAsync(ct);
                }

                return Result<CreateOrderResult>.Ok(new CreateOrderResult(
                    Order: new OrderDto(
                        order.Id,
                        order.OrderNumber,
                        order.Quantity,
                        order.ProductFormatId,
                        order.DueDate,
                        order.CreatedAt
                    ),
                    Project: new ProjectDto(
                        project.Id,
                        project.OrderId,
                        project.ProjectNumber,
                        project.IsCompleted,
                        project.CreatedAt
                    ),
                    Batches: batches
                        .OrderBy(b => b.BatchNo)
                        .Select(b => new BatchDto(
                            b.Id,
                            b.ProjectId,
                            b.BatchNo,
                            b.Quantity,
                            b.Status,
                            b.Stage,
                            b.CreatedAt,
                            b.UpdatedAt
                        ))
                        .ToList()
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
            return Result<CreateOrderResult>.Fail(
                AppError.Conflict("Nie udało się utworzyć zlecenia (konflikt lub naruszenie ograniczeń danych)."));
        }
        catch (Exception)
        {
            return Result<CreateOrderResult>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas tworzenia zlecenia."));
        }
    }

    public async Task<Result<PagedResult<OrderListItemDto>>> GetAsync(OrderQuery query, CancellationToken ct = default)
    {
        if (query is null)
        {
            return Result<PagedResult<OrderListItemDto>>.Fail(
                AppError.ValidationFailed("Brak parametrów zapytania.", new Dictionary<string, IReadOnlyList<string>>
                {
                    ["query"] = ["Query jest wymagany."]
                }));
        }

        var authError = EnsureManagerAuthorized();
        if (authError is not null)
        {
            return Result<PagedResult<OrderListItemDto>>.Fail(authError);
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
            var baseQuery =
                from o in db.Orders.AsNoTracking()
                join pf in db.ProductFormats.AsNoTracking() on o.ProductFormatId equals pf.Id
                select new { o, pf };

            if (query.DueFrom is not null)
            {
                var dueFrom = query.DueFrom.Value;
                baseQuery = baseQuery.Where(x => x.o.DueDate >= dueFrom);
            }

            if (query.DueTo is not null)
            {
                var dueTo = query.DueTo.Value;
                baseQuery = baseQuery.Where(x => x.o.DueDate <= dueTo);
            }

            if (q is not null)
            {
                baseQuery = baseQuery.Where(x =>
                    x.o.OrderNumber.Contains(q)
                    || db.Projects.AsNoTracking().Any(p => p.OrderId == x.o.Id && p.ProjectNumber.Contains(q)));
            }

            var total = await baseQuery.LongCountAsync(ct);

            var items = await baseQuery
                .OrderByDescending(x => x.o.CreatedAt)
                .ThenByDescending(x => x.o.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new OrderListItemDto(
                    x.o.Id,
                    x.o.OrderNumber,
                    x.o.Quantity,
                    x.o.DueDate,
                    x.pf.Name,
                    x.o.CreatedAt
                ))
                .ToListAsync(ct);

            return Result<PagedResult<OrderListItemDto>>.Ok(new PagedResult<OrderListItemDto>(
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
            return Result<PagedResult<OrderListItemDto>>.Fail(
                AppError.Unexpected("Nieoczekiwany błąd podczas pobierania listy zleceń."));
        }
    }

    public async Task<Result<OrderDetailsDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var authError = EnsureManagerAuthorized();
        if (authError is not null)
        {
            return Result<OrderDetailsDto>.Fail(authError);
        }

        try
        {
            var header = await (
                from o in db.Orders.AsNoTracking()
                join pf in db.ProductFormats.AsNoTracking() on o.ProductFormatId equals pf.Id
                where o.Id == id
                select new
                {
                    o.Id,
                    o.OrderNumber,
                    o.Quantity,
                    o.DueDate,
                    ProductFormatId = pf.Id,
                    ProductFormatName = pf.Name
                }
            ).SingleOrDefaultAsync(ct);

            if (header is null)
            {
                return Result<OrderDetailsDto>.Fail(AppError.NotFound($"Zlecenie o id={id} nie istnieje."));
            }

            var project = await db.Projects.AsNoTracking()
                .Where(p => p.OrderId == id)
                .OrderBy(p => p.Id)
                .Select(p => new OrderProjectSummaryDto(p.Id, p.ProjectNumber, p.IsCompleted))
                .FirstOrDefaultAsync(ct);

            if (project is null)
            {
                return Result<OrderDetailsDto>.Fail(AppError.Unexpected("Niespójne dane: zlecenie nie ma powiązanego projektu."));
            }

            return Result<OrderDetailsDto>.Ok(new OrderDetailsDto(
                header.Id,
                header.OrderNumber,
                header.Quantity,
                header.DueDate,
                new ProductFormatLookupDto(header.ProductFormatId, header.ProductFormatName),
                project
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<OrderDetailsDto>.Fail(AppError.Unexpected("Nieoczekiwany błąd podczas pobierania szczegółów zlecenia."));
        }
    }

    private AppError? EnsureManagerAuthorized()
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return AppError.Unauthorized("Użytkownik nie jest zalogowany.");
        }

        return currentUser.IsInRole("Manager")
            ? null
            : AppError.Forbidden("Brak uprawnień do tworzenia zleceń.");
    }

    private bool SupportsTransactions()
        => !string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

    private static CreateOrderRequest Normalize(CreateOrderRequest request)
        => request with
        {
            OrderNumber = request.OrderNumber.Trim()
        };

    private static AppError? Validate(CreateOrderRequest request)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();

        if (string.IsNullOrWhiteSpace(request.OrderNumber))
        {
            errors["orderNumber"] = ["Wymagane."];
        }
        else if (request.OrderNumber.Length > 100)
        {
            errors["orderNumber"] = ["Maksymalna długość to 100 znaków."];
        }

        if (request.Quantity is < 1 or > 100000)
        {
            errors["quantity"] = ["Musi być w zakresie 1..100000."];
        }

        // dueDate >= dziś + 7 dni (PRD)
        var minDue = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7));
        if (request.DueDate < minDue)
        {
            errors["dueDate"] = [$"Minimalna data realizacji to {minDue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}."];
        }

        if (request.ProductFormatId <= 0)
        {
            errors["productFormatId"] = ["Wymagane."];
        }

        return errors.Count > 0
            ? AppError.ValidationFailed("Nieprawidłowe dane wejściowe.", errors)
            : null;
    }

    private async Task<BatchSplitRule?> FindActiveSplitRuleForQuantityAsync(int quantity, CancellationToken ct)
    {
        // Zakładamy brak overlapów aktywnych (walidowane w BatchSplitRuleService w przyszłości).
        // Dobieramy najbardziej "szczegółową" regułę: najwyższy MinQty, a przy remisie najniższy MaxQty.
        return await db.BatchSplitRules.AsNoTracking()
            .Where(r => r.IsActive)
            .Where(r => r.MinQty <= quantity)
            .Where(r => r.MaxQty == null || quantity <= r.MaxQty.Value)
            .OrderByDescending(r => r.MinQty)
            .ThenBy(r => r.MaxQty == null ? int.MaxValue : r.MaxQty.Value)
            .SingleOrDefaultAsync(ct);
    }

    private static List<Batch> CreateBatches(Project project, int orderQty, BatchSplitRule splitRule, DateTimeOffset now)
    {
        // baseSize = ceil(orderQty * percent / 100)
        var baseSize = (int)Math.Ceiling(orderQty * (double)splitRule.Percent / 100d);
        baseSize = Math.Max(baseSize, splitRule.MinBatchSize);

        if (baseSize <= 0)
        {
            throw new InvalidOperationException("Wyliczony rozmiar batcha jest nieprawidłowy.");
        }

        var batches = new List<Batch>();
        var remaining = orderQty;
        var batchNo = 1;

        while (remaining > 0)
        {
            var take = Math.Min(baseSize, remaining);
            if (take < splitRule.MinBatchSize && batches.Count > 0)
            {
                // Jeśli ostatni batch byłby za mały, dokładamy go do poprzedniego.
                batches[^1].Quantity += take;
                remaining = 0;
                break;
            }

            batches.Add(new Batch
            {
                Project = project,
                BatchNo = batchNo++,
                Quantity = take,
                Status = BatchStatus.New,
                Stage = ProductionStage.Design,
                CreatedAt = now,
                UpdatedAt = now
            });

            remaining -= take;

            if (splitRule.MaxBatchesPerProject is not null && batches.Count > splitRule.MaxBatchesPerProject.Value)
            {
                throw new InvalidOperationException("Liczba batchy przekroczyłaby limit MaxBatchesPerProject.");
            }
        }

        if (batches.Sum(b => b.Quantity) != orderQty)
        {
            throw new InvalidOperationException("Suma batchy nie zgadza się z ilością zlecenia.");
        }

        if (batches.Any(b => b.Quantity < splitRule.MinBatchSize))
        {
            throw new InvalidOperationException("Co najmniej jeden batch ma ilość poniżej MinBatchSize.");
        }

        return batches;
    }

    private static string CreateProjectNumberFromOrderNumber(string orderNumber)
    {
        // MVP: prosty mapping ORD-YYYY-XXXX -> PRJ-YYYY-XXXX (zgodne z przykładami w api-plan.md).
        if (orderNumber.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase))
        {
            return "PRJ-" + orderNumber[4..];
        }

        return $"PRJ-{orderNumber}";
    }
}

