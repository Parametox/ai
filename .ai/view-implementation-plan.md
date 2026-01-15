# Clean Architecture Use Case Implementation Plan: KanbanLite MVP (kontrakt serwisów in-process)

> **Cel**: wdrożyć pełny przepływ UI (Blazor Server) → **warstwa aplikacyjna (serwisy/use case’y)** → **DataAccess (EF Core/PostgreSQL)** + **RBAC (Identity role: Manager/Operator)** + **audyt zmian batchy**.
>
> **Uwaga**: w MVP nie wystawiamy publicznego REST API. Kształt operacji jest opisany w `.ai/api-plan.md`, ale implementujemy go jako metody serwisów z `.ai/service-layer-plan.md`.

## 1. Przegląd operacji (use case)

Zakres MVP obejmuje zestaw operacji zgrupowanych w serwisy:

- **Product formats** (Panel Managera): listowanie, tworzenie, aktualizacja, dezaktywacja + lookup aktywnych formatów.
- **Batch split rules** (Panel Managera): listowanie, create/update, aktywacja/dezaktywacja; walidacja braku nakładania się aktywnych zakresów.
- **Orders** (Panel Managera): tworzenie zlecenia + automatyczne utworzenie projektu i batchy wg reguły; historia/lista; szczegóły.
- **Projects** (Kanban + akcje Managera): szczegóły projektu (z batchami) + akcja **Wyślij do klienta**.
- **Batches / Kanban** (Kanban): odczyt tablicy Kanban (projekcja join), zmiana statusu, zmiana etapu (tylko do przodu), soft-limit **InProgress > 20** jako ostrzeżenie.
- **Audit**: odczyt audytu batcha (transparentnie dla Manager+Operator) + zapis audytu transakcyjnie przy zmianach.
- **Dashboard** (Panel Managera): liczniki po statusach/etapach + lista pilnych zleceń (due < dziś+7).

Kluczowe reguły biznesowe:

- **RBAC**: role **Manager** (pełny zakres) i **Operator** (Kanban + szczegóły projektu).
- **Batch stage**: tylko „do przodu” (1..5).
- **Batch status**: w MVP tylko `New → InProgress → Done` (bez cofania).
- **Soft limit 20**: gdy globalnie `InProgressCount > 20`, zwracamy ostrzeżenie (nie blokujemy).
- **Ship project**: tylko Manager i tylko gdy wszystkie batche projektu mają `Stage=5 (Shipping)` i `Status=Done`.
- **Audyt**: zapisujemy kto/kiedy oraz stare/nowe wartości (status i/lub etap) **w tej samej transakcji** co zmiana batcha.

## 2. Kontrakt serwisu (Application)

### 2.1 Docelowe interfejsy (kontrakt in-process)

Kontrakt metod i DTO jest opisany w `.ai/service-layer-plan.md`. DTO/Request/Result są już zdefiniowane w `src/Types.cs` (`namespace KanbanLite.Contracts`).

Rekomendowane interfejsy (w warstwie aplikacyjnej, np. `src/Application/Services/...`):

- `IProductFormatService`
  - `Task<PagedResult<ProductFormatDto>> GetAsync(ProductFormatQuery query, CancellationToken ct = default)`
  - `Task<IReadOnlyList<ProductFormatLookupDto>> GetActiveLookupAsync(CancellationToken ct = default)`
  - `Task<ProductFormatDto> CreateAsync(CreateProductFormatRequest request, CancellationToken ct = default)`
  - `Task<ProductFormatDto> UpdateAsync(long id, UpdateProductFormatRequest request, CancellationToken ct = default)`
  - `Task<bool> DeactivateAsync(long id, CancellationToken ct = default)`

- `IBatchSplitRuleService`
  - `Task<IReadOnlyList<BatchSplitRuleDto>> GetAsync(BatchSplitRuleQuery query, CancellationToken ct = default)`
  - `Task<BatchSplitRuleDto> CreateAsync(UpsertBatchSplitRuleRequest request, CancellationToken ct = default)`
  - `Task<BatchSplitRuleDto> UpdateAsync(long id, UpsertBatchSplitRuleRequest request, CancellationToken ct = default)`
  - `Task<BatchSplitRuleDto> SetActiveAsync(long id, bool isActive, CancellationToken ct = default)`

- `IOrderService`
  - `Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)`
  - `Task<PagedResult<OrderListItemDto>> GetAsync(OrderQuery query, CancellationToken ct = default)`
  - `Task<OrderDetailsDto?> GetByIdAsync(long id, CancellationToken ct = default)`

- `IProjectService`
  - `Task<ProjectDetailsDto?> GetByIdAsync(long id, CancellationToken ct = default)`
  - `Task<ShipProjectResult> ShipToCustomerAsync(long projectId, CancellationToken ct = default)`

- `IBatchService`
  - `Task<KanbanBatchesResult> GetKanbanAsync(KanbanQuery query, CancellationToken ct = default)`
  - `Task<UpdateBatchStatusResult> UpdateStatusAsync(long batchId, UpdateBatchStatusRequest request, CancellationToken ct = default)`
  - `Task<UpdateBatchStageResult> UpdateStageAsync(long batchId, UpdateBatchStageRequest request, CancellationToken ct = default)`
  - `Task<int> GetInProgressCountAsync(CancellationToken ct = default)`

- `IBatchAuditService`
  - `Task<PagedResult<BatchAuditEventDto>> GetForBatchAsync(long batchId, PageQuery page, CancellationToken ct = default)`
  - `Task AddAsync(AddBatchAuditEventRequest request, CancellationToken ct = default)` *(zwykle internal w obrębie transakcji)*

- `IDashboardService`
  - `Task<DashboardDto> GetAsync(CancellationToken ct = default)`

### 2.2 Granice dostępu (gdzie sprawdzamy RBAC)

W MVP rekomendacja: **sprawdzenia RBAC na wejściu serwisu** (a w UI dodatkowo ukrywanie akcji/sekcji).

- Manager-only: `OrderService.*`, `ProductFormatService.(CRUD + pełna lista)`, `BatchSplitRuleService.*`, `ProjectService.ShipToCustomerAsync`, `DashboardService.GetAsync`
- Manager+Operator: `BatchService.*`, `ProjectService.GetByIdAsync`, `BatchAuditService.GetForBatchAsync` (rekomendowane)

## 3. Modele danych (DTO/Request/Result + mapowania)

### 3.1 Stan aktualny w repo

- **Encje + mapowania EF + constrainty/indeksy**: `src/DataAccess/*` (w tym `AppDbContext.cs` i `Entities/*`).
- **DTO/Request/Result**: `src/Types.cs` (`KanbanLite.Contracts`) – zgodne z `.ai/api-plan.md` i `.ai/service-layer-plan.md`.

### 3.2 Mapowania encje → DTO (najważniejsze)

- `ProductFormat` → `ProductFormatDto` / `ProductFormatLookupDto`
- `BatchSplitRule` → `BatchSplitRuleDto`
- `Order` + `ProductFormat` → `OrderDto` / `OrderListItemDto` / `OrderDetailsDto`
- `Project` + `Order` + `ProductFormat` + `Batches` → `ProjectDetailsDto`
- `Batch` + `Project` + `Order` → `KanbanBatchDto` (projekcja join)
- `BatchAuditLog` → `BatchAuditEventDto`
- `Dashboard`: agregacje na `batches` + projekcja pilnych na `orders/projects`

Reguły pochodne (w DTO):

- `progressPercent`: mapowanie etapu: 1→20, 2→40, 3→60, 4→80, 5→100 (zgodnie z `.ai/service-layer-plan.md`).

## 4. Przepływ danych i transakcje (UI → Application → DataAccess)

### 4.1 UI → serwisy (Blazor Server)

- Komponenty Blazor wywołują serwisy przez DI (in-process).
- UI mapuje wyjątki/Result na komunikaty (np. MudBlazor `ISnackbar`, dialogi walidacyjne).
- UI odświeża listę Kanban/Dashboard po zmianach (w MVP: proste „refresh” bez real-time broadcastu; opcjonalnie później SignalR/Hubs).

### 4.2 Serwisy → EF Core (jednostka pracy)

- Serwisy korzystają bezpośrednio z `AppDbContext` (Scoped).
- Dla operacji modyfikujących:
  - walidacja wejścia
  - pobranie encji (po kluczu) / weryfikacja uprawnień
  - zmiana stanu + zapis

### 4.3 Transakcje (wymagane miejsca)

**Wymagane transakcje (atomiczność):**

- `BatchService.UpdateStatusAsync`: zmiana `batches.status` + insert `batch_audit_log`.
- `BatchService.UpdateStageAsync`: zmiana `batches.stage` (+ `updated_at`) + insert `batch_audit_log`.
- `OrderService.CreateAsync`: insert `orders` + insert `projects` + insert `batches` (w tym wyliczenie batchy) – jedna transakcja.
- `ProjectService.ShipToCustomerAsync`: ustawienie completion pól w `projects` (plus ewentualny wpis audytu w przyszłości, jeśli dodamy audit projektów) – jedna transakcja.

W implementacji: `await using var tx = await db.Database.BeginTransactionAsync(ct); ... await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);`

## 5. Autoryzacja i bezpieczeństwo (Identity/RBAC, antiforgery, granice dostępu)

### 5.1 Identity + role

- Mechanizm: ASP.NET Core Identity (cookie auth).
- Role: `Manager`, `Operator`.
- Seed lokalny jest już przygotowany w `tests/DbSeed.IntegrationTests/UnitTest1.cs` (jednorazowy, sterowany env var).

### 5.2 Granice dostępu

- UI ma `[Authorize]` na obszarach aplikacji; Panel Managera `[Authorize(Roles="Manager")]`.
- Serwisy na wejściu weryfikują role (np. przez `IAuthorizationService` albo `ClaimsPrincipal` z `AuthenticationStateProvider`/`IHttpContextAccessor` – zależnie od hosta).

### 5.3 CSRF / antiforgery

- W Blazor Server operacje są wywoływane w ramach uwierzytelnionego circuit (SignalR). Dla ewentualnych endpointów HTTP (login, minimal APIs, healthchecks) stosować standardowe zabezpieczenia ASP.NET Core.
- Wymusić bezpieczne ustawienia cookies (HTTPS, SameSite adekwatnie do środowiska).

## 6. Walidacja i reguły biznesowe (DB constraints vs app rules)

### 6.1 Walidacje wejścia (aplikacja)

- **ProductFormat**
  - `Name`: trim, wymagane, max długość (np. 200).
  - Unikalność: obsłużyć konflikt (DB: `uq_product_formats_name`).
- **Order**
  - `Quantity`: 1..100000 (DB check).
  - `DueDate >= dziś + 7 dni` (tylko w aplikacji).
  - `ProductFormatId` istnieje i rekomendowane: format aktywny.
  - `OrderNumber`: wymagane, trim, unikalne (DB).
  - Split rules: musi istnieć dopasowana aktywna reguła; walidacja min batch size / max batches.
- **BatchSplitRule**
  - wartości liczbowe wg `.ai/service-layer-plan.md` + brak overlapów aktywnych przed zapisem.
- **Batch**
  - `UpdateStage`: `newStage > oldStage` i `newStage` w 1..5.
  - `UpdateStatus`: tylko przejście o 1 krok (`New→InProgress→Done`).
- **Project shipping**
  - wszystkie batche projektu: `Stage == Shipping` i `Status == Done`.

### 6.2 DB constraints (już zaimplementowane)

Constrainty i indeksy z `.ai/db-plan.md` są już skonfigurowane w `src/DataAccess/AppDbContext.cs` (unikaty, check constrainty, partial indexy).

## 7. Obsługa błędów (strategia, kody/błędy domenowe, mapowanie na UI)

### 7.1 Strategia w MVP (bez HTTP)

Rekomendacja: **wyjątki aplikacyjne z kodem** + mapowanie w UI:

- `ValidationException` (z listą błędów pól)
- `NotFoundException` (np. batch/project nie istnieje)
- `ConflictException` (unikaty, konflikty stanu)
- `BusinessRuleException` (np. niedozwolone przejście statusu/etapu, `CannotShipProject`)

UI łapie wyjątki, pokazuje komunikaty (snackbar/dialog), a logi lecą do loggera.

### 7.2 Mapowanie konfliktów DB

- Unique constraint → `ConflictException` (np. `uq_orders_order_number`, `uq_product_formats_name`).
- Check constraint (rzadziej w normalnym flow) → `ValidationException`/`BusinessRuleException` zależnie od kontekstu.

## 8. Wydajność i spójność (projekcje, unikanie N+1, agregacje, concurrency)

### 8.1 Unikanie N+1 (krytyczne miejsca)

- **Kanban**: jedna projekcja (join `batches` + `projects` + `orders`) + filtrowanie `projects.is_completed=false`.
- **Project details**: preferować pojedyncze zapytanie projekcyjne (albo kontrolowane `Include`/`ThenInclude` z rozsądnym zakresem).
- **Dashboard**: agregacje (`GroupBy`) dla statusów/etapów; pilne zlecenia jako osobna projekcja.

### 8.2 Soft limit InProgress > 20

- Liczyć przez agregację (z użyciem partial indexu `ix_batches_inprogress`).
- Zwracać `WarningDto` gdy `count > 20` (w `GetKanbanAsync`, `UpdateStatusAsync`, `DashboardService`).

### 8.3 Concurrency (opcjonalne w MVP)

- W MVP można pominąć kontrolę współbieżności.
- Jeśli okaże się potrzebne: dodać `row_version` (lub użyć `xmin`) i sprawdzać na update (konflikt → `ConflictException`).

## 9. Kroki implementacji (konkretne zadania, pliki/klasy, DI, testy)

### 9.1 Uporządkowanie warstw (Clean Architecture)

1. Dodać projekty:
   - `src/Contracts` *(opcjonalnie; docelowo przenieść `src/Types.cs` do osobnego projektu i referencjonować z UI/Application/DataAccess)*.
   - `src/Application` (interfejsy serwisów, implementacje, wyjątki, walidacje).
   - `src/Web` (Blazor Server + MudBlazor + Identity + DI, strony/komponenty UI).
2. Referencje:
   - `Application` → `DataAccess` + `Contracts`
   - `Web` → `Application` + `Contracts` + `DataAccess` (tylko do rejestracji DbContext)

### 9.2 Implementacja serwisów (Application)

3. Utworzyć foldery:
   - `src/Application/Services/*` (implementacje)
   - `src/Application/Abstractions/*` (interfejsy, ewentualne helpery)
   - `src/Application/Errors/*` (wyjątki + kody)
4. Zaimplementować serwisy zgodnie z `.ai/service-layer-plan.md`:
   - `ProductFormatService` (CRUD + lookup)
   - `BatchSplitRuleService` (walidacja overlapów)
   - `OrderService` (split na batche + transakcja)
   - `BatchService` (Kanban projekcja + update status/stage + audyt transakcyjny)
   - `BatchAuditService` (read + add w transakcji)
   - `ProjectService` (details + ship)
   - `DashboardService` (agregacje + pilne)

### 9.3 Seed i Identity (Web)

5. W `src/Web` skonfigurować:
   - Identity (cookie auth)
   - role `Manager`, `Operator`
   - seed kont (przenieść logikę z testu do jawnego seeda uruchamianego w dev, albo utrzymać test jako manualny seed zgodnie z README).

### 9.4 UI (Blazor + MudBlazor)

6. Widoki:
   - Kanban: tabela/lista batchy + filtry (status/stage/q) + akcje update (status/stage) + ostrzeżenie soft limit.
   - Panel Managera: dashboard + reguły splitu + formaty + tworzenie zlecenia + historia.
   - Szczegóły projektu: lista batchy + warunek wysyłki + podgląd audytu.
7. Obsługa błędów:
   - wspólny komponent/serwis do mapowania wyjątków na UI (snackbar/dialog) + log.

### 9.5 Testy

8. Testy integracyjne serwisów (Postgres w Docker):
   - `OrderService.CreateAsync`: poprawne tworzenie projektu + batchy (sumy, minBatchSize, maxBatchesPerProject).
   - `BatchService.UpdateStageAsync/UpdateStatusAsync`: walidacje przejść + wpis w `batch_audit_log`.
   - `ProjectService.ShipToCustomerAsync`: blokada gdy batche nie spełniają kryteriów.
   - `BatchSplitRuleService`: walidacja overlapów aktywnych.

### 9.6 Dokumentacja

9. Po wdrożeniu warstwy aplikacyjnej i hosta Blazor, uaktualnić README:
   - jak uruchomić `src/Web`
   - jak seedować role/użytkowników w dev
   - jakie env var są wspierane (`KANBANLITE_CONNECTION_STRING`, ewentualnie flagi seeda)

---

## Implementacja endpointu (workflow 3×3)

Twoim zadaniem jest wdrożenie **operacji aplikacyjnej (use case)** w oparciu o podany plan wdrożenia. Twoim celem jest stworzenie solidnej i dobrze zorganizowanej implementacji zgodnej z **Clean Architecture** (Blazor Server in-process), która zawiera odpowiednią walidację, obsługę błędów, autoryzację i podąża za logicznymi krokami opisanymi w planie.

Najpierw dokładnie przejrzyj dostarczony plan wdrożenia:

<implementation_plan>
@.ai/view-implementation-plan.md
</implementation_plan>

<types>
@src/Types.cs
@.ai/api-plan.md
@.ai/service-layer-plan.md
</types>

<implementation_rules>
@.cursor/rules/dev.mdc
@.cursor/rules/backend.mdc
@.cursor/rules/frontend.mdc
</implementation_rules>

<implementation_approach>
Realizuj maksymalnie 3 kroki planu implementacji, podsumuj krótko co zrobiłeś i opisz plan na 3 kolejne działania - zatrzymaj w tym momencie pracę i czekaj na mój feedback.
</implementation_approach>

Teraz wykonaj następujące kroki, aby zaimplementować operację (use case) w Clean Architecture:

1. Przeanalizuj plan wdrożenia:
   - Określ **serwis i metodę** (np. `IOrderService.CreateAsync`) oraz gdzie mają żyć implementacje (Application).
   - Określ **model wejścia** (Request/Command/Query) oraz wszystkie oczekiwane pola.
   - Określ **model wyjścia** (DTO/Result) oraz mapowania encje → DTO (projekcje/joiny dla list).
   - Zrozum wymaganą logikę biznesową, granice transakcji i kroki przetwarzania danych.
   - Zwróć uwagę na szczególne wymagania dot. walidacji, RBAC (Manager/Operator), audytu oraz unikania **N+1 w EF Core**.

2. Rozpocznij implementację (bez publicznego REST API w MVP):
   - Zdefiniuj/uzupełnij interfejs serwisu i implementację metody (Application), zgodnie z kontraktem w planie.
   - Skonfiguruj wstrzykiwanie zależności (DI) i dostęp do `AppDbContext`.
   - Zaimplementuj walidację wejścia (aplikacyjną) i normalizację danych (np. `Trim()`).
   - Zaimplementuj logikę biznesową zgodnie z planem (w tym transakcje i atomiczność).
   - Zaimplementuj obsługę błędów (wyjątki domenowe/aplikacyjne lub `Result<T>`) oraz logowanie.
   - Jeśli operacja modyfikuje batch: zapisz wpis do audytu **w tej samej transakcji**.

3. Walidacja i obsługa błędów (bez HTTP):
   - Zaimplementuj dokładną walidację danych wejściowych dla wszystkich pól.
   - Stosuj spójną strategię błędów: np. `ValidationException`, `NotFoundException`, `ConflictException`, `BusinessRuleException` z kodem.
   - Zapewnij jasne komunikaty błędów i mapowanie na UI (np. snackbar/dialog), bez polegania na statusach HTTP.
   - Obsłuż wyjątki z EF/DB (unikaty/check constrainty) i mapuj je na błędy aplikacyjne.

4. Rozważania dotyczące testowania:
   - Uwzględnij edge-case’y i scenariusze biznesowe z planu (przejścia statusu/etapu, shipping gate, overlap split rules, soft limit 20).
   - Dodaj/uzupełnij testy integracyjne serwisów (Postgres) dla kluczowych ścieżek.

5. Dokumentacja:
   - Dodaj krótkie komentarze tylko tam, gdzie logika jest nietrywialna (np. split na batche, walidacja overlapów, projekcje Kanbanu).
   - Uaktualnij dokumentację uruchomieniową (README) jeśli zmieniasz wymagania techniczne (seed, env var, migracje).

Po zakończeniu implementacji upewnij się, że kod zawiera wszystkie niezbędne importy, definicje typów, rejestracje DI oraz że nie wprowadzasz **N+1** w odczytach Kanbanu/Dashboardu.

Jeśli musisz przyjąć jakieś założenia lub masz pytania dotyczące planu implementacji, przedstaw je przed pisaniem kodu.

Pamiętaj, aby przestrzegać najlepszych praktyk Clean Architecture, stosować się do wytycznych stylu oraz upewnić się, że kod jest czysty, czytelny i dobrze zorganizowany.

