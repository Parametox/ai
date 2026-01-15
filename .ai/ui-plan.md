# Plan implementacji UI - KanbanLite MVP

> Dokument opisuje szczegółowy plan implementacji warstwy prezentacji (Blazor Server + MudBlazor) dla aplikacji KanbanLite MVP.

## 1. Rozwiązania dla nierozwiązanych kwestii z PRD

### 1.1. Semantyka ostrzeżenia limitu InProgress

**Rozwiązanie:**
- Ostrzeżenie wyświetlane **tylko przy faktycznym przekroczeniu** (InProgress > 20).
- Przy tworzeniu zlecenia **nie pokazujemy ostrzeżenia potencjalnego** — nowe batche startują jako `New`, więc nie wpływają na limit.
- Ostrzeżenie pojawia się w:
  - **Czasie rzeczywistym** przy zmianie statusu na `InProgress` (jeśli po zmianie count > 20).
  - **Widoku Kanban** jako `MudAlert` z typem `Severity.Warning` gdy `inProgressCount > 20`.
  - **Dashboardzie Managera** w sekcji metryk.
  - **Nagłówku aplikacji** jako badge/ikona z liczbą (dla szybkiego dostępu).

**Implementacja:**
- Licznik `InProgressCount` odświeżany po każdej zmianie statusu batcha.
- Okresowe odświeżanie co 30 sekund przez `BatchService.GetInProgressCountAsync()`.
- Komponent `InProgressCounter` w nagłówku z `MudBadge` i `MudIcon`.

---

### 1.2. Reguły przejść statusów

**Rozwiązanie:**
- Statusy: **tylko do przodu** — `New → InProgress → Done` (bez powrotów).
- Etapy: **tylko do przodu** — `1 → 2 → 3 → 4 → 5` (bez powrotów).

**Dopuszczalne przejścia:**
- `New → InProgress` ✅
- `InProgress → Done` ✅
- `InProgress → InProgress` (bez zmiany) ✅ (przy zmianie tylko etapu)
- `Done → Done` (bez zmiany) ✅ (przy zmianie tylko etapu)

**Niedopuszczalne:**
- `InProgress → New` ❌
- `Done → InProgress` ❌
- `Done → New` ❌

**Implementacja:**
- W `MudSelect` dla statusu: wyłączyć opcje, które nie są dozwolone (np. gdy status = Done, ukryć New i InProgress).
- Walidacja po stronie serwera w `BatchService.UpdateStatusAsync()`.
- Komunikaty błędów przez `MudSnackbar` przy próbie nieprawidłowej zmiany.

---

### 1.3. Definicja "Done" vs etap

**Rozwiązanie:**
- Status `Done` może być ustawiony **niezależnie od etapu**.
- Warunek wysyłki projektu: wszystkie batche muszą mieć `stage = 5 (Shipping)` **i** `status = Done`.

**Przykłady:**
- Batch na etapie 2 (Druk) z `status = Done` — **możliwe** (np. wstrzymanie pracy).
- Batch na etapie 5 (Shipping) z `status = InProgress` — **nie można wysłać projektu**.
- Batch na etapie 5 (Shipping) z `status = Done` — **spełnia warunek wysyłki**.

**Implementacja:**
- W widoku projektu: przycisk "Wyślij do klienta" aktywny tylko gdy `Completion.CanShip = true`.
- Wyświetlanie `Completion.Reason` jako tooltip lub komunikat, gdy wysyłka niemożliwa.
- Wizualne oznaczenie batchy, które blokują wysyłkę (np. czerwony border).

---

### 1.4. Szczegóły dashboardu

**Rozwiązanie:**

#### Metryki dashboardu:
1. **Rozkład batchy po statusach:**
   - `New`: liczba batchy ze statusem New (tylko z aktywnych projektów)
   - `InProgress`: liczba batchy ze statusem InProgress (tylko z aktywnych projektów)
   - `Done`: liczba batchy ze statusem Done (tylko z aktywnych projektów)

2. **Rozkład batchy po etapach:**
   - Etap 1 (Projektowanie): liczba batchy na etapie 1
   - Etap 2 (Druk): liczba batchy na etapie 2
   - Etap 3 (Cięcie): liczba batchy na etapie 3
   - Etap 4 (Pakowanie): liczba batchy na etapie 4
   - Etap 5 (Wysyłka): liczba batchy na etapie 5

3. **Lista pilnych zleceń:**
   - Zlecenia z `dueDate < today + 7 days` i `project.IsCompleted = false`
   - Sortowanie: `dueDate ASC`
   - Limit: **10 najpilniejszych**
   - Wyświetlane dane: `orderNumber`, `projectNumber`, `dueDate`, `quantity`, liczba batchy InProgress dla projektu

4. **Średni czas realizacji zlecenia:**
   - Obliczanie: `AVG(project.CompletedAt - project.CreatedAt)` dla projektów z `IsCompleted = true`
   - Zakres: **ostatnie 30 dni** (lub wszystkie ukończone, jeśli mniej niż 10)
   - Format: dni (np. "12.5 dni")
   - Jeśli brak ukończonych projektów: "Brak danych"

5. **Ostrzeżenia:**
   - `InProgressSoftLimitExceeded`: gdy `inProgressCount > 20`
   - `UrgentOrdersCount`: liczba pilnych zleceń (jeśli > 5, pokaż ostrzeżenie)

**Implementacja:**
- Endpoint `/dashboard` zwraca `DashboardMetricsDto` z wszystkimi metrykami.
- Komponenty `MudCard` dla każdej metryki.
- `MudChart` (opcjonalnie) dla wizualizacji rozkładu.
- `MudTable` dla listy pilnych zleceń z linkami do projektów.
- Auto-odświeżanie co 60 sekund przez `Timer` w Blazor.

---

## 2. Architektura komponentów

### 2.1. Struktura folderów

```
src/Web/
├── Components/
│   ├── Layout/
│   │   ├── MainLayout.razor
│   │   ├── MainLayout.razor.cs
│   │   ├── NavMenu.razor
│   │   ├── NavMenu.razor.cs
│   │   ├── Header.razor
│   │   └── Header.razor.cs
│   ├── Kanban/
│   │   ├── KanbanTable.razor
│   │   ├── KanbanTable.razor.cs
│   │   ├── BatchStatusSelect.razor
│   │   ├── BatchStatusSelect.razor.cs
│   │   ├── BatchStageSelect.razor
│   │   ├── BatchStageSelect.razor.cs
│   │   └── InProgressCounter.razor
│   ├── Project/
│   │   ├── ProjectDetails.razor
│   │   ├── ProjectDetails.razor.cs
│   │   ├── BatchList.razor
│   │   ├── BatchList.razor.cs
│   │   └── AuditLog.razor
│   ├── Manager/
│   │   ├── Dashboard/
│   │   │   ├── DashboardMetrics.razor
│   │   │   └── DashboardMetrics.razor.cs
│   │   ├── Orders/
│   │   │   ├── OrderForm.razor
│   │   │   ├── OrderForm.razor.cs
│   │   │   └── OrderList.razor
│   │   ├── Formats/
│   │   │   ├── FormatList.razor
│   │   │   ├── FormatList.razor.cs
│   │   │   ├── FormatForm.razor
│   │   │   └── FormatForm.razor.cs
│   │   └── SplitRules/
│   │       ├── SplitRuleList.razor
│   │       ├── SplitRuleList.razor.cs
│   │       ├── SplitRuleForm.razor
│   │       └── SplitRuleForm.razor.cs
│   └── Shared/
│       ├── ErrorBoundary.razor
│       ├── LoadingSpinner.razor
│       └── ConfirmDialog.razor
├── Pages/
│   ├── Index.razor (redirect to Kanban)
│   ├── Login.razor
│   ├── Login.razor.cs
│   ├── Kanban.razor
│   ├── Kanban.razor.cs
│   ├── Project/
│   │   └── ProjectDetails.razor
│   └── Manager/
│       ├── Dashboard.razor
│       ├── Dashboard.razor.cs
│       ├── Orders/
│       │   ├── Create.razor
│       │   └── History.razor
│       ├── Formats/
│       │   └── Index.razor
│       └── SplitRules/
│           └── Index.razor
└── Services/
    ├── IErrorHandler.cs
    ├── ErrorHandler.cs
    └── CacheService.cs
```

---

### 2.2. Routing

```csharp
// Program.cs lub App.razor
app.MapRazorPages();
app.MapBlazorHub();

// Routes:
/                          → Index (redirect to /kanban)
/login                     → Login.razor
/kanban                    → Kanban.razor (Operator + Manager)
/project/{id}              → ProjectDetails.razor (Operator + Manager)
/manager/dashboard         → Dashboard.razor (Manager only)
/manager/orders/create     → Create.razor (Manager only)
/manager/orders/history    → History.razor (Manager only)
/manager/formats           → Formats/Index.razor (Manager only)
/manager/split-rules       → SplitRules/Index.razor (Manager only)
```

---

## 3. Szczegóły implementacji widoków

### 3.1. Layout i nawigacja

#### MainLayout.razor
- **Komponenty MudBlazor:**
  - `MudLayout` z `DrawerOpen="true"`
  - `MudAppBar` w nagłówku z `InProgressCounter`
  - `MudDrawer` z `NavMenu`
  - `MudMainContent` dla `@Body`

#### NavMenu.razor
- **Komponenty MudBlazor:**
  - `MudNavMenu` z warunkowym wyświetlaniem:
    - Dla wszystkich: "Kanban" → `/kanban`
    - Dla Managera: "Panel Managera" z podmenu:
      - "Dashboard" → `/manager/dashboard`
      - "Utwórz zlecenie" → `/manager/orders/create`
      - "Historia zleceń" → `/manager/orders/history`
      - "Formaty produktów" → `/manager/formats`
      - "Konfiguracja batchowania" → `/manager/split-rules`
- **Autoryzacja:** `@attribute [Authorize]` + sprawdzanie roli przez `@inject IAuthorizationService`

#### Header.razor
- **Komponenty MudBlazor:**
  - `MudAppBar` z:
    - `MudText` z nazwą użytkownika
    - `InProgressCounter` (badge z liczbą)
    - `MudButton` "Wyloguj" → `/logout`
- **Logika:** `@inject AuthenticationStateProvider` do pobrania użytkownika

---

### 3.2. Widok Kanban

#### Kanban.razor
- **Komponenty MudBlazor:**
  - `MudContainer` z `MaxWidth="MaxWidth.ExtraLarge"`
  - `MudAlert` (jeśli `inProgressCount > 20`) z `Severity.Warning`
  - `MudCard` z filtrami:
    - `MudSelect` dla statusu (New/InProgress/Done/Wszystkie)
    - `MudSelect` dla etapu (1-5/Wszystkie)
    - `MudTextField` dla wyszukiwania (debounce 300ms)
    - `MudButton` "Wyczyść filtry"
  - `KanbanTable` (komponent)
  - `MudProgressLinear` podczas ładowania

#### KanbanTable.razor
- **Komponenty MudBlazor:**
  - `MudTable<KanbanBatchDto>` z kolumnami:
    - Numer zlecenia (link do `/project/{projectId}`)
    - Numer batcha
    - Liczba sztuk
    - Progress bar (`MudProgressLinear` z `Value="{item.ProgressPercent}"`)
    - Status (`BatchStatusSelect` - komponent)
    - Etap (`BatchStageSelect` - komponent)
    - Ostrzeżenie (ikona `MudIcon` jeśli `inProgressCount > 20`)
  - Wbudowana paginacja `MudTable` z `PageSize="50"`
  - Sortowanie: domyślnie `updatedAt DESC`

#### BatchStatusSelect.razor
- **Komponenty MudBlazor:**
  - `MudSelect<BatchStatus>` z wartościami: New, InProgress, Done
  - Wyłączenie opcji niedozwolonych (np. gdy status = Done, ukryć New i InProgress)
  - `OnSelectionChanged` → wywołanie `BatchService.UpdateStatusAsync()`
  - `MudProgressCircular` podczas zapisu

#### BatchStageSelect.razor
- **Komponenty MudBlazor:**
  - `MudSelect<int>` z wartościami: 1, 2, 3, 4, 5
  - Wyłączenie opcji mniejszych niż aktualny etap (tylko do przodu)
  - `OnSelectionChanged` → wywołanie `BatchService.UpdateStageAsync()`
  - `MudProgressCircular` podczas zapisu

#### InProgressCounter.razor
- **Komponenty MudBlazor:**
  - `MudBadge` z `Content="{inProgressCount}"` i `Color="Color.Warning"` (jeśli > 20)
  - `MudIcon` "Work" lub "Warning"
  - Auto-odświeżanie co 30 sekund przez `Timer`

**Logika Kanban.razor.cs:**
```csharp
private KanbanQuery _query = new();
private PagedResult<KanbanBatchDto>? _batches;
private int _inProgressCount;
private bool _loading;
private Timer? _refreshTimer;

protected override async Task OnInitializedAsync()
{
    await LoadDataAsync();
    _refreshTimer = new Timer(async _ => await RefreshInProgressCount(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
}

private async Task LoadDataAsync()
{
    _loading = true;
    try
    {
        _batches = await _batchService.GetKanbanAsync(_query);
        _inProgressCount = await _batchService.GetInProgressCountAsync();
    }
    finally
    {
        _loading = false;
        StateHasChanged();
    }
}

private async Task OnStatusChanged(int batchId, BatchStatus newStatus)
{
    var result = await _batchService.UpdateStatusAsync(batchId, newStatus);
    if (result.IsSuccess)
    {
        _inProgressCount = result.InProgressCount;
        await LoadDataAsync(); // Refresh table
        _snackbar.Add("Status zaktualizowany", Severity.Success);
    }
    else
    {
        _snackbar.Add(result.ErrorMessage, Severity.Error);
    }
}
```

---

### 3.3. Widok szczegółów projektu

#### ProjectDetails.razor
- **Komponenty MudBlazor:**
  - `MudContainer` z `MaxWidth="MaxWidth.Large"`
  - `MudCard` z informacjami o zleceniu:
    - Numer zlecenia, format produktu, ilość, termin realizacji
  - `MudCard` z informacją o gotowości do wysyłki:
    - `MudAlert` z `Severity.Info` jeśli `CanShip = true`
    - `MudAlert` z `Severity.Warning` jeśli `CanShip = false` + `Reason`
    - `MudButton` "Wyślij do klienta" (tylko Manager, tylko gdy `CanShip = true`)
  - `BatchList` (komponent) z wszystkimi batchami projektu
  - `AuditLog` (komponent) z historią zmian

#### BatchList.razor
- **Komponenty MudBlazor:**
  - `MudTable<BatchDto>` z kolumnami:
    - Numer batcha
    - Liczba sztuk
    - Status (read-only, kolorowany)
    - Etap (read-only, z nazwą etapu)
    - Progress bar
    - Oznaczenie wizualne (czerwony border) jeśli batch blokuje wysyłkę

#### AuditLog.razor
- **Komponenty MudBlazor:**
  - `MudExpansionPanels` z historią zmian
  - Dla każdego zdarzenia: data, użytkownik, zmiany (status/etap)

**Logika ProjectDetails.razor.cs:**
```csharp
private ProjectDetailsDto? _project;
private bool _loading;

protected override async Task OnInitializedAsync()
{
    var projectId = int.Parse(RouteData.Values["id"]?.ToString() ?? "0");
    _project = await _projectService.GetByIdAsync(projectId);
}

private async Task ShipProject()
{
    var confirmed = await _dialogService.ShowMessageBox(
        "Potwierdzenie",
        "Czy na pewno chcesz wysłać zlecenie do klienta?",
        yesText: "Tak", cancelText: "Anuluj");
    
    if (confirmed == true)
    {
        var result = await _projectService.ShipAsync(_project.Id);
        if (result.IsSuccess)
        {
            _snackbar.Add("Zlecenie wysłane do klienta", Severity.Success);
            NavigationManager.NavigateTo("/kanban");
        }
        else
        {
            _snackbar.Add(result.ErrorMessage, Severity.Error);
        }
    }
}
```

---

### 3.4. Panel Managera - Dashboard

#### Dashboard.razor
- **Komponenty MudBlazor:**
  - `MudContainer` z `MaxWidth="MaxWidth.ExtraLarge"`
  - `MudAlert` z ostrzeżeniem o przekroczeniu limitu InProgress (jeśli > 20)
  - `MudGrid` z metrykami:
    - `MudCard` z rozkładem po statusach (3 karty: New, InProgress, Done)
    - `MudCard` z rozkładem po etapach (5 kart lub wykres)
    - `MudCard` ze średnim czasem realizacji
  - `MudCard` z listą pilnych zleceń (`MudTable`)
  - Auto-odświeżanie co 60 sekund

**Logika Dashboard.razor.cs:**
```csharp
private DashboardMetricsDto? _metrics;
private Timer? _refreshTimer;

protected override async Task OnInitializedAsync()
{
    await LoadMetricsAsync();
    _refreshTimer = new Timer(async _ => await LoadMetricsAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
}

private async Task LoadMetricsAsync()
{
    _metrics = await _dashboardService.GetMetricsAsync();
    StateHasChanged();
}
```

---

### 3.5. Panel Managera - Formularz tworzenia zlecenia

#### Create.razor (Orders)
- **Komponenty MudBlazor:**
  - `MudContainer` z `MaxWidth="MaxWidth.Medium"`
  - `MudCard` z formularzem:
    - `MudTextField` dla numeru zlecenia (z auto-generacją lub ręcznym)
    - `MudSelect<ProductFormatLookupDto>` dla formatu produktu (z cache)
    - `MudNumericField<int>` dla ilości sztuk (1-100000)
    - `MudDatePicker` dla terminu realizacji (walidacja: ≥ dziś+7 dni)
    - `MudButton` "Utwórz zlecenie" (tylko Manager)
  - Walidacja inline przez `MudTextField.Error` i `MudTextField.ErrorText`

**Logika Create.razor.cs:**
```csharp
private CreateOrderDto _model = new();
private List<ProductFormatLookupDto> _formats = new();
private bool _loading;

protected override async Task OnInitializedAsync()
{
    _formats = await _cacheService.GetOrSetAsync(
        "product_formats_lookup",
        async () => await _formatService.GetActiveLookupAsync(),
        TimeSpan.FromMinutes(5));
}

private async Task CreateOrder()
{
    _loading = true;
    try
    {
        var result = await _orderService.CreateAsync(_model);
        if (result.IsSuccess)
        {
            _snackbar.Add("Zlecenie utworzone", Severity.Success);
            NavigationManager.NavigateTo($"/project/{result.Project.Id}");
        }
        else
        {
            _snackbar.Add(result.ErrorMessage, Severity.Error);
        }
    }
    finally
    {
        _loading = false;
    }
}
```

---

### 3.6. Panel Managera - Historia zleceń

#### History.razor (Orders)
- **Komponenty MudBlazor:**
  - `MudContainer` z `MaxWidth="MaxWidth.ExtraLarge"`
  - `MudCard` z filtrami:
    - `MudTextField` dla wyszukiwania (orderNumber/projectNumber)
    - `MudDatePicker` dla `dueFrom`
    - `MudDatePicker` dla `dueTo`
    - `MudButton` "Wyczyść filtry"
  - `MudTable<OrderDto>` z kolumnami:
    - Numer zlecenia (link do `/project/{projectId}`)
    - Ilość, format, termin realizacji
    - Data utworzenia
  - Wbudowana paginacja

---

### 3.7. Panel Managera - Formaty produktów

#### Index.razor (Formats)
- **Komponenty MudBlazor:**
  - `MudContainer` z `MaxWidth="MaxWidth.ExtraLarge"`
  - `MudButton` "Dodaj format" (otwiera dialog)
  - `MudTable<ProductFormatDto>` z kolumnami:
    - Nazwa
    - Status (Aktywny/Nieaktywny)
    - Data utworzenia
    - Akcje (Edytuj, Usuń/Deaktywuj)
  - `MudDialog` z `FormatForm` dla tworzenia/edycji

#### FormatForm.razor
- **Komponenty MudBlazor:**
  - `MudTextField` dla nazwy
  - `MudSwitch` dla `IsActive`
  - `MudButton` "Zapisz" / "Anuluj"

---

### 3.8. Panel Managera - Konfiguracja batchowania

#### Index.razor (SplitRules)
- **Komponenty MudBlazor:**
  - `MudContainer` z `MaxWidth="MaxWidth.ExtraLarge"`
  - `MudButton` "Dodaj regułę" (otwiera dialog)
  - `MudTable<BatchSplitRuleDto>` z kolumnami:
    - Min ilość
    - Max ilość
    - Procent
    - Min rozmiar batcha
    - Max batchy na projekt
    - Status (Aktywna/Nieaktywna)
    - Akcje (Edytuj, Deaktywuj)
  - `MudDialog` z `SplitRuleForm` dla tworzenia/edycji
  - Walidacja nakładających się zakresów

#### SplitRuleForm.razor
- **Komponenty MudBlazor:**
  - `MudNumericField<int>` dla `MinQty`
  - `MudNumericField<int?>` dla `MaxQty` (nullable)
  - `MudNumericField<decimal>` dla `Percent` (0-100)
  - `MudNumericField<int>` dla `MinBatchSize`
  - `MudNumericField<int?>` dla `MaxBatchesPerProject` (nullable)
  - `MudSwitch` dla `IsActive`
  - `MudButton` "Zapisz" / "Anuluj"

---

## 4. Obsługa błędów i walidacji

### 4.1. Globalny error boundary

#### ErrorBoundary.razor
- **Komponenty MudBlazor:**
  - `ErrorBoundary` (Blazor) z `ChildContent` i `ErrorContent`
  - `MudAlert` z `Severity.Error` i komunikatem ogólnym
  - `MudButton` "Odśwież stronę"

**Użycie w MainLayout.razor:**
```razor
<ErrorBoundary>
    <ChildContent>
        @Body
    </ChildContent>
    <ErrorContent>
        <MudAlert Severity="Severity.Error">Wystąpił nieoczekiwany błąd.</MudAlert>
    </ErrorContent>
</ErrorBoundary>
```

### 4.2. Lokalna obsługa błędów

**Strategia:**
- Każdy komponent obsługuje błędy biznesowe przez sprawdzanie wyników serwisów.
- Wyjątki domenowe (`ValidationException`, `BusinessRuleException`) przechwytywane przez try-catch.
- Komunikaty wyświetlane przez `MudSnackbar`:
  - `Severity.Success` — operacja udana
  - `Severity.Warning` — ostrzeżenie (np. przekroczenie limitu)
  - `Severity.Error` — błąd walidacji/biznesowy
  - `Severity.Info` — informacja

**Przykład:**
```csharp
try
{
    var result = await _batchService.UpdateStatusAsync(batchId, newStatus);
    if (result.IsSuccess)
    {
        _snackbar.Add("Status zaktualizowany", Severity.Success);
    }
    else
    {
        _snackbar.Add(result.ErrorMessage, Severity.Error);
    }
}
catch (ValidationException ex)
{
    _snackbar.Add(ex.Message, Severity.Error);
}
catch (BusinessRuleException ex)
{
    _snackbar.Add(ex.Message, Severity.Warning);
}
```

### 4.3. Walidacja formularzy

**Strategia:**
- Walidacja po stronie klienta przez `DataAnnotations` i `EditContext`.
- Walidacja po stronie serwera przez `OrderService.CreateAsync()`.
- Błędy wyświetlane inline przy polach przez `MudTextField.Error` i `MudTextField.ErrorText`.

**Przykład:**
```razor
<MudNumericField @bind-Value="_model.Quantity"
                 Label="Ilość sztuk"
                 Min="1"
                 Max="100000"
                 Error="@(_editContext.GetValidationMessages(() => _model.Quantity).FirstOrDefault())"
                 ErrorText="@(_editContext.GetValidationMessages(() => _model.Quantity).FirstOrDefault())" />
```

---

## 5. Cache i wydajność

### 5.1. Buforowanie danych

**Implementacja przez `IMemoryCache`:**

```csharp
// CacheService.cs
public class CacheService
{
    private readonly IMemoryCache _cache;
    
    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiration)
    {
        if (_cache.TryGetValue(key, out T cached))
            return cached;
        
        var value = await factory();
        _cache.Set(key, value, expiration);
        return value;
    }
    
    public void Invalidate(string key) => _cache.Remove(key);
}
```

**Dane buforowane:**
- Lista formatów produktów (`product_formats_lookup`) — 5 minut
- Lista aktywnych reguł batchowania (tylko do wyświetlenia) — 10 minut

**Invalidacja cache:**
- Po utworzeniu/edycji formatu → `_cacheService.Invalidate("product_formats_lookup")`
- Po utworzeniu/edycji reguły batchowania → `_cacheService.Invalidate("batch_split_rules_lookup")`

**Dane niebuforowane:**
- Kanban (zawsze świeże)
- Dashboard (odświeżany co 60s)
- Widok projektu (zawsze świeży)

---

## 6. Synchronizacja danych

### 6.1. MVP - Blazor Server circuits

**Mechanizm:**
- Blazor Server automatycznie synchronizuje stan komponentu w ramach jednej sesji (circuit).
- Po zmianie statusu/etapu batcha: odświeżenie danych przez ponowne wywołanie `GetKanbanAsync()`.

**Implementacja:**
```csharp
private async Task OnStatusChanged(int batchId, BatchStatus newStatus)
{
    var result = await _batchService.UpdateStatusAsync(batchId, newStatus);
    if (result.IsSuccess)
    {
        await LoadDataAsync(); // Refresh table
        StateHasChanged();
    }
}
```

### 6.2. Przyszłość - SignalR hub (opcjonalnie)

**Jeśli potrzebna synchronizacja między sesjami:**
- Custom SignalR hub z metodą `BroadcastBatchUpdate`.
- Wywoływanie po zapisie zmian w bazie.
- Subskrypcja w komponentach Blazor przez `HubConnection`.

---

## 7. Autoryzacja

### 7.1. Atrybuty autoryzacji

**Użycie:**
```csharp
@attribute [Authorize] // Wymaga zalogowania
@attribute [Authorize(Roles = "Manager")] // Tylko Manager
```

**Przykłady:**
- `Kanban.razor` → `[Authorize]` (Operator + Manager)
- `Dashboard.razor` → `[Authorize(Roles = "Manager")]`
- `Create.razor` → `[Authorize(Roles = "Manager")]`

### 7.2. Warunkowe wyświetlanie w UI

**Użycie `IAuthorizationService`:**
```razor
@inject IAuthorizationService AuthService

@if (await AuthService.IsAuthorizedAsync(User, null, "Manager"))
{
    <MudButton>Wyślij do klienta</MudButton>
}
```

---

## 8. Komponenty MudBlazor - podsumowanie

### Używane komponenty:
- **Layout:** `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`
- **Nawigacja:** `MudNavMenu`, `MudNavLink`
- **Tabele:** `MudTable<T>`, `MudDataGrid` (opcjonalnie)
- **Formularze:** `MudTextField`, `MudNumericField`, `MudSelect<T>`, `MudDatePicker`, `MudSwitch`
- **Przyciski:** `MudButton`
- **Karty:** `MudCard`
- **Alerty:** `MudAlert`, `MudSnackbar`
- **Progress:** `MudProgressLinear`, `MudProgressCircular`
- **Badge:** `MudBadge`
- **Ikony:** `MudIcon`
- **Dialogi:** `MudDialog`, `MudDialogProvider`
- **Grid:** `MudGrid`, `MudItem`
- **Wykresy:** `MudChart` (opcjonalnie)
- **Paginacja:** wbudowana w `MudTable`

---

## 9. Harmonogram implementacji

### Faza 1: Podstawowy layout i autoryzacja (2-3 dni)
- MainLayout z sidebar i nagłówkiem
- NavMenu z warunkowym wyświetlaniem
- Strona logowania
- Autoryzacja przez `[Authorize]`

### Faza 2: Widok Kanban (3-4 dni)
- KanbanTable z batchami
- BatchStatusSelect i BatchStageSelect
- Filtrowanie i sortowanie
- InProgressCounter w nagłówku
- Obsługa błędów

### Faza 3: Widok projektu (2 dni)
- ProjectDetails z informacjami o zleceniu
- BatchList z batchami projektu
- AuditLog z historią zmian
- Przycisk "Wyślij do klienta" (Manager)

### Faza 4: Panel Managera - Dashboard (2 dni)
- DashboardMetrics z metrykami
- Rozkład po statusach i etapach
- Lista pilnych zleceń
- Auto-odświeżanie

### Faza 5: Panel Managera - Zlecenia (2-3 dni)
- Formularz tworzenia zlecenia
- Historia zleceń z filtrowaniem
- Walidacja formularza

### Faza 6: Panel Managera - Formaty i reguły (2-3 dni)
- CRUD formatów produktów
- CRUD reguł batchowania
- Walidacja nakładających się zakresów

### Faza 7: Cache i optymalizacja (1-2 dni)
- CacheService z IMemoryCache
- Buforowanie formatów i reguł
- Invalidacja cache

**Całkowity czas: 14-19 dni roboczych (ok. 3-4 tygodnie)**

---

## 10. Testy UI (opcjonalnie)

### Testy komponentów (bUnit):
- Testy jednostkowe komponentów Blazor
- Testy warunkowego wyświetlania (Manager vs Operator)
- Testy walidacji formularzy

### Testy E2E (Playwright/Selenium):
- Scenariusze użytkownika (US-001 do US-010)
- Testy autoryzacji
- Testy zmian statusu/etapu batcha

---

**Dokument przygotowany:** 12 stycznia 2026  
**Status:** Gotowy do implementacji
