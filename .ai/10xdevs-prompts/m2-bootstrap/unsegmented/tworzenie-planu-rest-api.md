# Projektowanie Prostej Warstwy Serwisów (.NET Service Layer)

<db-plan>
@.ai/db-plan.md
</db-plan>

<prd>
@.ai/prd.md
</prd>

<tech-stack>
@.ai/tech-stack.md
</tech-stack>

**Kontekst Architektoniczny:**
Stawiamy na maksymalną prostotę (KISS). Nie używamy CQRS, MediatR ani skomplikowanych wzorców funkcyjnych. Logika ma być zawarta w standardowych serwisach .NET wstrzykiwanych przez DI. Metody serwisów operują bezpośrednio na modelach DTO lub encjach (zależnie od tego, co sugeruje PRD) i są wywoływane bezpośrednio przez komponenty Razor.

Jesteś pragmatycznym deweloperem .NET. Twoim zadaniem jest stworzenie przejrzystego planu serwisów backendowych.

### Zadania do wykonania:

1. **Analiza Struktury:**
   - Przejrzyj tabelę bazy danych i PRD.
   - Pogrupuj funkcjonalności w logiczne serwisy (np. `ProjectService`, `UserService`).

2. **Projektowanie Metod:**
   - Zaplanuj standardowe metody CRUD (Create, Read, Update, Delete).
   - Dodaj metody dla specyficznej logiki biznesowej opisanej w PRD (np. `ChangeStatus`, `AssignUser`).
   - Używaj standardowych typów zwracanych: `Task<T>`, `Task<IEnumerable<T>>` lub `Task<bool>`.

3. **Uproszczona Walidacja i Bezpieczeństwo:**
   - Wykorzystaj atrybuty `[Authorize]` lub proste sprawdzenia `User.IsInRole` wewnątrz metod.
   - Walidacja powinna opierać się na standardowych mechanizmach .NET (np. Data Annotations).

---

### Proces myślowy (wewnątrz tagów <service_analysis>):
1. Wymień główne encje i określ, jakie serwisy są potrzebne do ich obsługi.
2. Zidentyfikuj w PRD konkretne akcje użytkownika i przypisz je jako metody do serwisów.
3. Zidentyfikuj relacje (np. "Projekt ma wiele Zadań") i zdecyduj, czy Zadania obsługuje `ProjectService` czy dedykowany `TaskService`.
4. Ustal proste reguły walidacji (np. "Pole X nie może być puste") na podstawie schematu DB.

---

### Struktura dokumentu wynikowego (.ai/service-layer-plan.md):

# Backend Service Plan (Blazor Server)

## 1. Lista Serwisów i Rejestracja DI
- Krótka lista serwisów i informacja o ich cyklu życia (np. `builder.Services.AddScoped<IProjectService, ProjectService>();`).

## 2. Szczegóły Serwisów
Dla każdego serwisu (np. `ProjectService`):
- **Metody:** Nazwa metody, parametry wejściowe, zwracany typ.
- **Opis:** Krótko, co metoda robi i jaką logikę z PRD realizuje.
- **Walidacja:** Podstawowe warunki, które muszą być spełnione przed zapisem.
- **Uprawnienia:** Kto (jaka rola) ma dostęp do danej metody.

## 3. Modele Danych (DTOs)
- Lista prostych klas/rekordów C# służących do przesyłania danych między widokiem a serwisem (jeśli różnią się od encji bazy danych).

## 4. Obsługa Błędów
- Prosta strategia (np. rzucanie wyjątków biznesowych lub zwracanie `null` / `false` przy niepowodzeniu).