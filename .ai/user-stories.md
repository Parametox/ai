# User Stories - KanbanLite MVP

> Dokument zawiera wszystkie historie użytkownika wydzielone z PRD wraz z kryteriami akceptacji i rekomendacjami technicznymi.

---

## US-001: Logowanie

**Jako** użytkownik  
**Chcę** zalogować się do systemu  
**Aby** korzystać z funkcji odpowiednich dla mojej roli

**Kryteria akceptacji:**
- Po uruchomieniu aplikacji użytkownik niezalogowany widzi ekran logowania
- Formularz logowania z polami: username (login) i password
- Poprawne logowanie przez formularz logowania (walidacja po stronie klienta i serwera)
- Przekierowanie do strony głównej (`/`) po zalogowaniu, która przekierowuje do Kanban
- Widoczna rola użytkownika w nagłówku aplikacji
- Różne widoki menu w zależności od roli (Manager widzi Panel Managera, Operator tylko Kanban)
- Użytkownicy niezalogowani nie mają dostępu do żadnych stron aplikacji poza logowaniem
- Obsługa błędów: wyświetlanie komunikatu przy niepoprawnych danych logowania

**Rekomendacje techniczne:**
- Użyć ASP.NET Core Identity z cookie authentication
- Strona logowania: `/login` z formularzem `MudTextField` (username/password)
- Po zalogowaniu: `NavigationManager.NavigateTo("/")` (strona główna przekierowuje do `/kanban`)
- W nagłówku wyświetlić nazwę użytkownika i przycisk wylogowania
- Autoryzacja widoków przez `[Authorize]` i `[Authorize(Roles = "Manager")]`
- Wszystkie strony poza `/login` wymagają autoryzacji (globalna konfiguracja w `Program.cs`)

---

## US-001a: Ekran logowania dla niezalogowanych użytkowników

**Jako** korzystający z aplikacji  
**Chcę** po uruchomieniu zobaczyć ekran logowania, jeżeli nie jestem zalogowany  
**Aby** nieuprawnione osoby nie miały dostępu do funkcji aplikacji

**Kryteria akceptacji:**
- Użytkownik niezalogowany automatycznie przekierowywany na `/login`
- Wszystkie próby dostępu do chronionych stron przekierowują na `/login`
- Ekran logowania dostępny bez autoryzacji
- Po zalogowaniu przekierowanie na stronę główną (`/`)

**Rekomendacje techniczne:**
- Globalna konfiguracja autoryzacji w `Program.cs`: `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`
- Wyjątek dla `/login`: `[AllowAnonymous]` na stronie `Login.razor`
- Przechwytywanie prób dostępu do chronionych stron i przekierowanie na `/login` z `returnUrl`

---

## US-001b: Przekierowanie po zalogowaniu

**Jako** zalogowany użytkownik  
**Chcę** po wpisaniu poprawnych danych być przekierowany na stronę główną (`/`)  
**Aby** mogę korzystać z aplikacji zgodnie z uprawnieniami

**Kryteria akceptacji:**
- Po poprawnym logowaniu przekierowanie na `/` (strona główna)
- Strona główna (`/`) automatycznie przekierowuje do `/kanban`
- Sesja użytkownika jest utrzymywana (cookie authentication)
- Użytkownik pozostaje zalogowany do czasu wylogowania lub wygaśnięcia sesji

**Rekomendacje techniczne:**
- Po logowaniu: `SignInManager.SignInAsync()` z opcją `isPersistent: false`
- Przekierowanie: `NavigationManager.NavigateTo("/")` lub `returnUrl` jeśli dostępny
- Strona `Index.razor` przekierowuje do `/kanban` przez `NavigationManager.NavigateTo("/kanban")`

---

## US-001c: Różne uprawnienia w zależności od roli

**Jako** zalogowany użytkownik  
**Chcę** mieć różne uprawnienia w zależności od roli ("manager" lub "operator")  
**Aby** mogę korzystać z funkcji zgodnie z przydzieloną rolą

**Kryteria akceptacji:**
- Manager widzi: Kanban + Panel Managera (Dashboard, Utwórz zlecenie, Historia zleceń, Formaty produktów, Konfiguracja batchowania)
- Operator widzi: tylko Kanban
- Próba dostępu do funkcji Managera przez Operatora kończy się komunikatem o braku uprawnień
- Rola użytkownika widoczna w nagłówku aplikacji

**Rekomendacje techniczne:**
- Autoryzacja przez `[Authorize(Roles = "Manager")]` na stronach Panelu Managera
- Warunkowe wyświetlanie menu przez `IAuthorizationService.IsAuthorizedAsync(User, null, "Manager")`
- Komunikaty błędów przez `MudSnackbar` przy próbie nieautoryzowanego dostępu

---

## US-002: Utworzenie zlecenia (Manager)

**Jako** Manager  
**Chcę** utworzyć zlecenie (ilość, format, termin)  
**Aby** rozpocząć produkcję

**Kryteria akceptacji:**
- Formularz z polami: numer zlecenia, format produktu (dropdown), ilość sztuk, termin realizacji
- Walidacje pól:
  - Liczba sztuk: 1-100000 (walidacja po stronie klienta i serwera)
  - Termin realizacji: ≥ dziś+7 dni (walidacja po stronie klienta i serwera)
  - Format produktu: wybór z listy aktywnych formatów
- Automatyczne utworzenie batchy wg tabeli konfiguracyjnej (reguły batchowania)
- Wszystkie batchy startują jako `New` (status) i `Design` (etap 1)
- Po utworzeniu: przekierowanie do widoku projektu lub komunikat sukcesu z linkiem

**Rekomendacje techniczne:**
- Strona: `/manager/orders/create` (tylko Manager)
- Formularz: `MudNumericField` dla ilości, `MudSelect` dla formatu, `MudDatePicker` dla terminu
- Wywołanie: `OrderService.CreateAsync(CreateOrderDto)` → zwraca `Result<ProjectDto>`
- Automatyczne batchowanie: logika w `OrderService` na podstawie aktywnych reguł z `BatchSplitRuleService`
- Cache formatów: `IMemoryCache` z kluczem `"product_formats_lookup"` (5 minut)

---

## US-003: Start batcha (Operator/Manager)

**Jako** Operator  
**Chcę** zmienić status batcha z New na InProgress  
**Aby** rozpocząć pracę nad batchem

**Kryteria akceptacji:**
- Status zmieniony na InProgress przez dropdown w tabeli Kanban
- Licznik InProgress aktualizuje się automatycznie (w nagłówku i w widoku Kanban)
- Ostrzeżenie wyświetla się gdy InProgress > 20 (soft limit)
- Historia zmian zapisana (kto/kiedy/co)
- Niedozwolone przejścia: InProgress → New, Done → InProgress, Done → New

**Rekomendacje techniczne:**
- Komponent: `BatchStatusSelect.razor` z `MudSelect<BatchStatus>`
- Wywołanie: `BatchService.UpdateStatusAsync(batchId, BatchStatus.InProgress)`
- Wynik: `UpdateBatchStatusResult` z `InProgressCount` (do aktualizacji licznika)
- Ostrzeżenie: `MudAlert` z `Severity.Warning` gdy `inProgressCount > 20`
- Odświeżenie danych: ponowne wywołanie `GetKanbanAsync()` po zmianie statusu
- Walidacja przejść: w `BatchService.UpdateStatusAsync()` (tylko do przodu: New → InProgress → Done)

---

## US-004: Przesunięcie etapu batcha (Operator/Manager)

**Jako** Operator  
**Chcę** przesunąć batch do kolejnego etapu  
**Aby** odzwierciedlić postęp pracy

**Kryteria akceptacji:**
- Zmiana etapu tylko do przodu (1→2→3→4→5)
- Progress bar aktualizuje się automatycznie (20/40/60/80/100% dla etapów 1-5)
- Wpis w historii zmian (kto/kiedy/co)
- Niedozwolone: cofanie etapu (np. 3→2)

**Rekomendacje techniczne:**
- Komponent: `BatchStageSelect.razor` z `MudSelect<int>` (wartości 1-5)
- Wyłączenie opcji mniejszych niż aktualny etap (tylko do przodu)
- Wywołanie: `BatchService.UpdateStageAsync(batchId, ProductionStage)`
- Progress bar: `MudProgressLinear` z `Value="{ProgressPercentFromStage(stage)}"`
- Mapowanie etapu na procent: `Design=20%, Print=40%, Cut=60%, Pack=80%, Ship=100%`
- Walidacja: w `BatchService.UpdateStageAsync()` (sprawdzenie `newStage >= oldStage`)

---

## US-005: Zakończenie batcha (Operator/Manager)

**Jako** Operator  
**Chcę** zmienić status batcha z InProgress na Done  
**Aby** zakończyć pracę nad batchem

**Kryteria akceptacji:**
- Status zmieniony na Done przez dropdown w tabeli Kanban
- Licznik InProgress zmniejszony (aktualizacja w czasie rzeczywistym)
- Historia zmian zapisana (kto/kiedy/co)
- Batch może mieć status Done niezależnie od etapu (np. Done na etapie 2 jest możliwe)

**Rekomendacje techniczne:**
- Komponent: `BatchStatusSelect.razor` z `MudSelect<BatchStatus>`
- Wywołanie: `BatchService.UpdateStatusAsync(batchId, BatchStatus.Done)`
- Wynik: `UpdateBatchStatusResult` z `InProgressCount` (do aktualizacji licznika)
- Odświeżenie danych: ponowne wywołanie `GetKanbanAsync()` po zmianie statusu
- Uwaga: Status Done może być ustawiony niezależnie od etapu (np. wstrzymanie pracy)

---

## US-006: Podgląd projektu (Operator/Manager)

**Jako** użytkownik  
**Chcę** wejść w szczegóły projektu  
**Aby** zobaczyć wszystkie batche i statystyki

**Kryteria akceptacji:**
- Widoczne wszystkie batche zlecenia (tabela z numerem batcha, ilością, statusem, etapem, progress barem)
- Progres projektu (średnia wszystkich batchy lub suma ukończonych)
- Status gotowości do wysyłki (informacja czy wszystkie batche spełniają warunki: stage=5 i status=Done)
- Dla Managera: akcje administracyjne (edycja terminu, przycisk "Wyślij do klienta")
- Historia zmian (audit log) dla każdego batcha

**Rekomendacje techniczne:**
- Strona: `/project/{id}` (dostępna dla Operator i Manager)
- Wywołanie: `ProjectService.GetByIdAsync(projectId)` → zwraca `ProjectDetailsDto`
- Komponenty: `BatchList.razor` (tabela batchy), `AuditLog.razor` (historia zmian)
- Progress projektu: obliczenie średniej `ProgressPercent` ze wszystkich batchy
- Status wysyłki: `Completion.CanShip` (true gdy wszystkie batche: stage=5 i status=Done)
- Wizualne oznaczenie: czerwony border dla batchy, które blokują wysyłkę

---

## US-007: Wysyłka do klienta (Manager)

**Jako** Manager  
**Chcę** wysłać zlecenie do klienta  
**Aby** zamknąć projekt

**Kryteria akceptacji:**
- Przycisk aktywny tylko gdy wszystkie batche spełniają warunki: `stage = 5 (Ship)` **i** `status = Done`
- Dialog potwierdzenia przed wysyłką ("Czy na pewno chcesz wysłać zlecenie do klienta?")
- Przeniesienie do historii zleceń (`Project.IsCompleted = true`, opcjonalnie `CompletedAt`/`CompletedBy`)
- Zniknięcie z listy aktywnych batchy (na Kanbanie tylko projekty z `IsCompleted = false`)
- Komunikat sukcesu po wysyłce

**Rekomendacje techniczne:**
- Przycisk: tylko w widoku projektu (`/project/{id}`), tylko dla Managera
- Warunek: `Completion.CanShip = true` (sprawdzenie w `ProjectService.GetByIdAsync()`)
- Wywołanie: `ProjectService.ShipAsync(projectId)` → zwraca `Result<ProjectDto>`
- Dialog: `MudDialog` z `ShowMessageBox` do potwierdzenia
- Po wysyłce: `NavigationManager.NavigateTo("/kanban")` lub `/manager/orders/history`
- Komunikat: `MudSnackbar` z `Severity.Success`

---

## US-008: Konfiguracja reguł batchowania (Manager)

**Jako** Manager  
**Chcę** edytować progi i % batchy  
**Aby** dopasować produkcję do potrzeb

**Kryteria akceptacji:**
- Edycja tabeli reguł (zakresy ilości → procent wielkości batcha)
- Pola: min_qty, max_qty (nullable), percent (0-100), min_batch_size, max_batches_per_project (nullable), is_active
- Walidacja zakresów (nie mogą się nakładać dla aktywnych reguł)
- Wpływ tylko na nowe zlecenia (istniejące projekty nie są modyfikowane)
- CRUD operacje: dodawanie, edycja, deaktywacja reguł

**Rekomendacje techniczne:**
- Strona: `/manager/split-rules` (tylko Manager)
- Komponenty: `SplitRuleList.razor` (tabela), `SplitRuleForm.razor` (dialog formularza)
- Wywołania: `BatchSplitRuleService.GetAllAsync()`, `CreateAsync()`, `UpdateAsync()`, `DeactivateAsync()`
- Walidacja nakładających się zakresów: w `BatchSplitRuleService` (sprawdzenie czy zakresy się nakładają dla aktywnych reguł)
- Cache: invalidacja `"batch_split_rules_lookup"` po modyfikacji (jeśli używany do wyświetlania)

---

## US-009: Zarządzanie formatami (Manager)

**Jako** Manager  
**Chcę** dodawać/edytować formaty  
**Aby** były dostępne przy tworzeniu zlecenia

**Kryteria akceptacji:**
- CRUD formatów produktów (nazwa, status aktywny/nieaktywny)
- Domyślne 3 formaty obecne w systemie: A6 (10x15 cm), Kwadrat (15x15 cm), A5 (14.8x21 cm)
- Formaty widoczne w dropdownie przy tworzeniu zlecenia (tylko aktywne)
- Walidacja: nazwa wymagana, unikalność nazwy

**Rekomendacje techniczne:**
- Strona: `/manager/formats` (tylko Manager)
- Komponenty: `FormatList.razor` (tabela), `FormatForm.razor` (dialog formularza)
- Wywołania: `ProductFormatService.GetAllAsync()`, `CreateAsync()`, `UpdateAsync()`, `DeactivateAsync()`
- Cache: invalidacja `"product_formats_lookup"` po modyfikacji (5 minut TTL)
- Dropdown w formularzu zlecenia: `ProductFormatService.GetActiveLookupAsync()` (z cache)

---

## US-010: Dashboard (Manager)

**Jako** Manager  
**Chcę** widzieć metryki produkcji  
**Aby** zarządzać obciążeniem

**Kryteria akceptacji:**
- Rozkład batchy po statusach (New/InProgress/Done) - tylko z aktywnych projektów
- Rozkład batchy po etapach produkcji (1-5) - tylko z aktywnych projektów
- Lista pilnych zleceń (termin < 7 dni) - sortowanie po terminie, limit 10 najpilniejszych
- Średni czas realizacji zlecenia (ostatnie 30 dni lub wszystkie ukończone jeśli mniej niż 10)
- Ostrzeżenia: InProgress > 20 (soft limit), liczba pilnych zleceń > 5
- Auto-odświeżanie metryk co 60 sekund

**Rekomendacje techniczne:**
- Strona: `/manager/dashboard` (tylko Manager)
- Wywołanie: `DashboardService.GetMetricsAsync()` → zwraca `DashboardMetricsDto`
- Komponenty: `MudCard` dla każdej metryki, `MudTable` dla listy pilnych zleceń, `MudChart` (opcjonalnie) dla wizualizacji
- Auto-odświeżanie: `Timer` w Blazor (co 60 sekund) wywołujący `LoadMetricsAsync()`
- Ostrzeżenia: `MudAlert` z `Severity.Warning` dla przekroczenia limitu InProgress i liczby pilnych zleceń
- Linki: lista pilnych zleceń z linkami do `/project/{id}`

---

## Pytania i niejasności z rekomendacjami

### P1: Czy Operator może zmieniać status batcha z Done z powrotem na InProgress?

**Odpowiedź z PRD:** Nie, statusy tylko do przodu (New → InProgress → Done).

**Rekomendacja:** Zaimplementować walidację w `BatchService.UpdateStatusAsync()`:
- Dozwolone: `New → InProgress`, `InProgress → Done`
- Niedozwolone: `InProgress → New`, `Done → InProgress`, `Done → New`
- W UI: wyłączyć opcje niedozwolonych statusów w `MudSelect` (np. gdy status=Done, ukryć New i InProgress)

---

### P2: Czy batch może mieć status Done na etapie wcześniejszym niż 5 (Ship)?

**Odpowiedź z PRD:** Tak, status Done może być niezależny od etapu (np. wstrzymanie pracy).

**Rekomendacja:** Nie blokować ustawienia statusu Done na dowolnym etapie. Warunek wysyłki projektu: wszystkie batche muszą mieć `stage=5` **i** `status=Done`. W widoku projektu oznaczyć wizualnie (czerwony border) batche, które blokują wysyłkę (np. stage<5 lub status≠Done).

---

### P3: Jak często odświeżać licznik InProgress w nagłówku?

**Rekomendacja:** 
- Po każdej zmianie statusu batcha (przez `UpdateBatchStatusResult.InProgressCount`)
- Okresowo co 30 sekund przez `Timer` w komponencie `InProgressCounter.razor`
- Wywołanie: `BatchService.GetInProgressCountAsync()`

---

### P4: Czy ostrzeżenie o przekroczeniu limitu InProgress powinno blokować akcje?

**Odpowiedź z PRD:** Nie, tylko komunikat + ikona, bez blokowania działania (soft limit).

**Rekomendacja:** Wyświetlać `MudAlert` z `Severity.Warning` w widoku Kanban i Dashboard, ale nie blokować zmiany statusu na InProgress. Ostrzeżenie informacyjne, nie restrykcyjne.

---

### P5: Jak obsługiwać synchronizację danych między wieloma użytkownikami?

**Rekomendacja dla MVP:** 
- Blazor Server automatycznie synchronizuje stan komponentu w ramach jednej sesji (circuit)
- Po zmianie statusu/etapu: odświeżenie danych przez ponowne wywołanie `GetKanbanAsync()`
- Jeśli w przyszłości potrzebna synchronizacja między sesjami: dodać custom SignalR hub z metodą `BroadcastBatchUpdate`

---

### P6: Jakie dane powinny być buforowane w UI?

**Rekomendacja:**
- **Buforowane:** Lista formatów produktów (`product_formats_lookup`) - 5 minut, lista aktywnych reguł batchowania (tylko do wyświetlenia) - 10 minut
- **Niebuforowane:** Kanban (zawsze świeże), Dashboard (odświeżany co 60s), widok projektu (zawsze świeży)
- Implementacja: `IMemoryCache` z invalidacją po modyfikacji danych

---

**Dokument przygotowany:** 12 stycznia 2026  
**Status:** Gotowy do implementacji  
**Źródło:** Wydzielone z `prd.md` sekcja "b) Kluczowe historie użytkownika i ścieżki korzystania"
