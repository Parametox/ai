using System.Globalization;
using AutoMapper;
using DataAccess;
using DataAccess.Entities;
using DataAccess.Enums;
using DataAccess.Repositories;
using FluentValidation;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using static KanbanLite.Application.Security.AuthorizationHelper;

namespace KanbanLite.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateOrderRequest> _createOrderValidator;
    private readonly IMapper _mapper;

    public OrderService(
        IDbContextFactory<AppDbContext> dbFactory, 
        ICurrentUser currentUser,
        IValidator<CreateOrderRequest> createOrderValidator,
        IMapper mapper)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _createOrderValidator = createOrderValidator;
        _mapper = mapper;
    }
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

        var authError = EnsureManagerAuthorized(_currentUser, "Brak uprawnień do tworzenia zleceń.");
        if (authError is not null)
        {
            return Result<CreateOrderResult>.Fail(authError);
        }

        // FluentValidation
        var validationResult = await _createOrderValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key, 
                    g => (IReadOnlyList<string>)g.Select(e => e.ErrorMessage).ToList());
            
            return Result<CreateOrderResult>.Fail(
                AppError.ValidationFailed("Nieprawidłowe dane wejściowe.", errors));
        }

        var normalized = Normalize(request);

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            using var unitOfWork = new UnitOfWork(db);
            
            var productFormat = await unitOfWork.ProductFormats.SingleOrDefaultAsync(x => x.Id == normalized.ProductFormatId, ct);
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

            var splitRule = await FindActiveSplitRuleForQuantityAsync(unitOfWork, normalized.Quantity, ct);
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

            await unitOfWork.BeginTransactionAsync(ct);

            try
            {
                unitOfWork.Orders.Add(order);
                unitOfWork.Projects.Add(project);
                unitOfWork.Batches.AddRange(batches);

                await unitOfWork.SaveChangesAsync(ct);
                await unitOfWork.CommitTransactionAsync(ct);

                // AutoMapper mappings
                var orderDto = _mapper.Map<OrderDto>(order);
                var projectDto = _mapper.Map<ProjectDto>(project);
                var batchDtos = batches
                    .OrderBy(b => b.BatchNo)
                    .Select(b => _mapper.Map<BatchDto>(b))
                    .ToList();

                return Result<CreateOrderResult>.Ok(new CreateOrderResult(
                    Order: orderDto,
                    Project: projectDto,
                    Batches: batchDtos
                ));
            }
            catch
            {
                await unitOfWork.RollbackTransactionAsync(ct);
                throw;
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

        var authError = EnsureManagerAuthorized(_currentUser, "Brak uprawnień do tworzenia zleceń.");
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
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            
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
        var authError = EnsureManagerAuthorized(_currentUser, "Brak uprawnień do tworzenia zleceń.");
        if (authError is not null)
        {
            return Result<OrderDetailsDto>.Fail(authError);
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            
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

    private static CreateOrderRequest Normalize(CreateOrderRequest request)
        => request with
        {
            OrderNumber = request.OrderNumber.Trim()
        };

    private static async Task<BatchSplitRule?> FindActiveSplitRuleForQuantityAsync(IUnitOfWork unitOfWork, int quantity, CancellationToken ct)
    {
        // Zakładamy brak overlapów aktywnych (walidowane w BatchSplitRuleService w przyszłości).
        // Dobieramy najbardziej "szczegółową" regułę: najwyższy MinQty, a przy remisie najniższy MaxQty.
        var rules = await unitOfWork.BatchSplitRules.QueryNoTracking()
            .Where(r => r.IsActive)
            .Where(r => r.MinQty <= quantity)
            .Where(r => r.MaxQty == null || quantity <= r.MaxQty.Value)
            .OrderByDescending(r => r.MinQty)
            .ThenBy(r => r.MaxQty == null ? int.MaxValue : r.MaxQty.Value)
            .ToListAsync(ct);
        
        return rules.FirstOrDefault();
    }

    private static List<Batch> CreateBatches(Project project, int orderQty, BatchSplitRule splitRule, DateTimeOffset now)
    {
        // Maksymalny rozmiar batcha = Percent * MaxQty (lub MaxQty z reguły, lub orderQty jako fallback)
        var referenceQty = splitRule.MaxQty ?? orderQty;
        var maxBatchSize = (int)Math.Ceiling(referenceQty * (double)splitRule.Percent);
        
        // MaxBatchSize nie może być mniejszy niż MinBatchSize
        maxBatchSize = Math.Max(maxBatchSize, splitRule.MinBatchSize);

        if (maxBatchSize <= 0)
        {
            throw new InvalidOperationException("Wyliczony rozmiar batcha jest nieprawidłowy.");
        }

        // Walidacja: MinBatchSize nie może być większy niż MaxBatchSize
        if (splitRule.MinBatchSize > maxBatchSize)
        {
            throw new InvalidOperationException("MinBatchSize nie może być większy niż MaxBatchSize.");
        }

        var batches = new List<Batch>();
        var remaining = orderQty;
        var batchNo = 1;

        // Oblicz liczbę pełnych batchy
        var fullBatches = remaining / maxBatchSize;
        var lastBatchQty = remaining % maxBatchSize;

        // Tworzenie pełnych batchy
        for (int i = 0; i < fullBatches; i++)
        {
            batches.Add(new Batch
            {
                Project = project,
                BatchNo = batchNo++,
                Quantity = maxBatchSize,
                Status = BatchStatus.New,
                Stage = ProductionStage.Design,
                CreatedAt = now,
                UpdatedAt = now
            });

            if (splitRule.MaxBatchesPerProject is not null && batches.Count >= splitRule.MaxBatchesPerProject.Value)
            {
                // Jeśli osiągnięto limit, resztę dodajemy do ostatniego batcha
                var totalInBatches = batches.Sum(b => b.Quantity);
                if (totalInBatches < orderQty)
                {
                    batches[^1].Quantity += (orderQty - totalInBatches);
                }
                break;
            }
        }

        // Obsługa ostatniego batcha (reszty)
        if (lastBatchQty > 0 && (splitRule.MaxBatchesPerProject is null || batches.Count < splitRule.MaxBatchesPerProject.Value))
        {
            if (lastBatchQty < splitRule.MinBatchSize && batches.Count > 0)
            {
                // Jeśli reszta jest mniejsza niż MinBatchSize, dokładamy do poprzedniego batcha
                batches[^1].Quantity += lastBatchQty;
            }
            else
            {
                // Reszta jest wystarczająco duża, tworzę nowy batch
                batches.Add(new Batch
                {
                    Project = project,
                    BatchNo = batchNo++,
                    Quantity = lastBatchQty,
                    Status = BatchStatus.New,
                    Stage = ProductionStage.Design,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        // Walidacje końcowe
        if (batches.Sum(b => b.Quantity) != orderQty)
        {
            throw new InvalidOperationException("Suma batchy nie zgadza się z ilością zlecenia.");
        }

        if (batches.Any(b => b.Quantity < splitRule.MinBatchSize))
        {
            throw new InvalidOperationException("Co najmniej jeden batch ma ilość poniżej MinBatchSize.");
        }

        if (splitRule.MaxBatchesPerProject is not null && batches.Count > splitRule.MaxBatchesPerProject.Value)
        {
            throw new InvalidOperationException("Liczba batchy przekroczyłaby limit MaxBatchesPerProject.");
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

