# Plan implementacji widoku Kanban

## 1. Przegląd

Widok Kanban jest głównym widokiem aplikacji KanbanLite MVP, dostępnym zarówno dla użytkowników z rolą **Manager** jak i **Operator**. Widok prezentuje listę aktywnych batchy produkcyjnych w formie tabeli z możliwością filtrowania, sortowania i bezpośredniej edycji statusu oraz etapu każdego batcha.

**Cel widoku:**
- Wyświetlenie wszystkich aktywnych batchy z projektów, które nie zostały jeszcze wysłane do klienta (`Project.IsCompleted = false`)
- Umożliwienie szybkiej zmiany statusu batcha (New → InProgress → Done) oraz etapu produkcji (1-5, tylko do przodu)
- Monitorowanie liczby batchy w statusie InProgress z ostrzeżeniem przy przekroczeniu limitu 20
- Filtrowanie i wyszukiwanie batchy po statusie, etapie oraz numerze zlecenia/projektu
- Nawigacja do szczegółów projektu poprzez kliknięcie w numer zlecenia

**User Stories adresowane przez widok:**
- **US-003**: Start batcha (zmiana statusu z New na InProgress)
- **US-004**: Przesunięcie etapu batcha (tylko do przodu)
- **US-005**: Zakończenie batcha (zmiana statusu z InProgress na Done)

## 2. Routing widoku

**Ścieżka routingu:** `/kanban`

**Autoryzacja:** `[Authorize]` - dostęp dla wszystkich zalogowanych użytkowników (Manager i Operator)

**Plik implementacji:**
- `src/Web/Pages/Kanban.razor` - główna strona widoku
- `src/Web/Pages/Kanban.razor.cs` - logika code-behind

**Przekierowanie:** Strona główna (`/`) przekierowuje do `/kanban` po zalogowaniu.

## 3. Struktura komponentów

```
Kanban.razor (strona główna)
├── MudContainer
│   ├── MudAlert (ostrzeżenie InProgress > 20)
│   ├── MudCard (filtry)
│   │   ├── MudSelect (status)
│   │   ├── MudSelect (etap)
│   │   ├── MudTextField (wyszukiwanie)
│   │   └── MudButton (wyczyść filtry)
│   ├── KanbanTable.razor (komponent)
│   │   └── MudTable<KanbanBatchDto>
│   │       ├── Kolumna: Numer zlecenia (link)
│   │       ├── Kolumna: Numer batcha
│   │       ├── Kolumna: Liczba sztuk
│   │       ├── Kolumna: Progress bar
│   │       ├── Kolumna: Status (BatchStatusSelect.razor)
│   │       ├── Kolumna: Etap (BatchStageSelect.razor)
│   │       └── Kolumna: Ostrzeżenie (ikona)
│   └── MudProgressLinear (loading)
```

**Hierarchia komponentów:**
1. **Kanban.razor** - kontener główny z filtrami i logiką zarządzania stanem
2. **KanbanTable.razor** - tabela z batchami (komponent reużywalny)
3. **BatchStatusSelect.razor** - dropdown do zmiany statusu batcha
4. **BatchStageSelect.razor** - dropdown do zmiany etapu batcha

## 4. Szczegóły komponentów

### Kanban.razor

**Opis:** Główna strona widoku Kanban zawierająca filtry, ostrzeżenia oraz tabelę batchy.

**Główne elementy HTML i komponenty dzieci:**
- `MudContainer` z `MaxWidth="MaxWidth.ExtraLarge"` - kontener główny
- `MudAlert` z `Severity="Warning"` - wyświetlany gdy `_inProgressCount > 20`
- `MudCard` - karta z filtrami:
  - `MudSelect<BatchStatus?>` - filtr statusu (null = wszystkie)
  - `MudSelect<ProductionStage?>` - filtr etapu (null = wszystkie)
  - `MudTextField` - wyszukiwanie po numerze zlecenia/projektu (debounce 300ms)
  - `MudButton` "Wyczyść filtry" - resetuje wszystkie filtry
- `KanbanTable` - komponent tabeli (przekazuje `_batches`, `_query`, callbacki)
- `MudProgressLinear` - wyświetlany gdy `_loading = true`

**Obsługiwane zdarzenia:**
- `OnInitializedAsync()` - inicjalizacja: ładowanie danych, uruchomienie timera odświeżania
- `OnStatusFilterChanged(BatchStatus?)` - zmiana filtra statusu
- `OnStageFilterChanged(ProductionStage?)` - zmiana filtra etapu
- `OnSearchTextChanged(string)` - zmiana tekstu wyszukiwania (z debouncingiem)
- `OnClearFilters()` - wyczyszczenie wszystkich filtrów
- `OnStatusChanged(long batchId, BatchStatus newStatus)` - callback z BatchStatusSelect
- `OnStageChanged(long batchId, ProductionStage newStage)` - callback z BatchStageSelect
- `RefreshInProgressCount()` - okresowe odświeżanie licznika (co 30 sekund)

**Warunki walidacji:**
- Brak walidacji po stronie UI (walidacja w serwisie)
- Filtry są opcjonalne (null oznacza "wszystkie")

**Typy:**
- `KanbanQuery` - model zapytania z filtrami
- `KanbanBatchesResult` - wynik z serwisu (zawiera `Items`, `InProgressCount`, `Warnings`, `Page`, `PageSize`, `Total`)
- `KanbanBatchDto` - pojedynczy batch w tabeli
- `WarningDto` - ostrzeżenie (np. przekroczenie limitu)

**Propsy:** Brak (strona główna, nie jest komponentem potomnym)

### KanbanTable.razor

**Opis:** Komponent tabeli wyświetlającej listę batchy z paginacją i sortowaniem.

**Główne elementy:**
- `MudTable<KanbanBatchDto>` z kolumnami:
  - **Numer zlecenia** (`MudLink` do `/project/{item.ProjectId}`) - `item.OrderNumber`
  - **Numer batcha** - `item.BatchNo`
  - **Liczba sztuk** - `item.Quantity`
  - **Progress** (`MudProgressLinear`) - `Value="{item.ProgressPercent}"`, `Color="Color.Primary"`
  - **Status** (`BatchStatusSelect`) - przekazuje `item.BatchId`, `item.Status`, callback `OnStatusChanged`
  - **Etap** (`BatchStageSelect`) - przekazuje `item.BatchId`, `item.Stage`, callback `OnStageChanged`
  - **Ostrzeżenie** (`MudIcon`) - wyświetlany gdy `_inProgressCount > 20` i `item.Status == BatchStatus.InProgress`
- Wbudowana paginacja `MudTable` z `PageSize="50"`, `Page="{_query.Page}"`, `TotalItems="{_batches?.Total ?? 0}"`
- Sortowanie: domyślnie `UpdatedAt DESC` (ustawiane w `KanbanQuery.Sort`)

**Obsługiwane zdarzenia:**
- `OnPageChanged(int page)` - zmiana strony paginacji
- `OnPageSizeChanged(int pageSize)` - zmiana rozmiaru strony
- `OnSortChanged(string sortBy, SortDirection direction)` - zmiana sortowania (opcjonalnie)

**Warunki walidacji:**
- Brak walidacji (tylko wyświetlanie danych)

**Typy:**
- `KanbanBatchDto` - typ elementu tabeli
- `PagedResult<KanbanBatchDto>` - wynik paginacji (z `KanbanBatchesResult`)

**Propsy:**
- `KanbanBatchesResult? Batches` - dane do wyświetlenia
- `KanbanQuery Query` - aktualne filtry i paginacja
- `int InProgressCount` - liczba batchy InProgress (do wyświetlenia ostrzeżenia)
- `EventCallback<long, BatchStatus> OnStatusChanged` - callback zmiany statusu
- `EventCallback<long, ProductionStage> OnStageChanged` - callback zmiany etapu
- `EventCallback<KanbanQuery> OnQueryChanged` - callback zmiany zapytania (paginacja/sortowanie)

### BatchStatusSelect.razor

**Opis:** Komponent dropdown do zmiany statusu batcha z walidacją dozwolonych przejść.

**Główne elementy:**
- `MudSelect<BatchStatus>` z wartościami: `New`, `InProgress`, `Done`
- `MudProgressCircular` - wyświetlany podczas zapisu (`_saving = true`)
- Wyłączenie opcji niedozwolonych:
  - Gdy `CurrentStatus == BatchStatus.Done`: ukryć `New` i `InProgress` (tylko `Done` dostępne)
  - Gdy `CurrentStatus == BatchStatus.InProgress`: ukryć `New` (dostępne: `InProgress`, `Done`)
  - Gdy `CurrentStatus == BatchStatus.New`: wszystkie dostępne (ale tylko `InProgress` dozwolone w walidacji serwisu)

**Obsługiwane zdarzenia:**
- `OnSelectionChanged(BatchStatus newStatus)` - wywołuje `OnStatusChanged.InvokeAsync(BatchId, newStatus)`
- `OnSavingChanged(bool saving)` - aktualizuje stan `_saving` podczas operacji

**Warunki walidacji:**
- **Po stronie UI:** Wyłączenie opcji niedozwolonych w dropdownie (wizualna walidacja)
- **Po stronie serwisa:** Walidacja przejść statusu w `BatchService.UpdateStatusAsync()`:
  - Dozwolone: `New → InProgress`, `InProgress → Done`
  - Niedozwolone: `InProgress → New`, `Done → InProgress`, `Done → New`
  - Błąd: `BusinessRuleException` z kodem `InvalidStatusTransition`

**Typy:**
- `BatchStatus` (enum: `New`, `InProgress`, `Done`)
- `UpdateBatchStatusResult` - wynik operacji (zawiera `InProgressCount`, `Warnings`)

**Propsy:**
- `long BatchId` - ID batcha
- `BatchStatus CurrentStatus` - aktualny status batcha
- `EventCallback<long, BatchStatus> OnStatusChanged` - callback zmiany statusu
- `bool Disabled` - wyłączenie komponentu (opcjonalne)

### BatchStageSelect.razor

**Opis:** Komponent dropdown do zmiany etapu batcha (tylko do przodu).

**Główne elementy:**
- `MudSelect<ProductionStage>` z wartościami: `Design` (1), `Print` (2), `Cut` (3), `Pack` (4), `Ship` (5)
- `MudProgressCircular` - wyświetlany podczas zapisu (`_saving = true`)
- Wyłączenie opcji mniejszych niż aktualny etap:
  - Gdy `CurrentStage == Design (1)`: wszystkie dostępne (1-5)
  - Gdy `CurrentStage == Print (2)`: ukryć `Design` (dostępne: 2-5)
  - Gdy `CurrentStage == Cut (3)`: ukryć `Design`, `Print` (dostępne: 3-5)
  - Gdy `CurrentStage == Pack (4)`: ukryć `Design`, `Print`, `Cut` (dostępne: 4-5)
  - Gdy `CurrentStage == Ship (5)`: ukryć wszystkie wcześniejsze (tylko `Ship` dostępne)

**Obsługiwane zdarzenia:**
- `OnSelectionChanged(ProductionStage newStage)` - wywołuje `OnStageChanged.InvokeAsync(BatchId, newStage)`
- `OnSavingChanged(bool saving)` - aktualizuje stan `_saving` podczas operacji

**Warunki walidacji:**
- **Po stronie UI:** Wyłączenie opcji mniejszych niż aktualny etap (wizualna walidacja)
- **Po stronie serwisa:** Walidacja w `BatchService.UpdateStageAsync()`:
  - Dozwolone: `newStage >= oldStage` i `newStage` w zakresie 1..5
  - Niedozwolone: `newStage < oldStage` (cofanie etapu)
  - Błąd: `BusinessRuleException` z kodem `InvalidStageTransition`

**Typy:**
- `ProductionStage` (enum: `Design = 1`, `Print = 2`, `Cut = 3`, `Pack = 4`, `Ship = 5`)
- `UpdateBatchStageResult` - wynik operacji

**Propsy:**
- `long BatchId` - ID batcha
- `ProductionStage CurrentStage` - aktualny etap batcha
- `EventCallback<long, ProductionStage> OnStageChanged` - callback zmiany etapu
- `bool Disabled` - wyłączenie komponentu (opcjonalne)

## 5. Typy

### Typy wejściowe (Request/Query)

**KanbanQuery:**
```csharp
public sealed record KanbanQuery(
    BatchStatus? Status = null,           // Filtr statusu (null = wszystkie)
    ProductionStage? Stage = null,         // Filtr etapu (null = wszystkie)
    string? Q = null,                      // Wyszukiwanie po numerze zlecenia/projektu
    KanbanSort? Sort = null,               // Sortowanie (DueDateAsc, DueDateDesc, UpdatedAtDesc)
    int Page = 1,                          // Numer strony (1-based)
    int PageSize = 50                      // Rozmiar strony
);
```

**UpdateBatchStatusRequest:**
```csharp
public sealed record UpdateBatchStatusRequest(BatchStatus NewStatus);
```

**UpdateBatchStageRequest:**
```csharp
public sealed record UpdateBatchStageRequest(ProductionStage NewStage);
```

### Typy wyjściowe (DTO/Result)

**KanbanBatchesResult:**
```csharp
public sealed record KanbanBatchesResult(
    IReadOnlyList<KanbanBatchDto> Items,   // Lista batchy
    int InProgressCount,                   // Liczba batchy InProgress (globalnie)
    IReadOnlyList<WarningDto> Warnings,   // Ostrzeżenia (np. InProgressSoftLimitExceeded)
    int Page,                              // Aktualna strona
    int PageSize,                          // Rozmiar strony
    long Total                             // Całkowita liczba batchy
);
```

**KanbanBatchDto:**
```csharp
public sealed record KanbanBatchDto(
    long BatchId,                          // ID batcha
    long ProjectId,                        // ID projektu (do linku)
    string ProjectNumber,                  // Numer projektu
    string OrderNumber,                    // Numer zlecenia (do wyświetlenia)
    DateOnly DueDate,                      // Termin realizacji
    int BatchNo,                           // Numer batcha w projekcie
    int Quantity,                          // Liczba sztuk
    BatchStatus Status,                    // Status batcha
    ProductionStage Stage,                 // Etap produkcji
    int ProgressPercent,                   // Postęp (20/40/60/80/100 dla etapów 1-5)
    DateTimeOffset UpdatedAt               // Data ostatniej aktualizacji
);
```

**UpdateBatchStatusResult:**
```csharp
public sealed record UpdateBatchStatusResult(
    long BatchId,
    BatchStatus OldStatus,
    BatchStatus NewStatus,
    ProductionStage Stage,
    DateTimeOffset UpdatedAt,
    int InProgressCount,                   // Zaktualizowana liczba InProgress
    IReadOnlyList<WarningDto> Warnings    // Ostrzeżenia (jeśli limit przekroczony)
);
```

**UpdateBatchStageResult:**
```csharp
public sealed record UpdateBatchStageResult(
    long BatchId,
    ProductionStage OldStage,
    ProductionStage NewStage,
    BatchStatus Status,
    int ProgressPercent,                   // Zaktualizowany postęp
    DateTimeOffset UpdatedAt
);
```

**WarningDto:**
```csharp
public sealed record WarningDto(
    string Code,                           // Kod ostrzeżenia (np. "InProgressSoftLimitExceeded")
    string Message                         // Komunikat (np. "InProgress batches count is 21 (soft limit is 20).")
);
```

### Enums

**BatchStatus:**
```csharp
public enum BatchStatus
{
    New = 0,
    InProgress = 1,
    Done = 2
}
```

**ProductionStage:**
```csharp
public enum ProductionStage
{
    Design = 1,    // Projektowanie
    Print = 2,     // Druk
    Cut = 3,       // Cięcie
    Pack = 4,      // Pakowanie
    Ship = 5       // Wysyłka
}
```

**KanbanSort:**
```csharp
public enum KanbanSort
{
    DueDateAsc = 0,
    DueDateDesc = 1,
    UpdatedAtDesc = 2
}
```

### ViewModel (stan komponentu)

**KanbanViewModel (w Kanban.razor.cs):**
```csharp
private KanbanQuery _query = new();                    // Aktualne filtry i paginacja
private KanbanBatchesResult? _batches;                 // Wynik zapytania
private int _inProgressCount;                          // Licznik InProgress (do ostrzeżenia)
private bool _loading;                                  // Stan ładowania
private Timer? _refreshTimer;                           // Timer odświeżania licznika
private CancellationTokenSource? _searchCts;           // CancellationToken dla debouncingu wyszukiwania
```

## 6. Zarządzanie stanem

**Stan lokalny komponentu Kanban:**
- `_query` - aktualne filtry, paginacja i sortowanie (aktualizowane przez użytkownika)
- `_batches` - wynik zapytania z serwisu (odświeżany po każdej zmianie)
- `_inProgressCount` - licznik batchy InProgress (odświeżany po zmianie statusu i co 30 sekund)
- `_loading` - flaga ładowania (blokuje interakcje podczas operacji)

**Custom hook (opcjonalny):**
Można utworzyć `useKanbanData` hook do zarządzania stanem i logiką odświeżania, ale w MVP wystarczy logika w `Kanban.razor.cs`.

**Odświeżanie danych:**
1. **Po inicjalizacji:** `OnInitializedAsync()` - pierwsze ładowanie
2. **Po zmianie filtrów:** automatyczne odświeżenie przez `LoadDataAsync()`
3. **Po zmianie statusu/etapu:** odświeżenie przez callback `OnStatusChanged` / `OnStageChanged`
4. **Okresowe:** timer co 30 sekund odświeża tylko `InProgressCount` (nie całej tabeli)

**Synchronizacja między komponentami:**
- `KanbanTable` otrzymuje `_batches` i `_query` jako propsy
- Callbacki `OnStatusChanged` i `OnStageChanged` przekazywane do `BatchStatusSelect` i `BatchStageSelect`
- Po zmianie statusu/etapu: odświeżenie `_batches` i aktualizacja `_inProgressCount` z `UpdateBatchStatusResult`

## 7. Integracja API

### Endpointy serwisów (in-process)

**IBatchService.GetKanbanAsync:**
```csharp
Task<KanbanBatchesResult> GetKanbanAsync(KanbanQuery query, CancellationToken ct = default)
```
- **Wejście:** `KanbanQuery` z filtrami, paginacją i sortowaniem
- **Wyjście:** `KanbanBatchesResult` z listą batchy, licznikiem InProgress i ostrzeżeniami
- **Projekcja:** Join `batches` + `projects` + `orders` (unika N+1)
- **Filtrowanie:** Tylko projekty z `IsCompleted = false`
- **Autoryzacja:** Manager i Operator

**IBatchService.UpdateStatusAsync:**
```csharp
Task<UpdateBatchStatusResult> UpdateStatusAsync(long batchId, UpdateBatchStatusRequest request, CancellationToken ct = default)
```
- **Wejście:** `batchId`, `UpdateBatchStatusRequest` z `NewStatus`
- **Wyjście:** `UpdateBatchStatusResult` z zaktualizowanym statusem, licznikiem InProgress i ostrzeżeniami
- **Transakcja:** Zmiana statusu + zapis audytu w jednej transakcji
- **Walidacja:** Tylko dozwolone przejścia (New → InProgress → Done)
- **Autoryzacja:** Manager i Operator

**IBatchService.UpdateStageAsync:**
```csharp
Task<UpdateBatchStageResult> UpdateStageAsync(long batchId, UpdateBatchStageRequest request, CancellationToken ct = default)
```
- **Wejście:** `batchId`, `UpdateBatchStageRequest` z `NewStage`
- **Wyjście:** `UpdateBatchStageResult` z zaktualizowanym etapem i postępem
- **Transakcja:** Zmiana etapu + zapis audytu w jednej transakcji
- **Walidacja:** Tylko do przodu (`newStage >= oldStage`)
- **Autoryzacja:** Manager i Operator

**IBatchService.GetInProgressCountAsync:**
```csharp
Task<int> GetInProgressCountAsync(CancellationToken ct = default)
```
- **Wejście:** Brak
- **Wyjście:** Liczba batchy w statusie InProgress (globalnie)
- **Autoryzacja:** Manager i Operator

### Mapowanie wyjątków na komunikaty UI

**ValidationException:**
- Mapowanie: `ex.Message` → `MudSnackbar` z `Severity.Error`
- Przykład: "Status nie może być pusty"

**BusinessRuleException:**
- Mapowanie: `ex.Message` → `MudSnackbar` z `Severity.Warning`
- Przykłady:
  - `InvalidStatusTransition` → "Nie można zmienić statusu z Done na InProgress"
  - `InvalidStageTransition` → "Etap można przesunąć tylko do przodu"

**NotFoundException:**
- Mapowanie: `ex.Message` → `MudSnackbar` z `Severity.Error`
- Przykład: "Batch nie został znaleziony"

**ConflictException:**
- Mapowanie: `ex.Message` → `MudSnackbar` z `Severity.Error`
- Przykład: "Konflikt podczas aktualizacji batcha"

## 8. Interakcje użytkownika

### Zmiana filtra statusu

1. Użytkownik wybiera status w `MudSelect` (New/InProgress/Done/Wszystkie)
2. `OnStatusFilterChanged` aktualizuje `_query.Status`
3. Wywołanie `LoadDataAsync()` z nowym `_query`
4. Odświeżenie tabeli z przefiltrowanymi danymi

### Zmiana filtra etapu

1. Użytkownik wybiera etap w `MudSelect` (1-5/Wszystkie)
2. `OnStageFilterChanged` aktualizuje `_query.Stage`
3. Wywołanie `LoadDataAsync()` z nowym `_query`
4. Odświeżenie tabeli z przefiltrowanymi danymi

### Wyszukiwanie

1. Użytkownik wpisuje tekst w `MudTextField`
2. Debouncing 300ms (anulowanie poprzedniego zapytania)
3. `OnSearchTextChanged` aktualizuje `_query.Q`
4. Wywołanie `LoadDataAsync()` z nowym `_query`
5. Odświeżenie tabeli z wynikami wyszukiwania

### Wyczyść filtry

1. Użytkownik klika przycisk "Wyczyść filtry"
2. `OnClearFilters()` resetuje `_query` do wartości domyślnych
3. Wywołanie `LoadDataAsync()` z czystym `_query`
4. Odświeżenie tabeli z wszystkimi batchami

### Zmiana statusu batcha

1. Użytkownik wybiera nowy status w `BatchStatusSelect`
2. `OnSelectionChanged` wywołuje `OnStatusChanged.InvokeAsync(BatchId, NewStatus)`
3. `Kanban.OnStatusChanged` wywołuje `BatchService.UpdateStatusAsync()`
4. Jeśli sukces:
   - Aktualizacja `_inProgressCount` z `result.InProgressCount`
   - Odświeżenie tabeli przez `LoadDataAsync()`
   - Wyświetlenie komunikatu sukcesu (`MudSnackbar`)
   - Jeśli `result.Warnings.Any()`: wyświetlenie ostrzeżenia
5. Jeśli błąd:
   - Wyświetlenie komunikatu błędu (`MudSnackbar`)
   - Tabela pozostaje bez zmian

### Zmiana etapu batcha

1. Użytkownik wybiera nowy etap w `BatchStageSelect`
2. `OnSelectionChanged` wywołuje `OnStageChanged.InvokeAsync(BatchId, NewStage)`
3. `Kanban.OnStageChanged` wywołuje `BatchService.UpdateStageAsync()`
4. Jeśli sukces:
   - Odświeżenie tabeli przez `LoadDataAsync()` (nowy `ProgressPercent`)
   - Wyświetlenie komunikatu sukcesu (`MudSnackbar`)
5. Jeśli błąd:
   - Wyświetlenie komunikatu błędu (`MudSnackbar`)
   - Tabela pozostaje bez zmian

### Kliknięcie w numer zlecenia

1. Użytkownik klika link w kolumnie "Numer zlecenia"
2. `MudLink` nawiguje do `/project/{item.ProjectId}`
3. Przekierowanie do widoku szczegółów projektu

### Paginacja

1. Użytkownik zmienia stronę w `MudTable`
2. `OnPageChanged` aktualizuje `_query.Page`
3. Wywołanie `LoadDataAsync()` z nowym `_query`
4. Odświeżenie tabeli z danymi nowej strony

### Okresowe odświeżanie licznika

1. Timer uruchomiony w `OnInitializedAsync()` (co 30 sekund)
2. `RefreshInProgressCount()` wywołuje `BatchService.GetInProgressCountAsync()`
3. Aktualizacja `_inProgressCount`
4. Jeśli `_inProgressCount > 20`: aktualizacja `MudAlert` z ostrzeżeniem

## 9. Warunki i walidacja

### Warunki weryfikowane przez interfejs

**Filtry (opcjonalne):**
- Status: `null` (wszystkie) lub `BatchStatus` (New/InProgress/Done)
- Etap: `null` (wszystkie) lub `ProductionStage` (1-5)
- Wyszukiwanie: `null` (brak) lub tekst (trimowany)

**Paginacja:**
- `Page >= 1` (domyślnie 1)
- `PageSize >= 1` (domyślnie 50)

**Status batcha (wizualna walidacja w BatchStatusSelect):**
- Gdy `CurrentStatus == Done`: ukryć opcje `New` i `InProgress`
- Gdy `CurrentStatus == InProgress`: ukryć opcję `New`
- Gdy `CurrentStatus == New`: wszystkie opcje dostępne (walidacja serwisu blokuje nieprawidłowe przejścia)

**Etap batcha (wizualna walidacja w BatchStageSelect):**
- Ukryć opcje mniejsze niż `CurrentStage`
- Przykład: Gdy `CurrentStage == Cut (3)`, ukryć `Design (1)` i `Print (2)`

**Wpływ na stan interfejsu:**
- Gdy `_loading == true`: wyłączenie wszystkich interakcji, wyświetlenie `MudProgressLinear`
- Gdy `_inProgressCount > 20`: wyświetlenie `MudAlert` z ostrzeżeniem
- Gdy `_batches == null`: wyświetlenie komunikatu "Brak danych" lub spinnera

### Warunki weryfikowane przez API/serwis

**GetKanbanAsync:**
- `KanbanQuery` - walidacja zakresów (Page >= 1, PageSize >= 1)
- Filtrowanie: tylko projekty z `IsCompleted = false`
- Projekcja: join `batches` + `projects` + `orders` (unika N+1)

**UpdateStatusAsync:**
- `batchId` - batch musi istnieć (404 jeśli nie)
- `NewStatus` - tylko dozwolone przejścia:
  - `New → InProgress` ✅
  - `InProgress → Done` ✅
  - `InProgress → New` ❌
  - `Done → InProgress` ❌
  - `Done → New` ❌
- Soft limit: ostrzeżenie gdy `InProgressCount > 20` (nie blokuje akcji)

**UpdateStageAsync:**
- `batchId` - batch musi istnieć (404 jeśli nie)
- `NewStage` - tylko do przodu:
  - `newStage >= oldStage` ✅
  - `newStage < oldStage` ❌
  - `newStage` w zakresie 1..5 ✅

## 10. Obsługa błędów

### Scenariusze błędów

**Błąd sieci/połączenia:**
- Przechwycenie: `HttpRequestException` lub `TaskCanceledException`
- Obsługa: Wyświetlenie `MudSnackbar` z komunikatem "Błąd połączenia. Spróbuj ponownie."
- Logowanie: Zapis do loggera z poziomem `Error`

**Błąd walidacji (ValidationException):**
- Przechwycenie: `ValidationException` z listą błędów
- Obsługa: Wyświetlenie `MudSnackbar` z `Severity.Error` i komunikatem z `ex.Message`
- Logowanie: Zapis do loggera z poziomem `Warning`

**Błąd reguły biznesowej (BusinessRuleException):**
- Przechwycenie: `BusinessRuleException` z kodem błędu
- Obsługa: Wyświetlenie `MudSnackbar` z `Severity.Warning` i komunikatem z `ex.Message`
- Przykłady:
  - `InvalidStatusTransition` → "Nie można zmienić statusu. Dozwolone przejścia: New → InProgress → Done"
  - `InvalidStageTransition` → "Etap można przesunąć tylko do przodu"

**Błąd nieznalezienia (NotFoundException):**
- Przechwycenie: `NotFoundException`
- Obsługa: Wyświetlenie `MudSnackbar` z `Severity.Error` i komunikatem "Batch nie został znaleziony"
- Logowanie: Zapis do loggera z poziomem `Warning`
- Akcja: Odświeżenie tabeli (batch mógł zostać usunięty)

**Błąd konfliktu (ConflictException):**
- Przechwycenie: `ConflictException`
- Obsługa: Wyświetlenie `MudSnackbar` z `Severity.Error` i komunikatem "Konflikt podczas aktualizacji. Odśwież stronę i spróbuj ponownie."
- Logowanie: Zapis do loggera z poziomem `Warning`

**Błąd nieoczekiwany (Exception):**
- Przechwycenie: Ogólny `catch (Exception ex)`
- Obsługa: Wyświetlenie `MudSnackbar` z `Severity.Error` i komunikatem "Wystąpił nieoczekiwany błąd. Skontaktuj się z administratorem."
- Logowanie: Zapis do loggera z poziomem `Error` z pełnym stack trace

### Strategia obsługi błędów

**Globalny error boundary:**
- `ErrorBoundary` w `MainLayout.razor` przechwytuje nieoczekiwane wyjątki w komponentach
- Wyświetlenie ogólnego komunikatu błędu z możliwością odświeżenia strony

**Lokalna obsługa błędów:**
- Każda operacja asynchroniczna w `try-catch`
- Mapowanie wyjątków na komunikaty użytkownika przez `MudSnackbar`
- Logowanie błędów do loggera (Serilog) z odpowiednim poziomem

**Przypadki brzegowe:**
- Pusta lista batchy: Wyświetlenie komunikatu "Brak aktywnych batchy" zamiast pustej tabeli
- Timeout zapytania: Wyświetlenie komunikatu "Zapytanie trwa zbyt długo. Spróbuj ponownie."
- Równoczesne zmiany: Ostatnia zmiana wygrywa (serwis weryfikuje stan przed aktualizacją)

## 11. Kroki implementacji

### Krok 1: Przygotowanie struktury plików

1. Utworzyć plik `src/Web/Pages/Kanban.razor`
2. Utworzyć plik `src/Web/Pages/Kanban.razor.cs`
3. Utworzyć folder `src/Web/Components/Kanban/`
4. Utworzyć pliki:
   - `src/Web/Components/Kanban/KanbanTable.razor`
   - `src/Web/Components/Kanban/KanbanTable.razor.cs`
   - `src/Web/Components/Kanban/BatchStatusSelect.razor`
   - `src/Web/Components/Kanban/BatchStatusSelect.razor.cs`
   - `src/Web/Components/Kanban/BatchStageSelect.razor`
   - `src/Web/Components/Kanban/BatchStageSelect.razor.cs`

### Krok 2: Implementacja logiki Kanban.razor.cs

1. Zdefiniować pola prywatne:
   - `_query`, `_batches`, `_inProgressCount`, `_loading`, `_refreshTimer`, `_searchCts`
2. Wstrzyknąć zależności:
   - `IBatchService`, `ISnackbar`, `ILogger<Kanban>`, `NavigationManager`
3. Zaimplementować `OnInitializedAsync()`:
   - Wywołanie `LoadDataAsync()`
   - Uruchomienie timera odświeżania (co 30 sekund)
4. Zaimplementować `LoadDataAsync()`:
   - Ustawienie `_loading = true`
   - Wywołanie `BatchService.GetKanbanAsync(_query)`
   - Aktualizacja `_batches` i `_inProgressCount`
   - Obsługa błędów (try-catch)
5. Zaimplementować callbacki:
   - `OnStatusFilterChanged`, `OnStageFilterChanged`, `OnSearchTextChanged`, `OnClearFilters`
   - `OnStatusChanged`, `OnStageChanged`
6. Zaimplementować `RefreshInProgressCount()`:
   - Wywołanie `BatchService.GetInProgressCountAsync()`
   - Aktualizacja `_inProgressCount`
7. Zaimplementować `Dispose()`:
   - Zatrzymanie i zwolnienie timera
   - Anulowanie `_searchCts`

### Krok 3: Implementacja markupu Kanban.razor

1. Dodać `@attribute [Authorize]`
2. Dodać `@page "/kanban"`
3. Utworzyć strukturę:
   - `MudContainer` z `MaxWidth="MaxWidth.ExtraLarge"`
   - `MudAlert` (warunkowo gdy `_inProgressCount > 20`)
   - `MudCard` z filtrami:
     - `MudSelect` dla statusu
     - `MudSelect` dla etapu
     - `MudTextField` dla wyszukiwania
     - `MudButton` "Wyczyść filtry"
   - `KanbanTable` (przekazać propsy)
   - `MudProgressLinear` (warunkowo gdy `_loading`)

### Krok 4: Implementacja komponentu KanbanTable

1. Zdefiniować propsy w `KanbanTable.razor.cs`:
   - `[Parameter] KanbanBatchesResult? Batches`
   - `[Parameter] KanbanQuery Query`
   - `[Parameter] int InProgressCount`
   - `[Parameter] EventCallback<long, BatchStatus> OnStatusChanged`
   - `[Parameter] EventCallback<long, ProductionStage> OnStageChanged`
   - `[Parameter] EventCallback<KanbanQuery> OnQueryChanged`
2. Zaimplementować `MudTable<KanbanBatchDto>` w `KanbanTable.razor`:
   - Kolumny: Numer zlecenia (link), Numer batcha, Liczba sztuk, Progress, Status, Etap, Ostrzeżenie
   - Paginacja wbudowana
   - Sortowanie (opcjonalnie)
3. Zaimplementować callbacki:
   - `OnPageChanged`, `OnPageSizeChanged` (aktualizacja `_query` i wywołanie `OnQueryChanged`)

### Krok 5: Implementacja komponentu BatchStatusSelect

1. Zdefiniować propsy w `BatchStatusSelect.razor.cs`:
   - `[Parameter] long BatchId`
   - `[Parameter] BatchStatus CurrentStatus`
   - `[Parameter] EventCallback<long, BatchStatus> OnStatusChanged`
   - `[Parameter] bool Disabled`
2. Zaimplementować logikę wyłączania opcji:
   - Metoda `IsOptionDisabled(BatchStatus status)` zwracająca `bool`
3. Zaimplementować `MudSelect<BatchStatus>` w `BatchStatusSelect.razor`:
   - Wartości: `New`, `InProgress`, `Done`
   - Wyłączenie opcji przez `Disabled="{IsOptionDisabled(option)}"`
   - `OnSelectionChanged` wywołuje `OnStatusChanged`
   - `MudProgressCircular` podczas zapisu

### Krok 6: Implementacja komponentu BatchStageSelect

1. Zdefiniować propsy w `BatchStageSelect.razor.cs`:
   - `[Parameter] long BatchId`
   - `[Parameter] ProductionStage CurrentStage`
   - `[Parameter] EventCallback<long, ProductionStage> OnStageChanged`
   - `[Parameter] bool Disabled`
2. Zaimplementować logikę wyłączania opcji:
   - Metoda `IsOptionDisabled(ProductionStage stage)` zwracająca `bool` (stage < CurrentStage)
3. Zaimplementować `MudSelect<ProductionStage>` w `BatchStageSelect.razor`:
   - Wartości: `Design`, `Print`, `Cut`, `Pack`, `Ship`
   - Wyłączenie opcji przez `Disabled="{IsOptionDisabled(option)}"`
   - `OnSelectionChanged` wywołuje `OnStageChanged`
   - `MudProgressCircular` podczas zapisu

### Krok 7: Integracja z serwisami i obsługa błędów

1. Zarejestrować `IBatchService` w DI (jeśli nie zarejestrowany)
2. Zaimplementować mapowanie wyjątków na komunikaty:
   - Metoda `HandleError(Exception ex)` w `Kanban.razor.cs`
   - Mapowanie `ValidationException`, `BusinessRuleException`, `NotFoundException`, `ConflictException`
3. Dodać logowanie błędów:
   - `ILogger<Kanban>` w konstruktorze
   - Logowanie w `HandleError()` z odpowiednim poziomem

### Krok 8: Testowanie i optymalizacja

1. Testy manualne:
   - Filtrowanie po statusie i etapie
   - Wyszukiwanie po numerze zlecenia
   - Zmiana statusu batcha (wszystkie dozwolone przejścia)
   - Zmiana etapu batcha (tylko do przodu)
   - Paginacja
   - Ostrzeżenie przy przekroczeniu limitu InProgress
   - Obsługa błędów (nieprawidłowe przejścia, błędy sieci)
2. Optymalizacja:
   - Debouncing wyszukiwania (300ms)
   - Okresowe odświeżanie tylko licznika (nie całej tabeli)
   - Unikanie niepotrzebnych odświeżeń (sprawdzanie czy dane się zmieniły)

### Krok 9: Dokumentacja i cleanup

1. Dodać komentarze XML do publicznych metod
2. Sprawdzić zgodność z regułami frontend (`.cursor/rules/frontend.mdc`):
   - Rozdzielenie logiki (`.razor.cs`), stylów (`.razor.css`), markupu (`.razor`)
3. Upewnić się, że wszystkie importy są obecne
4. Sprawdzić, że nie ma hardcoded wartości (użyć stałych/config)

---

**Status:** Plan gotowy do implementacji  
**Data utworzenia:** 2026-01-15  
**Wersja:** 1.0
