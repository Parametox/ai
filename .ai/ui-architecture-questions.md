<pytania>
1. Czy główna nawigacja aplikacji powinna być zorganizowana jako zakładki (tabs) z widocznymi sekcjami "Kanban" i "Panel Managera" (tylko dla Managera), czy preferowany jest układ z menu bocznym (sidebar) z warunkowym wyświetlaniem opcji w zależności od roli użytkownika?

Rekomendacja: Wykorzystać komponent `MudNavMenu` z `MudBlazor` w układzie z menu bocznym (sidebar), który dynamicznie pokazuje opcje na podstawie roli użytkownika. Dla Managera: "Kanban" i "Panel Managera" (z podmenu: Dashboard, Konfiguracja batchowania, Formaty produktów, Historia zleceń). Dla Operatora: tylko "Kanban". Główny layout powinien zawierać również nagłówek z informacją o zalogowanym użytkowniku i przyciskiem wylogowania.

2. Jak powinien wyglądać widok Kanban - jako tabela z wierszami batchy z dropdownami do zmiany statusu/etapu, czy jako klasyczny widok kanban z kolumnami (New/InProgress/Done) z możliwością przeciągania kart batchy między kolumnami?

Rekomendacja: Dla MVP zastosować prostą tabelę (`MudTable`) z wierszami batchy, ponieważ API/Service Layer nie wspiera operacji drag-and-drop, a wymagałoby to dodatkowych endpointów do aktualizacji pozycji. Tabela powinna zawierać: numer zlecenia (link do projektu), numer batcha, liczbę sztuk, progress bar (`MudProgressLinear`), dropdown statusu (`MudSelect` z wartościami New/InProgress/Done), dropdown etapu (`MudSelect` z wartościami 1-5), oraz wskaźnik ostrzeżenia (ikona) gdy `InProgressCount > 20`. Filtrowanie i sortowanie powinno być obsługiwane przez parametry `KanbanQuery` przekazywane do `BatchService.GetKanbanAsync`.

3. Czy licznik batchy InProgress i ostrzeżenie o przekroczeniu limitu 20 powinny być widoczne globalnie w nagłówku/nawigacji, czy tylko w widoku Kanban i Dashboard?

Rekomendacja: Licznik `InProgressCount` i ostrzeżenie powinny być widoczne w dwóch miejscach: (1) w nagłówku aplikacji jako badge/ikona z liczbą (dla szybkiego dostępu), (2) w widoku Kanban jako alert (`MudAlert` z typem `Severity.Warning`) gdy limit przekroczony. W Dashboard Managera również wyświetlać ostrzeżenie w sekcji metryk. Wartość powinna być odświeżana po każdej zmianie statusu batcha (przez `UpdateBatchStatusResult.InProgressCount`) oraz okresowo (np. co 30 sekund) przez wywołanie `BatchService.GetInProgressCountAsync`.

4. Jak powinien być zorganizowany widok szczegółów projektu - jako osobna strona/route z pełnym widokiem wszystkich batchy projektu, czy jako modal/dialog otwierany z linku w tabeli Kanban?

Rekomendacja: Utworzyć osobną stronę Blazor (`/project/{id}`) z pełnym widokiem projektu, ponieważ zawiera wiele informacji: szczegóły zlecenia, lista wszystkich batchy z ich statusami/etapami, historia audytu, oraz (dla Managera) przycisk "Wyślij do klienta" z warunkową aktywacją. Modal byłby zbyt ciasny dla takiej ilości danych. Strona powinna wykorzystywać `ProjectService.GetByIdAsync` do pobrania `ProjectDetailsDto` z informacją `Completion.CanShip` i `Completion.Reason`.

5. Czy formularz tworzenia zlecenia powinien być dostępny jako osobna strona w Panelu Managera, czy jako dialog/modal otwierany z Dashboard lub sekcji historii zleceń?

Rekomendacja: Utworzyć osobną stronę `/manager/orders/create` w Panelu Managera, ponieważ formularz wymaga walidacji po stronie klienta (Blazor) i serwera (przez `OrderService.CreateAsync`), a także wyświetlenia wyniku utworzenia (lista utworzonych batchy). Formularz powinien zawierać: pole numeru zlecenia (z auto-generacją lub ręcznym wprowadzeniem), dropdown formatu produktu (`ProductFormatService.GetActiveLookupAsync`), pole ilości sztuk (1-100000), oraz pole daty terminu realizacji (walidacja: ≥ dziś+7 dni). Po utworzeniu przekierować do widoku projektu lub pokazać komunikat sukcesu z linkiem.

6. Jak powinna być obsługiwana synchronizacja danych w czasie rzeczywistym między wieloma użytkownikami - czy wykorzystać standardowy mechanizm SignalR w Blazor Server, czy dodać dodatkowe huby do broadcastu zmian statusu/etapu batchy?

Rekomendacja: W MVP wykorzystać standardowy mechanizm Blazor Server (circuit per connection), który automatycznie synchronizuje stan komponentu. Dla operacji zmiany statusu/etapu batcha, po wywołaniu `BatchService.UpdateStatusAsync` / `UpdateStageAsync`, odświeżyć dane Kanbanu przez ponowne wywołanie `GetKanbanAsync`. Jeśli w przyszłości potrzebna będzie synchronizacja między różnymi sesjami użytkowników (np. Operator A zmienia status, Operator B widzi zmianę bez odświeżenia), wówczas dodać custom SignalR hub z metodą `BroadcastBatchUpdate` wywoływaną po zapisie zmian w bazie.

7. Jak powinna być zorganizowana obsługa błędów i walidacji w UI - czy wykorzystać globalny handler błędów z wyświetlaniem komunikatów przez `MudSnackbar`, czy każdy komponent powinien obsługiwać błędy indywidualnie z dedykowanymi komunikatami?

Rekomendacja: Zaimplementować dwupoziomową strategię: (1) Globalny error boundary (`ErrorBoundary` w Blazor) do przechwytywania nieoczekiwanych wyjątków z wyświetleniem ogólnego komunikatu przez `MudSnackbar` z typem `Severity.Error`. (2) Lokalna obsługa błędów biznesowych (walidacja, reguły biznesowe) przez sprawdzanie wyników serwisów (jeśli używają `Result<T>` pattern) lub przechwytywanie wyjątków domenowych (`ValidationException`, `BusinessRuleException`) i mapowanie ich na komunikaty przez `MudSnackbar` z odpowiednim typem (`Severity.Warning` dla ostrzeżeń, `Severity.Error` dla błędów). Każdy formularz powinien również wyświetlać błędy walidacji inline przy polach (`MudTextField` z `Error` i `ErrorText`).

8. Czy widok Dashboard Managera powinien być statyczny (odświeżany tylko przy wejściu na stronę), czy dynamiczny z automatycznym odświeżaniem metryk w określonych interwałach czasu?

Rekomendacja: Dashboard powinien być dynamiczny z automatycznym odświeżaniem co 60 sekund (przez `Timer` w Blazor lub `IHostedService` jeśli potrzebne bardziej zaawansowane rozwiązanie). Metryki powinny być wyświetlane jako karty (`MudCard`) z licznikami (`MudNumericField` lub zwykły tekst) oraz wykresami (opcjonalnie `MudChart` dla rozkładu po statusach/etapach). Lista pilnych zleceń powinna być wyświetlana jako `MudTable` z linkami do projektów. Ostrzeżenie o przekroczeniu limitu InProgress powinno być widoczne na górze dashboardu jako `MudAlert`.

9. Jak powinna być zorganizowana stronicowanie i filtrowanie w widokach listowych (Kanban, historia zleceń, formaty produktów) - czy wykorzystać komponenty MudBlazor do paginacji (`MudTable` z wbudowaną paginacją), czy zaimplementować własne kontrolki z integracją z `PagedResult<T>` z serwisów?

Rekomendacja: Wykorzystać wbudowaną paginację `MudTable` z parametrami `Page` i `PageSize`, które mapują się na `PageQuery` przekazywane do serwisów. Dla Kanbanu: dodać filtry jako `MudSelect` (status, etap) i `MudTextField` (wyszukiwanie po numerze zlecenia/projektu), które aktualizują `KanbanQuery` i wywołują `GetKanbanAsync` z debouncingiem (opóźnienie 300ms) dla pola wyszukiwania. Dla historii zleceń: podobnie, z dodatkowymi filtrami dat (`MudDatePicker` dla `dueFrom`/`dueTo`). Wszystkie filtry powinny być resetowalne przez przycisk "Wyczyść filtry".

10. Czy komponenty UI powinny buforować dane (np. lista formatów produktów do dropdownów, reguły batchowania) w pamięci komponentu/stanu aplikacji, czy każdorazowo pobierać je z serwisów przy potrzebie?

Rekomendacja: Zaimplementować proste buforowanie na poziomie komponentu przez `@inject IMemoryCache` (ASP.NET Core) dla danych rzadko zmieniających się: (1) Lista aktywnych formatów produktów (`ProductFormatLookupDto`) - cache na 5 minut, klucz `"product_formats_lookup"`. (2) Lista aktywnych reguł batchowania (tylko do wyświetlenia, nie do tworzenia zleceń) - cache na 10 minut. Dane Kanbanu i Dashboardu nie powinny być buforowane (zawsze świeże). Po operacjach modyfikujących (utworzenie/edycja formatu, zmiana reguły) invalidować odpowiednie wpisy cache przez `IMemoryCache.Remove`. Dla Blazor Server, cache powinien być `Scoped` lub `Singleton` (w zależności od wymagań współdzielenia między użytkownikami).
</pytania>
