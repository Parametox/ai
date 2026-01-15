# Implementacja endpointu (workflow 3×3)

Twoim zadaniem jest wdrożenie **operacji aplikacyjnej (use case)** w monolicie **Blazor Server** (bez publicznego **REST API** w MVP). Implementacja ma być solidna: walidacja, autoryzacja **RBAC**, obsługa błędów (spójny **Result Pattern**), transakcje tam gdzie wymagane, oraz brak pułapek wydajnościowych (**N+1** w EF Core).

Najpierw dokładnie przejrzyj dostarczone plany i reguły:

<implementation_plan>
@.ai/view-implementation-plan.md
@.ai/api-plan.md
@.ai/service-layer-plan.md
@.ai/10xdevs-prompts/plan impl api.md
</implementation_plan>

<types>
@src/Types.cs
</types>

<implementation_rules>
@.cursor/rules/dev.mdc
@.cursor/rules/backend.mdc
</implementation_rules>

<implementation_approach>
Realizuj maksymalnie 3 kroki planu implementacji, podsumuj krótko co zrobiłeś i opisz plan na 3 kolejne działania — zatrzymaj w tym momencie pracę i czekaj na mój feedback.
</implementation_approach>

## Kroki do wykonania (dla pojedynczego endpointu)

### 1) Analiza planu wdrożenia (bez kodu)

- Zidentyfikuj **operację (use case)** na podstawie `.ai/api-plan.md` (notacja REST-like służy tu wyłącznie jako opis kontraktu).
- Wypisz wszystkie **wejścia** operacji:
  - parametry z „route/query/body” potraktuj jako pola **request/command/query** (typy z `src/Types.cs`)
  - doprecyzuj, co pochodzi z kontekstu użytkownika (np. `UserId`, role, tenant) zamiast z UI
- Określ **wyjście** (DTO/Result z `src/Types.cs`) oraz scenariusze błędów jako **błędy domenowe/aplikacyjne** (bez kodów HTTP), np.:
  - `NotFound`, `Conflict`, `ValidationFailed/BusinessRuleViolation`, `Unauthorized/Forbidden`, `Unexpected`
- Wyłap wymagania specjalne:
  - **RBAC**: które role mają dostęp.
  - **Transakcja**: czy zmiana wymaga atomowości (np. update batch + insert audytu).
  - **Wydajność**: czy endpoint musi robić projekcję/join w jednym zapytaniu (Kanban/Dashboard) i unikać **N+1**.

### 2) Kontrakt i implementacja operacji (in-process) — interfejs + serwis

- Zdefiniuj/wykorzystaj **interfejs serwisu** w warstwie Application (zgodnie z `.ai/service-layer-plan.md`), np. `IXxxService` z metodą:
  - `Task<Result<TOut>> ExecuteAsync(TIn request, CancellationToken ct = default)` (albo istniejąca konwencja w repo)
- Zaimplementuj serwis (use case) w Application:
  - serwis = walidacja biznesowa, autoryzacja, transakcje, dostęp do EF Core (przez DataAccess/Infrastructure)
  - UI nie może zawierać reguł biznesowych ani „ukrytej autoryzacji”
- Zadbaj o `CancellationToken`.

### 3) Walidacja, błędy, autoryzacja, transakcje

- Waliduj wejście:
  - walidacje „shape” (np. wymagane pola, zakresy, trim) na wejściu serwisu — spójnie z istniejącą konwencją w repo.
  - walidacje reguł biznesowych w serwisie (np. tylko etap do przodu, statusy tylko `New → InProgress → Done`).
- Autoryzacja:
  - UI: wymuś `[Authorize]` / policy/role na komponencie/stronie Blazor (jeśli dotyczy).
  - serwis: wymuś kontrolę dostępu zgodnie z planem RBAC (bramka musi być po stronie Application).
- Obsługa błędów:
  - stosuj **Result Pattern** zgodnie z `.cursor/rules/dev.mdc` (bez wyjątków domenowych).
  - mapuj wynik serwisu na czytelne komunikaty UI (np. snackbar/validation summary/dialog) oraz stan widoku.
- Transakcje:
  - gdy plan tego wymaga, zapis zmian + audyt wykonaj **atomowo** (jedna transakcja).

## Zasada 3×3 (iteracje)

Po każdej iteracji (maks. 3 kroki) zwróć:

- **Co zrobiłem**: 3–6 punktów, tylko fakty.
- **Co planuję dalej (kolejne 3 kroki)**: 3 punkty.

Potem **zatrzymaj pracę i czekaj na feedback**.

