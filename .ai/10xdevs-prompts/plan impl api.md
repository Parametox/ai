Jesteś doświadczonym architektem oprogramowania, którego zadaniem jest stworzenie szczegółowego planu wdrożenia **operacji aplikacyjnej (use case)** w architekturze **Clean Architecture** dla monolitu **Blazor Server**. Twój plan ma poprowadzić zespół programistów w poprawnym wdrożeniu pełnego przepływu: UI (Blazor) → warstwa aplikacyjna (serwisy/use case’y) → dostęp do danych (EF Core/PostgreSQL) + RBAC (Identity) + audyt.

Zanim zaczniemy, zapoznaj się z poniższymi informacjami:

1. Specyfikacja kontraktu operacji (REST-like notacja jako opis kontraktu, **bez publicznego REST API w MVP**):
<operation_contract_specification>
.ai\api-plan.md
</operation_contract_specification>

2. Plan warstwy serwisów (kontrakt in-process dla Blazor Server):
<service_layer_plan>
.ai\service-layer-plan.md
</service_layer_plan>

3. Powiązane zasoby bazy danych (jeśli dotyczy):
<related_db_resources>
.ai\db-plan.md
</related_db_resources>

4. Definicje typów (jeśli dotyczy):
<type_definitions>
.ai\types.md
</type_definitions>

5. Tech stack:
<tech_stack>
.ai\tech-stack.md
</tech_stack>

6. Implementation rules:
<implementation_rules>
.cursor\rules\backend.mdc, .cursor\rules\frontend.mdc, .cursor\rules\dev.mdc
</implementation_rules>

Twoim zadaniem jest stworzenie kompleksowego planu wdrożenia **operacji** (np. „Create order”, „Update batch stage”, „Ship project to customer”) w stylu Clean Architecture, zgodnie z dostarczonym kontraktem operacji i planem warstwy serwisów. Przed dostarczeniem ostatecznego planu użyj znaczników <analysis>, aby przeanalizować informacje i nakreślić swoje podejście. W tej analizie upewnij się, że:

1. Podsumuj kluczowe punkty specyfikacji **operacji** (wejście/wyjście, reguły biznesowe, RBAC, audyt).
2. Wymień wymagane i opcjonalne pola wejścia (request/command/query) wynikające ze specyfikacji.
3. Wymień niezbędne typy: **DTO/Request/Result** (oraz ewentualnie Command/Query, jeśli stosujesz CQRS).
4. Zaprojektuj miejsce logiki w architekturze:
   - UI (Blazor) — wywołanie serwisu, mapowanie błędów na komunikaty UI
   - Application — serwis/use case, walidacja, autoryzacja, transakcje
   - Domain — reguły/inwarianty (jeśli wydzielane), typy domenowe
   - Infrastructure/DataAccess — EF Core, repozytoria/projekcje, migracje
5. Zaplanuj walidację danych wejściowych zgodnie ze specyfikacją, ograniczeniami DB i regułami aplikacji.
6. Zaplanuj **audyt** (jeśli dotyczy) tak, aby był zapisywany **transakcyjnie** razem ze zmianą.
7. Identyfikuj zagrożenia bezpieczeństwa: **cookie auth**, **RBAC**, CSRF/antiforgery dla operacji modyfikujących, oraz kontrola dostępu w serwisach.
8. Nakreśl scenariusze błędów i sposób ich sygnalizacji **bez HTTP** (np. wyjątki domenowe / `Result<T>` / typy błędów), oraz mapowanie na UI.

Po przeprowadzeniu analizy utwórz szczegółowy plan wdrożenia w formacie markdown. Plan powinien zawierać następujące sekcje:

1. Przegląd operacji (use case)
2. Kontrakt serwisu (sygnatura metody, model wejścia/wyjścia)
3. Modele danych (DTO/Request/Result + mapowania)
4. Przepływ danych i transakcje (UI → Application → DataAccess)
5. Autoryzacja i bezpieczeństwo (Identity/RBAC, antiforgery, granice dostępu)
6. Walidacja i reguły biznesowe (DB constraints vs app rules)
7. Obsługa błędów (strategia, kody/błędy domenowe, mapowanie na UI)
8. Wydajność i spójność (projekcje, unikanie N+1, agregacje, concurrency)
9. Kroki implementacji (konkretne zadania, pliki/klasy, DI, testy)

W całym planie upewnij się, że
- Nie zakładasz istnienia **publicznych kontrolerów REST** w MVP — opisujesz kontrakt operacji jako **metody serwisów** wywoływane in-process przez Blazor Server.
- Każdą operację opierasz o istniejący plan serwisów (lub tworzysz nowy serwis tylko jeśli brak pasującego).
- Zwracasz uwagę na potencjalne wąskie gardła (szczególnie **N+1 w EF Core**) i opisujesz projekcje/joiny/agregacje.
- Dostosowanie do dostarczonego stacku technologicznego
- Postępuj zgodnie z podanymi zasadami implementacji

Końcowym wynikiem powinien być dobrze zorganizowany plan wdrożenia w formacie markdown. Oto przykład tego, jak powinny wyglądać dane wyjściowe:

``markdown
# Clean Architecture Use Case Implementation Plan: [Nazwa operacji]

## 1. Przegląd operacji (use case)
[Krótki opis celu i funkcjonalności operacji, kto ją wywołuje w UI i po co]

## 2. Kontrakt serwisu (Application)
- Serwis: [np. IOrderService]
- Metoda: [np. Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)]
- Wejście:
  - Wymagane: [lista pól]
  - Opcjonalne: [lista pól]
- Wyjście: [DTO/Result]

## 3. Modele danych (DTO/Request/Result)
[Lista typów + krótkie mapowanie: encje EF ↔ DTO; projekcje dla list]

## 4. Przepływ danych i transakcje
[Kroki wykonania w warstwach; gdzie zaczyna/kończy się transakcja; co musi być atomiczne (np. zmiana batcha + wpis audytu)]

## 5. Autoryzacja i bezpieczeństwo
[Role/policies; kontrola dostępu na wejściu serwisu; CSRF/antiforgery dla operacji modyfikujących; walidacja/normalizacja danych wejściowych]

## 6. Walidacja i reguły biznesowe
[Walidacje wejścia; reguły domenowe; powiązanie z constraintami DB; scenariusze odmowy (np. invalid stage transition)]

## 7. Obsługa błędów
[Strategia: wyjątki domenowe / Result; kody błędów; mapowanie na UI (np. snackbar/dialog); logowanie (np. Serilog)]

## 8. Wydajność i spójność
[Unikanie N+1; projekcje; agregacje; indeksy; concurrency jeśli dotyczy]

## 9. Kroki implementacji
1. [Krok 1]
2. [Krok 2]
3. [Krok 3]
...
```

Końcowe wyniki powinny składać się wyłącznie z planu wdrożenia w formacie markdown i nie powinny powielać ani powtarzać żadnej pracy wykonanej w sekcji analizy.

Pamiętaj, aby zapisać swój plan wdrożenia jako .ai/view-implementation-plan.md. Upewnij się, że plan jest szczegółowy, przejrzysty i zapewnia kompleksowe wskazówki dla zespołu programistów.