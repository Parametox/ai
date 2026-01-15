# Backend Service Plan (Blazor Server)

Dokument opisuje plan warstwy serwisów backendowych dla **KanbanLite MVP** jako **kontrakt aplikacyjny** wywoływany in-process przez UI Blazor Server (bez osobnego publicznego REST API w MVP).

## 1. Lista Serwisów i Rejestracja DI

Zakładamy proste serwisy `Scoped` (korzystają z `DbContext` per-request/circuit) oraz wstrzykiwanie przez DI.

```csharp
builder.Services.AddScoped<IProductFormatService, ProductFormatService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IBatchSplitRuleService, BatchSplitRuleService>();
builder.Services.AddScoped<IBatchAuditService, BatchAuditService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
```

Uwagi:
- **Auth/RBAC**: ASP.NET Core Identity (cookie auth) + role **Manager** / **Operator**. Autoryzacja może być na poziomie stron/komponentów oraz/lub na wejściu do metod serwisów (np. przez `IAuthorizationService`).
- **Wydajność**: unikać **N+1 w EF Core**. Kanban i dashboard budować przez **projekcje** (jedno zapytanie) + agregacje.

## 2. Szczegóły Serwisów

### 2.1 `ProductFormatService` (tabela `product_formats`)

**Odpowiedzialność:** słownik formatów produktów (CRUD w Panelu Managera) + lookup dla formularza tworzenia zlecenia.

- **Metody:**
  - `Task<PagedResult<ProductFormatDto>> GetAsync(ProductFormatQuery query, CancellationToken ct = default)`
  - `Task<IReadOnlyList<ProductFormatLookupDto>> GetActiveLookupAsync(CancellationToken ct = default)`
  - `Task<ProductFormatDto> CreateAsync(CreateProductFormatRequest request, CancellationToken ct = default)`
  - `Task<ProductFormatDto> UpdateAsync(long id, UpdateProductFormatRequest request, CancellationToken ct = default)`
  - `Task<bool> DeactivateAsync(long id, CancellationToken ct = default)` *(zamiast delete w MVP)*

- **Opis:**
  - Lookup zwraca tylko aktywne formaty do dropdownów.
  - CRUD dla Managera; Operator w MVP nie musi widzieć pełnej listy, ale może czytać aktywne formaty do UI.

- **Walidacja:**
  - `name` wymagane, trim, sensowny limit długości (np. 200).
  - Unikalność `name` (DB: `uq_product_formats_name`).

- **Uprawnienia:**
  - `GetActiveLookupAsync`: Manager i Operator (dla tworzenia zleceń/odczytu).
  - `GetAsync/CreateAsync/UpdateAsync/DeactivateAsync`: tylko **Manager**.

---

### 2.2 `BatchSplitRuleService` (tabela `batch_split_rules`)

**Odpowiedzialność:** zarządzanie konfiguracją dzielenia zleceń na batche (Panel Managera).

- **Metody:**
  - `Task<IReadOnlyList<BatchSplitRuleDto>> GetAsync(BatchSplitRuleQuery query, CancellationToken ct = default)`
  - `Task<BatchSplitRuleDto> CreateAsync(UpsertBatchSplitRuleRequest request, CancellationToken ct = default)`
  - `Task<BatchSplitRuleDto> UpdateAsync(long id, UpsertBatchSplitRuleRequest request, CancellationToken ct = default)`
  - `Task<BatchSplitRuleDto> SetActiveAsync(long id, bool isActive, CancellationToken ct = default)`

- **Opis:**
  - Reguły dotyczą **tylko nowych zleceń** (istniejące projekty/batche nie są przebudowywane).

- **Walidacja:**
  - `minQty >= 1`
  - `maxQty == null || maxQty >= minQty`
  - `percent > 0 && percent <= 100`
  - `minBatchSize >= 1`
  - `maxBatchesPerProject == null || maxBatchesPerProject >= 1`
  - **Brak nakładania się aktywnych zakresów** (walidacja w aplikacji; w przyszłości opcjonalny constraint w Postgres).

- **Uprawnienia:**
  - Wszystkie metody: tylko **Manager**.

---

### 2.3 `OrderService` (tabela `orders` + tworzenie `projects` + `batches`)

**Odpowiedzialność:** tworzenie zleceń i projekcji historii zleceń.

- **Metody:**
  - `Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)`
  - `Task<PagedResult<OrderListItemDto>> GetAsync(OrderQuery query, CancellationToken ct = default)`
  - `Task<OrderDetailsDto?> GetByIdAsync(long id, CancellationToken ct = default)`

- **Opis:**
  - `CreateAsync` tworzy:
    - rekord `orders`
    - rekord `projects` powiązany z `orders`
    - batche `batches` wyliczone na podstawie aktywnej reguły `batch_split_rules`
  - Zwraca wynik z `OrderDto`, `ProjectDto` oraz listą utworzonych `BatchDto`.

- **Walidacja:**
  - `quantity` 1..100000 (DB: `ck_orders_quantity_range`).
  - `dueDate >= dziś + 7 dni` (w aplikacji).
  - `productFormatId` musi istnieć i (rekomendowane) być aktywny.
  - `orderNumber` unikalny (DB: `uq_orders_order_number`).
  - Reguła splitu:
    - dopasowanie do zakresu `minQty/maxQty` dla ilości zlecenia,
    - `baseSize = ceil(orderQty * percent / 100)`,
    - batche po `baseSize`, ostatni batch = reszta, suma = `orderQty`,
    - respektowanie `minBatchSize` oraz `maxBatchesPerProject` (jeśli ustawione).

- **Uprawnienia:**
  - Wszystkie metody: tylko **Manager** (Panel Managera / historia).

---

### 2.4 `ProjectService` (tabela `projects`, odczyty + akcja “Wyślij do klienta”)

**Odpowiedzialność:** widok szczegółów projektu i zamknięcie projektu (wysyłka do klienta).

- **Metody:**
  - `Task<PagedResult<ProjectListItemDto>> GetAsync(ProjectQuery query, CancellationToken ct = default)` *(opcjonalne; jeśli potrzebne do list widoków)*
  - `Task<ProjectDetailsDto?> GetByIdAsync(long id, CancellationToken ct = default)` *(z batchami + informacją o gotowości do wysyłki)*
  - `Task<ShipProjectResult> ShipToCustomerAsync(long projectId, CancellationToken ct = default)`

- **Opis:**
  - Kanban pokazuje tylko projekty aktywne (`is_completed=false`).
  - `ShipToCustomerAsync` ustawia `is_completed=true`, `completed_at`, `completed_by_user_id`.

- **Walidacja:**
  - “Wyślij do klienta” dozwolone tylko gdy **wszystkie batche** projektu spełniają:
    - `stage = 5 (Wysyłka)` **i**
    - `status = Done`
  - Spójność completion w DB (constraint `ck_projects_completed_consistency`).

- **Uprawnienia:**
  - `GetByIdAsync`: Manager i Operator.
  - `ShipToCustomerAsync`: tylko **Manager**.

---

### 2.5 `BatchService` (tabela `batches` + soft limit 20 InProgress)

**Odpowiedzialność:** główny model Kanbanu i operacje na batchach (status/etap).

- **Metody:**
  - `Task<PagedResult<KanbanBatchDto>> GetKanbanAsync(KanbanQuery query, CancellationToken ct = default)`
  - `Task<BatchDetailsDto?> GetByIdAsync(long id, CancellationToken ct = default)` *(opcjonalne)*
  - `Task<UpdateBatchStatusResult> UpdateStatusAsync(long batchId, UpdateBatchStatusRequest request, CancellationToken ct = default)`
  - `Task<UpdateBatchStageResult> UpdateStageAsync(long batchId, UpdateBatchStageRequest request, CancellationToken ct = default)`
  - `Task<int> GetInProgressCountAsync(CancellationToken ct = default)`

- **Opis:**
  - Kanban listuje batche z kontekstem `project/order` dla projektów aktywnych.
  - Po zmianach statusu/etapu zapisuje wpis w audycie (we współpracy z `BatchAuditService`) **transakcyjnie**.
  - Soft limit 20 dotyczy **globalnie** liczby batchy `InProgress`. Po przekroczeniu serwis zwraca ostrzeżenie (nie blokuje akcji).

- **Walidacja:**
  - `status`: tylko wartości `New`, `InProgress`, `Done` (DB: `ck_batches_status_enum`).
  - `stage`: 1..5 (DB: `ck_batches_stage_range`).
  - **Etap tylko do przodu** (PRD).
  - **Zmiana statusu**: w MVP zakładamy `New → InProgress → Done` bez cofania.
  - **Soft limit**: jeśli `InProgressCount > 20`, zwrócić `Warning` (np. kod `InProgressSoftLimitExceeded`).

- **Uprawnienia:**
  - `GetKanbanAsync`, `UpdateStatusAsync`, `UpdateStageAsync`: Manager i Operator.

---

### 2.6 `BatchAuditService` (tabela `batch_audit_log`)

**Odpowiedzialność:** zapis i odczyt historii zmian batchy (kto/kiedy/co).

- **Metody:**
  - `Task<PagedResult<BatchAuditEventDto>> GetForBatchAsync(long batchId, PageQuery page, CancellationToken ct = default)`
  - `Task AddAsync(AddBatchAuditEventRequest request, CancellationToken ct = default)` *(zwykle wołane wewnętrznie przez `BatchService`)*

- **Opis:**
  - Audyt jest zapisywany **w tej samej transakcji** co aktualizacja batcha.

- **Walidacja:**
  - Co najmniej jedna zmiana (DB: `ck_batch_audit_has_change`).

- **Uprawnienia:**
  - Odczyt audytu: Manager (minimalnie) lub Manager+Operator (transparentność). W MVP rekomendacja: **Manager+Operator** dla podglądu w szczegółach projektu.

---

### 2.7 `DashboardService` (metryki Panelu Managera)

**Odpowiedzialność:** metryki operacyjne (statusy/etapy/pilne zlecenia).

- **Metody:**
  - `Task<DashboardDto> GetAsync(CancellationToken ct = default)`

- **Opis:**
  - Liczniki po statusach i etapach.
  - Lista pilnych zleceń/projektów: `due_date < dziś + 7 dni`.
  - (Opcjonalnie) średni czas realizacji — jeśli/ gdy pojawią się dane do policzenia.

- **Walidacja:**
  - Brak (odczyt).

- **Uprawnienia:**
  - Tylko **Manager**.

## 3. Modele Danych (DTOs)

Minimalny zestaw DTO dla UI i serwisów (rekordy/klasy):

- `ProductFormatDto`, `ProductFormatLookupDto`, `CreateProductFormatRequest`, `UpdateProductFormatRequest`, `ProductFormatQuery`
- `BatchSplitRuleDto`, `UpsertBatchSplitRuleRequest`, `BatchSplitRuleQuery`
- `CreateOrderRequest`, `CreateOrderResult`, `OrderListItemDto`, `OrderDetailsDto`, `OrderQuery`
- `ProjectListItemDto`, `ProjectDetailsDto`, `ProjectQuery`, `ShipProjectResult`
- `KanbanBatchDto`, `KanbanQuery`, `BatchDetailsDto`, `UpdateBatchStatusRequest`, `UpdateBatchStatusResult`, `UpdateBatchStageRequest`, `UpdateBatchStageResult`
- `BatchAuditEventDto`, `AddBatchAuditEventRequest`
- `DashboardDto`
- Wspólne: `PagedResult<T>`, `PageQuery`

Uwagi implementacyjne:
- DTO dla Kanbanu powinno być **projekcją** (join `batches` + `projects` + `orders`), żeby uniknąć **N+1**.
- `progressPercent` liczyć jako proste mapowanie etapu: 1→20, 2→40, 3→60, 4→80, 5→100.

## 4. Obsługa Błędów

Prosta strategia bez ciężkich frameworków:

- **Walidacja wejścia**: rzucanie `ValidationException` / `ArgumentException` lub zwracanie rezultatu typu `Result<T>` (jeśli preferowane). W MVP rekomendacja: wyjątki domenowe + mapowanie do komunikatów UI.
- **Błędy biznesowe** (np. niedozwolone przejście statusu/etapu, próba wysyłki niespełniająca warunków): rzucanie `BusinessRuleException` z kodem (np. `InvalidStageTransition`, `CannotShipProject`).
- **Brak encji**: zwracanie `null` dla `GetByIdAsync` lub rzucanie `NotFoundException` dla akcji modyfikujących.
- **Konflikty unikalności**: mapowanie wyjątków DB/EF (np. unique constraint) na `ConflictException`.
- **Transakcje**: aktualizacja batcha + wpis audytu w jednej transakcji (`BeginTransactionAsync`), żeby nie powstawały “pół-zapisy”.

## 5. Założenia i punkty do doprecyzowania (z PRD)

Poniższe punkty były oznaczone w PRD jako nierozstrzygnięte — plan serwisów przyjmuje domyślne, proste założenia MVP:

- **Semantyka ostrzeżenia limitu 20**: ostrzegamy, gdy faktycznie `InProgressCount > 20` po wykonaniu zmiany statusu na `InProgress` (oraz w odczytach Kanbanu/Dashboardu).
- **Reguły przejść statusów**: przyjmujemy brak powrotów (tylko `New → InProgress → Done`). Jeśli jednak dopuszczamy cofanie, trzeba rozszerzyć walidację w `UpdateStatusAsync`.
- **“Done” vs etap**: przyjmujemy, że batch może mieć `status=Done` na wcześniejszym etapie, ale **wysyłka projektu** wymaga jednocześnie `stage=5` i `status=Done` dla wszystkich batchy.

