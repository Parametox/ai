using AutoMapper;
using FluentValidation;
using KanbanLite.Application.Common;
using KanbanLite.Application.Security;
using KanbanLite.Application.Services.SupabaseModels;
using KanbanLite.Contracts;
using static KanbanLite.Application.Security.AuthorizationHelper;
using DataAccess.Enums;

namespace KanbanLite.Application.Services;

public sealed class OrderService(
    IOrderRepository orderRepository,
    IProjectRepository projectRepository,
    IBatchRepository batchRepository,
    IProductFormatRepository productFormatRepository,
    IBatchSplitRuleRepository batchSplitRuleRepository,
    ICurrentUser currentUser,
    IValidator<CreateOrderRequest> createOrderValidator,
    IMapper mapper) : IOrderService
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

        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do tworzenia zleceń.");
        if (authError is not null)
        {
            return Result<CreateOrderResult>.Fail(authError);
        }

        var validationResult = await createOrderValidator.ValidateAsync(request, ct);
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
            var productFormat = await productFormatRepository.GetByIdAsync(normalized.ProductFormatId, ct);
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

            var allRules = await batchSplitRuleRepository.GetActiveRulesAsync(ct);
            var splitRule = FindActiveRule(allRules, normalized.Quantity);
            
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

            var order = new SupabaseOrder
            {
                OrderNumber = normalized.OrderNumber,
                Quantity = normalized.Quantity,
                ProductFormatId = normalized.ProductFormatId,
                DueDate = normalized.DueDate.ToDateTime(TimeOnly.MinValue),
                CreatedAt = now
            };
            
            var createdOrder = await orderRepository.CreateAsync(order, ct);
            
            var projectNumber = CreateProjectNumberFromOrderNumber(createdOrder.OrderNumber ?? "");
            var project = new SupabaseProject
            {
                OrderId = createdOrder.Id,
                ProjectNumber = projectNumber,
                IsCompleted = false,
                CreatedAt = now
            };
            var createdProject = await projectRepository.CreateAsync(project, ct);

            var batches = CreateBatches(createdProject.Id, createdOrder.Quantity, splitRule, now);
            await batchRepository.CreateRangeAsync(batches, ct);

             var orderDto = new OrderDto(
                 createdOrder.Id, createdOrder.OrderNumber ?? "", createdOrder.Quantity, createdOrder.ProductFormatId, DateOnly.FromDateTime(createdOrder.DueDate), createdOrder.CreatedAt
             );
             var projectDto = new ProjectDto(
                 createdProject.Id, createdProject.OrderId, createdProject.ProjectNumber ?? "", createdProject.IsCompleted, createdProject.CreatedAt
             );
             var batchDtos = batches.Select(b => new BatchDto(
                 b.Id, b.ProjectId, b.BatchNo, b.Quantity, 
                 Enum.TryParse<BatchStatus>(b.Status, out var s) ? s : BatchStatus.New,
                 (ProductionStage)b.Stage,
                 b.CreatedAt, b.UpdatedAt
             )).ToList();

             return Result<CreateOrderResult>.Ok(new CreateOrderResult(orderDto, projectDto, batchDtos));

        }
        catch (Exception ex)
        {
            return Result<CreateOrderResult>.Fail(AppError.Unexpected($"Nieoczekiwany błąd podczas tworzenia zlecenia: {ex.Message}"));
        }
    }

    public async Task<Result<PagedResult<OrderListItemDto>>> GetAsync(OrderQuery query, CancellationToken ct = default)
    {
        if (query is null) return Result<PagedResult<OrderListItemDto>>.Fail(AppError.ValidationFailed("Query required."));
        
        var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do tworzenia zleceń.");
        if (authError is not null) return Result<PagedResult<OrderListItemDto>>.Fail(authError);

        try 
        {
             var page = query.Page < 1 ? 1 : query.Page;
             var pageSize = query.PageSize < 1 ? 50 : (query.PageSize > 200 ? 200 : query.PageSize); 

             var (items, total) = await orderRepository.GetOrdersAsync(query, page, pageSize, ct);

             var mappedItems = items.Select(o => new OrderListItemDto(
                 o.Id,
                 o.OrderNumber ?? "",
                 o.Quantity,
                 DateOnly.FromDateTime(o.DueDate),
                 o.ProductFormat?.Name ?? "", 
                 o.CreatedAt
             )).ToList();

             return Result<PagedResult<OrderListItemDto>>.Ok(new PagedResult<OrderListItemDto>(mappedItems, page, pageSize, total));
        }
        catch (Exception ex)
        {
             return Result<PagedResult<OrderListItemDto>>.Fail(AppError.Unexpected(ex.Message));
        }
    }
    
    public async Task<Result<OrderDetailsDto>> GetByIdAsync(long id, CancellationToken ct = default)
    {
         var authError = EnsureManagerAuthorized(currentUser, "Brak uprawnień do tworzenia zleceń.");
         if (authError is not null) return Result<OrderDetailsDto>.Fail(authError);
         
         try
         {
             var order = await orderRepository.GetByIdAsync(id, ct);
             if (order is null) return Result<OrderDetailsDto>.Fail(AppError.NotFound($"Zlecenie o id={id} nie istnieje."));

             var project = await projectRepository.GetByOrderIdAsync(id, ct);
             if (project is null) return Result<OrderDetailsDto>.Fail(AppError.Unexpected("Niespójne dane: zlecenie nie ma powiązanego projektu."));

             var projectSummary = new OrderProjectSummaryDto(project.Id, project.ProjectNumber ?? "", project.IsCompleted);

             var dto = new OrderDetailsDto(
                order.Id,
                order.OrderNumber ?? "",
                order.Quantity,
                DateOnly.FromDateTime(order.DueDate),
                new ProductFormatLookupDto(order.ProductFormatId, order.ProductFormat?.Name ?? ""),
                projectSummary
            );

            return Result<OrderDetailsDto>.Ok(dto);
         }
         catch (Exception ex)
         {
             return Result<OrderDetailsDto>.Fail(AppError.Unexpected(ex.Message));
         }
    }

    private static CreateOrderRequest Normalize(CreateOrderRequest request)
        => request with { OrderNumber = request.OrderNumber.Trim() };

    private static SupabaseBatchSplitRule? FindActiveRule(IReadOnlyList<SupabaseBatchSplitRule> rules, int quantity)
    {
        return rules
            .Where(r => r.IsActive)
            .Where(r => r.MinQty <= quantity)
            .Where(r => r.MaxQty == null || quantity <= r.MaxQty.Value)
            .OrderByDescending(r => r.MinQty)
            .ThenBy(r => r.MaxQty == null ? int.MaxValue : r.MaxQty.Value)
            .FirstOrDefault();
    }

    private static List<SupabaseBatch> CreateBatches(long projectId, int orderQty, SupabaseBatchSplitRule splitRule, DateTimeOffset now)
    {
        var referenceQty = splitRule.MaxQty ?? orderQty;
        var maxBatchSize = (int)Math.Ceiling(referenceQty * (double)splitRule.Percent);

        maxBatchSize = Math.Max(maxBatchSize, splitRule.MinBatchSize);

        if (maxBatchSize <= 0) throw new InvalidOperationException("Wyliczony rozmiar batcha jest nieprawidłowy.");
        if (splitRule.MinBatchSize > maxBatchSize) throw new InvalidOperationException("MinBatchSize nie może być większy niż MaxBatchSize.");

        var batches = new List<SupabaseBatch>();
        var remaining = orderQty;
        var batchNo = 1;

        var fullBatches = remaining / maxBatchSize;
        var lastBatchQty = remaining % maxBatchSize;

        for (int i = 0; i < fullBatches; i++)
        {
            batches.Add(new SupabaseBatch
            {
                ProjectId = projectId,
                BatchNo = batchNo++,
                Quantity = maxBatchSize,
                Status = BatchStatus.New.ToString(),
                Stage = (short)ProductionStage.Design,
                CreatedAt = now,
                UpdatedAt = now
            });

            if (splitRule.MaxBatchesPerProject is not null && batches.Count >= splitRule.MaxBatchesPerProject.Value)
            {
                var totalInBatches = batches.Sum(b => b.Quantity);
                if (totalInBatches < orderQty)
                {
                    batches[^1].Quantity += (orderQty - totalInBatches);
                }
                break;
            }
        }

        if (lastBatchQty > 0 && (splitRule.MaxBatchesPerProject is null || batches.Count < splitRule.MaxBatchesPerProject.Value))
        {
            if (lastBatchQty < splitRule.MinBatchSize && batches.Count > 0)
            {
                batches[^1].Quantity += lastBatchQty;
            }
            else
            {
                batches.Add(new SupabaseBatch
                {
                    ProjectId = projectId,
                    BatchNo = batchNo++,
                    Quantity = lastBatchQty,
                    Status = BatchStatus.New.ToString(),
                    Stage = (short)ProductionStage.Design,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        if (batches.Sum(b => b.Quantity) != orderQty)
            throw new InvalidOperationException("Suma batchy nie zgadza się z ilością zlecenia.");

        if (batches.Any(b => b.Quantity < splitRule.MinBatchSize))
            throw new InvalidOperationException("Co najmniej jeden batch ma ilość poniżej MinBatchSize.");

        if (splitRule.MaxBatchesPerProject is not null && batches.Count > splitRule.MaxBatchesPerProject.Value)
            throw new InvalidOperationException("Liczba batchy przekroczyłaby limit MaxBatchesPerProject.");

        return batches;
    }

    private static string CreateProjectNumberFromOrderNumber(string orderNumber)
    {
        if (orderNumber.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase))
        {
            return "PRJ-" + orderNumber[4..];
        }

        return $"PRJ-{orderNumber}";
    }
}

