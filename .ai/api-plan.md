# Service Layer Contract (Blazor Server Monolith)

> Ten dokument opisuje **kontrakt warstwy serwisów** (C# metody + DTO) wywoływanych in-process przez komponenty Blazor Server. Serwisy są rejestrowane w DI i używają ASP.NET Core Identity do autoryzacji.

> Docelowy plan serwisów (C# + DI + walidacje/RBAC) jest opisany w: `.ai/service-layer-plan.md`.

## 1. Resources (Entities)
- **ProductFormat** → `product_formats`
- **Order** → `orders`
- **Project** → `projects`
- **Batch** → `batches`
- **BatchSplitRule** → `batch_split_rules`
- **BatchAuditEvent** → `batch_audit_log`
- **Identity (User/Role)** → ASP.NET Core Identity tables (e.g., `AspNetUsers`, `AspNetRoles`, …) *(infrastructure; not domain CRUD in MVP)*

## 2. Service Methods

### Conventions (applies to all service methods)
- **Auth**: Cookie authentication via ASP.NET Core Identity.
- **Authorization**:
  - `Manager` (Manager): Kanban + Manager Panel + "Ship to customer".
  - `Operator`: Kanban only.
- **Error handling**: Serwisy rzucają wyjątki, które są mapowane na odpowiednie komunikaty w UI:
  - `NotFoundException`: zasób nie istnieje
  - `UnauthorizedException`: użytkownik nie zalogowany
  - `ForbiddenException`: użytkownik zalogowany, ale brak uprawnień (rola/policy)
  - `ValidationException`: błędy walidacji (z listą błędów pól)
  - `BusinessRuleException`: naruszenie reguły biznesowej
  - `ConflictException`: konflikt (unique constraint / invalid state transition / concurrency conflict)
- **Result types**: Metody zwracają DTO lub `Result<T>`/`Result` z informacją o sukcesie/błędzie (w zależności od wybranej strategii obsługi błędów).

---

### 2.1 Product formats (`product_formats`) — Manager Panel

#### List product formats
- **Service**: `IProductFormatService`
- **Method**: `Task<PagedResult<ProductFormatDto>> GetProductFormatsAsync(GetProductFormatsQuery query, CancellationToken ct = default)`
- **Description**: List formats for dropdowns and administration.
- **Query parameters**:
  - `IsActive` (bool?, optional)
  - `SearchTerm` (string, optional) – search by name (starts-with/contains)
  - `Page` (int, optional), `PageSize` (int, optional)
- **Response DTO**:

```csharp
public class ProductFormatDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}
```

- **Authorization**: `[Authorize(Roles = "Manager")]` *(optional: allow Operator to list active formats only for UI lookups)*
- **Exceptions**: `UnauthorizedException`, `ForbiddenException`

#### Create product format
- **Service**: `IProductFormatService`
- **Method**: `Task<ProductFormatDto> CreateProductFormatAsync(CreateProductFormatCommand command, CancellationToken ct = default)`
- **Description**: Create new product format.
- **Command**:

```csharp
public class CreateProductFormatCommand
{
    public string Name { get; set; }
    public bool IsActive { get; set; }
}
```

- **Response**: `ProductFormatDto`
- **Exceptions**:
  - `ConflictException`: name already exists (`uq_product_formats_name`)
  - `ValidationException`: invalid name (empty/too long per application constraints)
  - `ForbiddenException`: Operator not allowed

#### Update product format
- **Service**: `IProductFormatService`
- **Method**: `Task<ProductFormatDto> UpdateProductFormatAsync(int id, UpdateProductFormatCommand command, CancellationToken ct = default)`
- **Description**: Rename / activate-deactivate.
- **Command**:

```csharp
public class UpdateProductFormatCommand
{
    public string Name { get; set; }
    public bool IsActive { get; set; }
}
```

- **Response**: `ProductFormatDto`
- **Exceptions**: `NotFoundException`, `ConflictException`, `ValidationException`, `ForbiddenException`

#### Delete product format (optional)
- **Service**: `IProductFormatService`
- **Method**: `Task DeleteProductFormatAsync(int id, CancellationToken ct = default)`
- **Description**: For MVP prefer **soft deactivation** (`is_active=false`) to avoid FK issues with orders.
- **Response**: void
- **Exceptions**:
  - `ConflictException`: cannot delete due to existing orders referencing the format
  - `NotFoundException`

---

### 2.2 Orders (`orders`) — Manager Panel

#### Create order (and auto-create project + batches)
- **Service**: `IOrderService`
- **Method**: `Task<CreateOrderResult> CreateOrderAsync(CreateOrderCommand command, CancellationToken ct = default)`
- **Description**: Create an order; system creates a project and batches based on active split rules.
- **Command**:

```csharp
public class CreateOrderCommand
{
    public string OrderNumber { get; set; }
    public int Quantity { get; set; }
    public int ProductFormatId { get; set; }
    public DateTime DueDate { get; set; }
}
```

- **Response**:

```csharp
public class CreateOrderResult
{
    public OrderDto Order { get; set; }
    public ProjectDto Project { get; set; }
    public List<BatchDto> Batches { get; set; }
}
```

- **Exceptions**:
  - `ForbiddenException`: Operator not allowed
  - `ConflictException`: `orderNumber` already exists (`uq_orders_order_number`)
  - `ValidationException`: validation failures (quantity range; due date rule; split rules missing/invalid; resulting batch sizes invalid)

#### List orders (history)
- **Service**: `IOrderService`
- **Method**: `Task<PagedResult<OrderListItemDto>> GetOrdersAsync(GetOrdersQuery query, CancellationToken ct = default)`
- **Description**: List orders for history view (Manager Panel).
- **Query parameters**:
  - `SearchTerm` (string, optional): orderNumber/projectNumber search
  - `DueFrom` (DateTime?, optional), `DueTo` (DateTime?, optional)
  - `Page` (int, optional), `PageSize` (int, optional)
- **Response**: `PagedResult<OrderListItemDto>`
- **Authorization**: `[Authorize(Roles = "Manager")]`
- **Exceptions**: `UnauthorizedException`, `ForbiddenException`

#### Get order details
- **Service**: `IOrderService`
- **Method**: `Task<OrderDetailsDto> GetOrderDetailsAsync(int id, CancellationToken ct = default)`
- **Description**: Order + linked project summary.
- **Response**:

```csharp
public class OrderDetailsDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public int Quantity { get; set; }
    public DateTime DueDate { get; set; }
    public ProductFormatDto ProductFormat { get; set; }
    public ProjectSummaryDto Project { get; set; }
}
```

- **Authorization**: `[Authorize(Roles = "Manager")]`
- **Exceptions**: `NotFoundException`, `UnauthorizedException`, `ForbiddenException`

---

### 2.3 Projects (`projects`) — Kanban + Manager actions

#### List active projects (optional)
- **Service**: `IProjectService`
- **Method**: `Task<PagedResult<ProjectListItemDto>> GetActiveProjectsAsync(GetProjectsQuery query, CancellationToken ct = default)`
- **Description**: Active projects for quick navigation. Kanban is primarily batch-based.
- **Query parameters**:
  - `IsCompleted` (bool?, default: false)
  - `Page` (int, optional), `PageSize` (int, optional)
- **Response**: `PagedResult<ProjectListItemDto>`

#### Get project details (includes batches + readiness to ship)
- **Service**: `IProjectService`
- **Method**: `Task<ProjectDetailsDto> GetProjectDetailsAsync(int id, CancellationToken ct = default)`
- **Description**: Project details view (Operator/Manager).
- **Response**:

```csharp
public class ProjectDetailsDto
{
    public int Id { get; set; }
    public string ProjectNumber { get; set; }
    public OrderSummaryDto Order { get; set; }
    public bool IsCompleted { get; set; }
    public ProjectCompletionDto Completion { get; set; }
    public List<BatchSummaryDto> Batches { get; set; }
}

public class ProjectCompletionDto
{
    public bool CanShip { get; set; }
    public string Reason { get; set; }
}
```

- **Exceptions**: `NotFoundException`, `UnauthorizedException`

#### Ship project to customer (mark completed)
- **Service**: `IProjectService`
- **Method**: `Task<ProjectDto> ShipProjectAsync(int id, ShipProjectCommand command, CancellationToken ct = default)`
- **Description**: Manager action "Wyślij do klienta". Marks project completed and removes it from active Kanban.
- **Command** (optional):

```csharp
public class ShipProjectCommand
{
    public string Note { get; set; } // Optional shipping note
}
```

- **Response**: `ProjectDto` (with `IsCompleted = true`, `CompletedAt`, `CompletedByUserId`)
- **Authorization**: `[Authorize(Roles = "Manager")]`
- **Exceptions**:
  - `ForbiddenException`: Operator not allowed
  - `ConflictException` / `BusinessRuleException`: cannot ship because not all batches meet criteria
  - `NotFoundException`: project not found

---

### 2.4 Batches (`batches`) — Kanban operations

#### Kanban list (active batches)
- **Service**: `IKanbanService` lub `IBatchService`
- **Method**: `Task<KanbanBatchesResult> GetKanbanBatchesAsync(GetKanbanBatchesQuery query, CancellationToken ct = default)`
- **Description**: Main Kanban read model: batches joined with order/project context (active projects only).
- **Query parameters**:
  - `Status` (BatchStatus?, optional): New|InProgress|Done
  - `Stage` (int?, optional): 1..5
  - `SearchTerm` (string, optional): orderNumber/projectNumber
  - `SortBy` (string, optional): e.g., `DueDateAsc`, `DueDateDesc`, `UpdatedAtDesc`
  - `Page` (int, optional), `PageSize` (int, optional)
- **Response**:

```csharp
public class KanbanBatchesResult
{
    public List<KanbanBatchDto> Items { get; set; }
    public int InProgressCount { get; set; }
    public List<WarningDto> Warnings { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class KanbanBatchDto
{
    public int BatchId { get; set; }
    public int ProjectId { get; set; }
    public string ProjectNumber { get; set; }
    public string OrderNumber { get; set; }
    public DateTime DueDate { get; set; }
    public int BatchNo { get; set; }
    public int Quantity { get; set; }
    public BatchStatus Status { get; set; }
    public int Stage { get; set; }
    public int ProgressPercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- **Exceptions**: `UnauthorizedException`

#### Update batch status
- **Service**: `IBatchService`
- **Method**: `Task<UpdateBatchStatusResult> UpdateBatchStatusAsync(int id, UpdateBatchStatusCommand command, CancellationToken ct = default)`
- **Description**: Change status (New → InProgress → Done). Persists audit log entry.
- **Command**:

```csharp
public class UpdateBatchStatusCommand
{
    public BatchStatus NewStatus { get; set; }
}
```

- **Response**:

```csharp
public class UpdateBatchStatusResult
{
    public int BatchId { get; set; }
    public BatchStatus OldStatus { get; set; }
    public BatchStatus NewStatus { get; set; }
    public int Stage { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int InProgressCount { get; set; }
    public List<WarningDto> Warnings { get; set; }
}
```

- **Exceptions**:
  - `NotFoundException`: batch not found
  - `ConflictException` / `BusinessRuleException`: invalid status transition (business rule)
  - `ConflictException`: concurrency conflict (optional, via `xmin`/ETag-like mechanism)

#### Update batch stage
- **Service**: `IBatchService`
- **Method**: `Task<UpdateBatchStageResult> UpdateBatchStageAsync(int id, UpdateBatchStageCommand command, CancellationToken ct = default)`
- **Description**: Move stage forward only (1..5). Persists audit log entry.
- **Command**:

```csharp
public class UpdateBatchStageCommand
{
    public int NewStage { get; set; }
}
```

- **Response**:

```csharp
public class UpdateBatchStageResult
{
    public int BatchId { get; set; }
    public int OldStage { get; set; }
    public int NewStage { get; set; }
    public BatchStatus Status { get; set; }
    public int ProgressPercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- **Exceptions**:
  - `NotFoundException`
  - `ValidationException`: newStage out of range (DB constraint is 1..5)
  - `ConflictException` / `BusinessRuleException`: stage cannot move backwards (business rule)

---

### 2.5 Batch split rules (`batch_split_rules`) — Manager Panel

#### List split rules
- **Service**: `IBatchSplitRuleService`
- **Method**: `Task<List<BatchSplitRuleDto>> GetBatchSplitRulesAsync(bool? isActive = null, CancellationToken ct = default)`
- **Description**: Read and manage active rules. Applies only to newly created orders.
- **Response**:

```csharp
public class BatchSplitRuleDto
{
    public int Id { get; set; }
    public int MinQty { get; set; }
    public int? MaxQty { get; set; }
    public decimal Percent { get; set; }
    public int MinBatchSize { get; set; }
    public int? MaxBatchesPerProject { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

#### Create split rule
- **Service**: `IBatchSplitRuleService`
- **Method**: `Task<BatchSplitRuleDto> CreateBatchSplitRuleAsync(CreateBatchSplitRuleCommand command, CancellationToken ct = default)`
- **Description**: Manage thresholds and percent. Must prevent overlaps among active ranges.
- **Command**:

```csharp
public class CreateBatchSplitRuleCommand
{
    public int MinQty { get; set; }
    public int? MaxQty { get; set; }
    public decimal Percent { get; set; }
    public int MinBatchSize { get; set; }
    public int? MaxBatchesPerProject { get; set; }
    public bool IsActive { get; set; }
}
```

- **Response**: `BatchSplitRuleDto`
- **Authorization**: `[Authorize(Roles = "Manager")]`
- **Exceptions**:
  - `ValidationException`: invalid numeric values (minQty>=1, percent (0..100], etc.)
  - `ConflictException` / `BusinessRuleException`: overlaps an existing active rule range (application validation)

#### Update split rule
- **Service**: `IBatchSplitRuleService`
- **Method**: `Task<BatchSplitRuleDto> UpdateBatchSplitRuleAsync(int id, UpdateBatchSplitRuleCommand command, CancellationToken ct = default)`
- **Description**: Update thresholds and percent. Must prevent overlaps among active ranges.
- **Command**: `UpdateBatchSplitRuleCommand` (same structure as `CreateBatchSplitRuleCommand`)
- **Response**: `BatchSplitRuleDto`
- **Authorization**: `[Authorize(Roles = "Manager")]`
- **Exceptions**: `NotFoundException`, `ValidationException`, `ConflictException` / `BusinessRuleException`

#### Deactivate split rule
- **Service**: `IBatchSplitRuleService`
- **Method**: `Task<BatchSplitRuleDto> DeactivateBatchSplitRuleAsync(int id, CancellationToken ct = default)`
- **Description**: Sets `is_active=false`.
- **Response**: `BatchSplitRuleDto` (updated rule)
- **Authorization**: `[Authorize(Roles = "Manager")]`
- **Exceptions**: `NotFoundException`

---

### 2.6 Audit log (`batch_audit_log`) — Manager Panel / Project view

#### List audit events for a batch
- **Service**: `IBatchAuditService`
- **Method**: `Task<PagedResult<BatchAuditEventDto>> GetBatchAuditEventsAsync(int batchId, GetBatchAuditEventsQuery query, CancellationToken ct = default)`
- **Description**: Show who changed status/stage and when.
- **Query parameters**:
  - `Page` (int, optional), `PageSize` (int, optional)
- **Response**:

```csharp
public class BatchAuditEventDto
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedByUserId { get; set; }
    public BatchStatus? OldStatus { get; set; }
    public BatchStatus? NewStatus { get; set; }
    public int? OldStage { get; set; }
    public int? NewStage { get; set; }
}
```

- **Authorization**: `[Authorize]` *(optional: allow Operator to view audit for transparency; otherwise Manager-only)*
- **Exceptions**: `NotFoundException`, `UnauthorizedException`, `ForbiddenException`

---

### 2.7 Dashboard (Manager Panel)

#### Get dashboard metrics
- **Service**: `IDashboardService`
- **Method**: `Task<DashboardMetricsDto> GetDashboardMetricsAsync(CancellationToken ct = default)`
- **Description**: Operational metrics:
  - counts by status (New/InProgress/Done)
  - counts by stage (1..5)
  - urgent orders list (due < 7 days)
  - average order/project lead time *(if data available)*
- **Response**:

```csharp
public class DashboardMetricsDto
{
    public Dictionary<BatchStatus, int> CountsByStatus { get; set; }
    public Dictionary<int, int> CountsByStage { get; set; }
    public List<UrgentOrderDto> UrgentOrders { get; set; }
    public List<WarningDto> Warnings { get; set; }
}

public class UrgentOrderDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public DateTime DueDate { get; set; }
    public int ProjectId { get; set; }
    public string ProjectNumber { get; set; }
}
```

- **Authorization**: `[Authorize(Roles = "Manager")]`
- **Exceptions**: `UnauthorizedException`, `ForbiddenException`

---

## 3. Authentication and authorization

### 3.1. Authentication mechanism
- **Mechanizm**: ASP.NET Core Identity with **cookie authentication** (Blazor Server friendly).
- **Logowanie**: Włączone (`enabled="true"`). Użytkownicy po uruchomieniu aplikacji zawsze trafiają na ekran logowania.
- **Rejestracja**: Wyłączona (`enabled="false"`). Brak self-registration; konta tworzone przez skrypt seedujący.
- **Wymuszona autentykacja**: `forced-authentication="true"`. Użytkownicy niezalogowani nie mają dostępu do żadnych stron aplikacji poza logowaniem.
- **Przekierowanie po logowaniu**: Domyślnie na stronę główną (`/`), która przekierowuje do Kanban.

### 3.2. Roles
- **Manager**: full access to Manager Panel (CRUD formats, CRUD split rules, dashboard, order creation/history) and project shipping.
- **Operator**: Kanban read/write on batches (stage/status changes) and project details read.

### 3.3. Authorization boundaries
- **Manager-only operations**: create order; manage formats; manage split rules; ship project; dashboard; history views.
- **Shared operations**: Kanban list; batch status/stage updates; project details.
- **Public operations**: login page only (`/login`).

### 3.4. Implementation notes
- Use `[Authorize]` and `[Authorize(Roles="Manager")]`/policies at **service methods** (lub w komponentach Blazor).
- Prefer **policy-based checks** for fine-grained rules (e.g., `CanShipProject`).
- Serwisy mogą używać `IHttpContextAccessor` do dostępu do `HttpContext.User` dla autoryzacji programowej.
- Protect state-changing operations with standard ASP.NET Core antiforgery patterns where applicable (w komponentach Blazor).
- Global fallback policy: `RequireAuthenticatedUser()` dla wszystkich stron poza `/login`.

---

### 3.5. Authentication service methods

#### Login
- **Service**: `IAuthService` lub `SignInManager<TUser>`
- **Method**: `Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)`
- **Description**: Logowanie użytkownika przez username i password. Tworzy sesję cookie.
- **Request**:

```csharp
public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
}
```

- **Response**:

```csharp
public class AuthResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public UserDto? User { get; set; }
}

public class UserDto
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Role { get; set; } // "Manager" lub "Operator"
}
```

- **Authorization**: `[AllowAnonymous]` (dostęp bez logowania)
- **Exceptions**:
  - `UnauthorizedException`: nieprawidłowa nazwa użytkownika lub hasło
  - `ValidationException`: puste pola username/password

#### Get current user
- **Service**: `IAuthService` lub `UserManager<TUser>`
- **Method**: `Task<UserDto?> GetCurrentUserAsync(CancellationToken ct = default)`
- **Description**: Zwraca aktualnie zalogowanego użytkownika i jego uprawnienia na podstawie sesji/contextu.
- **Response**: `UserDto` (z Id, Username, Role) lub `null` jeśli użytkownik niezalogowany
- **Authorization**: `[Authorize]` (wymaga zalogowania)
- **Exceptions**: `UnauthorizedException` jeśli użytkownik niezalogowany

#### Logout
- **Service**: `IAuthService` lub `SignInManager<TUser>`
- **Method**: `Task LogoutAsync(CancellationToken ct = default)`
- **Description**: Wylogowanie użytkownika i wyczyszczenie sesji cookie.
- **Response**: void
- **Authorization**: `[Authorize]` (wymaga zalogowania)
- **Exceptions**: Brak (operacja zawsze się powodzi, nawet jeśli użytkownik już wylogowany)

## 4. Validation and business logic

### 4.1 Resource validation (database constraints + application rules)

#### ProductFormat
- **DB**: unique `name` (`uq_product_formats_name`), `is_active` default true.
- **App**: `name` required, trimmed; enforce reasonable max length.

#### Order
- **DB**:
  - unique `order_number` (`uq_orders_order_number`)
  - `quantity` check 1..100000 (`ck_orders_quantity_range`)
- **App**:
  - `due_date >= today + 7 days` (rule explicitly kept in app)
  - `product_format_id` must exist and be active (recommended)

#### Project
- **DB**:
  - unique `project_number` (`uq_projects_project_number`)
  - completion consistency (`ck_projects_completed_consistency`)
- **App**:
  - created automatically from order
  - shipping action sets `is_completed=true`, `completed_at`, `completed_by_user_id`

#### Batch
- **DB**:
  - unique `(project_id, batch_no)` (`uq_batches_project_batch_no`)
  - `quantity > 0` (`ck_batches_quantity_positive`)
  - `status` in {New, InProgress, Done} (`ck_batches_status_enum`)
  - `stage` in 1..5 (`ck_batches_stage_range`)
- **App**:
  - stage can only move forward (no back)
  - status transitions: **New → InProgress → Done** (assumption: no backward transitions in MVP)
  - **Soft limit**: if global `InProgress` count exceeds 20, return warning (no blocking)

#### BatchSplitRule
- **DB**: numeric constraints (minQty>=1, maxQty>=minQty or null, percent (0..100], etc.)
- **App**:
  - prevent overlaps among active rules (single-tenant; validate in Manager Panel)
  - used to compute batch sizes:
    - `baseSize = ceil(orderQty * percent / 100)`
    - create sequential batches of `baseSize`, last batch = remainder
    - ensure sum of batch quantities = order quantity
    - enforce `min_batch_size` and `max_batches_per_project` if configured

#### BatchAuditEvent
- **DB**:
  - must capture a change (`ck_batch_audit_has_change`)
  - domain constraints for status/stage
- **App**:
  - write audit entries **transactionally** with the batch update

### 4.2 Core business rules (PRD-driven)
- **Kanban scope**: show only projects where `projects.is_completed=false` (active work only).
- **Shipping gate ("Ship to customer")**:
  - Manager-only action.
  - Allowed only when **all project batches** satisfy:
    - `stage = 5 (Shipping)` **and**
    - `status = Done`
- **No self-registration**: seed initial accounts (Manager + Operator); UI only supports login.
- **N+1 avoidance / performance**:
  - Kanban list should use **projection** (single query join) rather than per-row loads.
  - Use partial indexes for active projects and `InProgress` counting where applicable.
  - `InProgress` count should be computed via aggregate query (and can be cached briefly per request/UI refresh).
