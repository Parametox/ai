using DataAccess.Enums;

namespace KanbanLite.Contracts;

// Ten plik zawiera DTO + Command Models opisane w `.ai/api-plan.md` / `.ai/service-layer-plan.md`.
// W MVP kontrakt jest wywoływany in-process (Blazor Server), ale struktury są zaprojektowane tak,
// aby 1:1 odpowiadały planowanemu kształtowi odpowiedzi/żądań w stylu REST.

// ----------------------------
// Wspólne / infrastrukturalne
// ----------------------------

public sealed record PageQuery(int Page = 1, int PageSize = 50);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long Total
);

/// <summary>
/// Ostrzeżenie (np. soft limit InProgress &gt; 20).
/// </summary>
public sealed record WarningDto(string Code, string Message);

/// <summary>
/// Prosty wrapper dla endpointów zwracających tylko listę w polu "items".
/// </summary>
public sealed record ItemsResult<T>(IReadOnlyList<T> Items);

// ----------------------------
// Product formats
// ----------------------------

public sealed record ProductFormatDto(
    long Id,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Minimalny DTO do dropdownów/lookupów (np. w formularzu tworzenia zlecenia).
/// </summary>
public sealed record ProductFormatLookupDto(long Id, string Name);

public sealed record CreateProductFormatRequest(string Name, bool IsActive = true);

public sealed record UpdateProductFormatRequest(string Name, bool IsActive);

public sealed record ProductFormatQuery(
    bool? IsActive = null,
    string? Q = null,
    int Page = 1,
    int PageSize = 50
);

// ----------------------------
// Batch split rules
// ----------------------------

public sealed record BatchSplitRuleDto(
    long Id,
    int MinQty,
    int? MaxQty,
    decimal Percent,
    int MinBatchSize,
    int? MaxBatchesPerProject,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public sealed record UpsertBatchSplitRuleRequest(
    int MinQty,
    int? MaxQty,
    decimal Percent,
    int MinBatchSize = 1,
    int? MaxBatchesPerProject = null,
    bool IsActive = true
);

public sealed record BatchSplitRuleQuery(bool? IsActive = null);

// ----------------------------
// Orders
// ----------------------------

public sealed record CreateOrderRequest(
    string OrderNumber,
    int Quantity,
    long ProductFormatId,
    DateOnly DueDate
);

public sealed record OrderDto(
    long Id,
    string OrderNumber,
    int Quantity,
    long ProductFormatId,
    DateOnly DueDate,
    DateTimeOffset CreatedAt
);

public sealed record OrderListItemDto(
    long Id,
    string OrderNumber,
    int Quantity,
    DateOnly DueDate,
    string ProductFormatName,
    DateTimeOffset CreatedAt
);

public sealed record OrderProjectSummaryDto(long Id, string ProjectNumber, bool IsCompleted);

public sealed record OrderDetailsDto(
    long Id,
    string OrderNumber,
    int Quantity,
    DateOnly DueDate,
    ProductFormatLookupDto ProductFormat,
    OrderProjectSummaryDto Project
);

public sealed record OrderQuery(
    string? Q = null,
    DateOnly? DueFrom = null,
    DateOnly? DueTo = null,
    int Page = 1,
    int PageSize = 50
);

// ----------------------------
// Projects
// ----------------------------

public sealed record ProjectDto(
    long Id,
    long OrderId,
    string ProjectNumber,
    bool IsCompleted,
    DateTimeOffset CreatedAt
);

public sealed record ProjectListItemDto(
    long Id,
    string ProjectNumber,
    string OrderNumber,
    DateOnly DueDate,
    bool IsCompleted
);

public sealed record ProjectOrderSummaryDto(
    long Id,
    string OrderNumber,
    int Quantity,
    DateOnly DueDate,
    string ProductFormatName
);

public sealed record ProjectCompletionDto(bool CanShip, string? Reason);

public sealed record ProjectBatchSummaryDto(
    long Id,
    int BatchNo,
    int Quantity,
    BatchStatus Status,
    ProductionStage Stage,
    int ProgressPercent,
    DateTimeOffset UpdatedAt
);

public sealed record ProjectDetailsDto(
    long Id,
    string ProjectNumber,
    ProjectOrderSummaryDto Order,
    bool IsCompleted,
    ProjectCompletionDto Completion,
    IReadOnlyList<ProjectBatchSummaryDto> Batches
);

public sealed record ProjectQuery(
    bool? IsCompleted = null,
    int Page = 1,
    int PageSize = 50
);

public sealed record ShipProjectResult(
    long Id,
    bool IsCompleted,
    DateTimeOffset CompletedAt,
    string CompletedByUserId
);

// ----------------------------
// Batches / Kanban
// ----------------------------

public sealed record BatchDto(
    long Id,
    long ProjectId,
    int BatchNo,
    int Quantity,
    BatchStatus Status,
    ProductionStage Stage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateOrderResult(
    OrderDto Order,
    ProjectDto Project,
    IReadOnlyList<BatchDto> Batches
);

public enum KanbanSort
{
    DueDateAsc = 0,
    DueDateDesc = 1,
    UpdatedAtDesc = 2
}

public sealed record KanbanQuery(
    BatchStatus? Status = null,
    ProductionStage? Stage = null,
    string? Q = null,
    KanbanSort? Sort = null,
    int Page = 1,
    int PageSize = 50
);

/// <summary>
/// Read model Kanbanu: projekcja (join batches + projects + orders) aby unikać N+1 w EF Core.
/// </summary>
public sealed record KanbanBatchDto(
    long BatchId,
    long ProjectId,
    string ProjectNumber,
    string OrderNumber,
    DateOnly DueDate,
    int BatchNo,
    int Quantity,
    BatchStatus Status,
    ProductionStage Stage,
    int ProgressPercent,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Kształt odpowiedzi Kanbanu zgodny z `.ai/api-plan.md` (items + paging + inProgressCount + warnings).
/// </summary>
public sealed record KanbanBatchesResult(
    IReadOnlyList<KanbanBatchDto> Items,
    int InProgressCount,
    IReadOnlyList<WarningDto> Warnings,
    int Page,
    int PageSize,
    long Total
);

public sealed record BatchDetailsDto(
    long Id,
    long ProjectId,
    int BatchNo,
    int Quantity,
    BatchStatus Status,
    ProductionStage Stage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record UpdateBatchStatusRequest(BatchStatus NewStatus);

public sealed record UpdateBatchStatusResult(
    long BatchId,
    BatchStatus OldStatus,
    BatchStatus NewStatus,
    ProductionStage Stage,
    DateTimeOffset UpdatedAt,
    int InProgressCount,
    IReadOnlyList<WarningDto> Warnings
);

public sealed record UpdateBatchStageRequest(ProductionStage NewStage);

public sealed record UpdateBatchStageResult(
    long BatchId,
    ProductionStage OldStage,
    ProductionStage NewStage,
    BatchStatus Status,
    int ProgressPercent,
    DateTimeOffset UpdatedAt
);

// ----------------------------
// Audit
// ----------------------------

public sealed record BatchAuditEventDto(
    long Id,
    long BatchId,
    DateTimeOffset ChangedAt,
    string ChangedByUserId,
    BatchStatus? OldStatus,
    BatchStatus? NewStatus,
    ProductionStage? OldStage,
    ProductionStage? NewStage
);

// ----------------------------
// Dashboard
// ----------------------------

public sealed record DashboardUrgentOrderDto(
    long OrderId,
    string OrderNumber,
    DateOnly DueDate,
    long ProjectId,
    string ProjectNumber
);

public sealed record DashboardDto(
    IReadOnlyDictionary<BatchStatus, int> CountsByStatus,
    IReadOnlyDictionary<ProductionStage, int> CountsByStage,
    IReadOnlyList<DashboardUrgentOrderDto> UrgentOrders,
    IReadOnlyList<WarningDto> Warnings
);

// ----------------------------
// Authentication / Authorization
// ----------------------------

/// <summary>
/// Typ wyliczeniowy ról użytkownika w systemie.
/// </summary>
public enum UserRole
{
    Manager = 0,
    Operator = 1
}

/// <summary>
/// DTO użytkownika z podstawowymi informacjami i rolą.
/// </summary>
public sealed record UserDto(
    string Id,
    string Username,
    UserRole Role
);

/// <summary>
/// Request do logowania użytkownika (username/password).
/// </summary>
public sealed record LoginRequest(
    string Username,
    string Password
);

/// <summary>
/// Response z logowania zawierający informacje o użytkowniku i statusie operacji.
/// </summary>
public sealed record AuthResult(
    bool IsSuccess,
    string? ErrorMessage,
    UserDto? User
);

/// <summary>
/// Response z informacjami o sesji użytkownika (opcjonalnie, jeśli potrzebne dodatkowe pola).
/// </summary>
public sealed record AuthResponse(
    string UserId,
    string Username,
    UserRole Role,
    DateTimeOffset ExpiresAt
);
